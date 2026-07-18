#nullable enable

using ArcGIS.Desktop.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Infrastructure
{
    internal static class ToolboxCatalogService
    {
        private const string DefaultToolsetName = "工具";

        public static ToolboxCatalog Load(string toolboxPath)
        {
            if (string.IsNullOrWhiteSpace(toolboxPath))
                throw new ArgumentException("工具箱路径为空。", nameof(toolboxPath));

            if (!File.Exists(toolboxPath))
                throw new FileNotFoundException("工具箱文件不存在。", toolboxPath);

            var extension = Path.GetExtension(toolboxPath).ToLowerInvariant();
            return extension switch
            {
                ".atbx" => LoadAtbx(toolboxPath),
                ".tbx" => LoadToolboxWithArcPyDescribe(toolboxPath),
                ".pyt" => LoadPythonToolbox(toolboxPath),
                _ => throw new NotSupportedException("仅支持 atbx、tbx 和 pyt 工具箱。")
            };
        }

        private static ToolboxCatalog LoadAtbx(string toolboxPath)
        {
            using var archive = ZipFile.OpenRead(toolboxPath);
            var toolboxContent = ReadJsonObject(archive, "toolbox.content");
            var toolboxResources = ReadResourceMap(archive, "toolbox.content.rc");

            var catalog = new ToolboxCatalog
            {
                ToolboxPath = toolboxPath,
                DisplayName = ResolveResource(toolboxContent?["displayname"]?.GetValue<string>(), toolboxResources)
                    ?? Path.GetFileNameWithoutExtension(toolboxPath),
                Alias = toolboxContent?["alias"]?.GetValue<string>() ?? string.Empty
            };

            var seenTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var toolsets = toolboxContent?["toolsets"] as JsonObject;
            if (toolsets is not null)
            {
                foreach (var item in toolsets)
                {
                    var toolsetName = ResolveToolsetName(item.Key, toolboxResources);
                    var toolNames = ReadToolNames(item.Value).Where(seenTools.Add).ToList();
                    AddAtbxToolset(catalog, archive, toolboxPath, toolsetName, toolNames);
                }
            }

            if (catalog.ToolCount == 0)
            {
                var toolNames = archive.Entries
                    .Select(entry => entry.FullName)
                    .Where(name => name.EndsWith(".tool/", StringComparison.OrdinalIgnoreCase))
                    .Select(name => name[..^6])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                AddAtbxToolset(catalog, archive, toolboxPath, DefaultToolsetName, toolNames);
            }

            RemoveEmptyToolsets(catalog);
            if (catalog.ToolCount == 0)
                throw new InvalidOperationException("未在 atbx 工具箱中找到可加载工具。");

            return catalog;
        }

        private static void AddAtbxToolset(
            ToolboxCatalog catalog,
            ZipArchive archive,
            string toolboxPath,
            string toolsetName,
            IEnumerable<string> toolNames)
        {
            var tools = toolNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new ToolboxTool
                {
                    Name = name,
                    Caption = ReadAtbxToolCaption(archive, name),
                    ToolPath = Path.Combine(toolboxPath, name),
                    SourceToolPath = name
                })
                .ToList();

            if (tools.Count == 0)
                return;

            AddToolsetPath(catalog.Toolsets, toolsetName, tools);
        }

        private static ToolboxCatalog LoadPythonToolbox(string toolboxPath)
        {
            ToolboxCatalog catalog;
            try
            {
                catalog = LoadPythonToolboxFromSource(toolboxPath);
            }
            catch
            {
                catalog = new ToolboxCatalog
                {
                    ToolboxPath = toolboxPath,
                    DisplayName = Path.GetFileNameWithoutExtension(toolboxPath)
                };
            }

            if (catalog.ToolCount == 0)
                catalog = LoadToolboxWithArcPyDescribe(toolboxPath);

            RemoveEmptyToolsets(catalog);
            if (catalog.ToolCount == 0)
                throw new InvalidOperationException("未在 pyt 工具箱中找到可加载工具。");

            return catalog;
        }

        private static ToolboxCatalog LoadPythonToolboxFromSource(string toolboxPath)
        {
            var source = File.ReadAllText(toolboxPath, Encoding.UTF8);
            var classBlocks = ReadPythonClassBlocks(source);
            var toolboxBlock = SelectBestPythonToolboxBlock(classBlocks);
            if (toolboxBlock is null)
            {
                return new ToolboxCatalog
                {
                    ToolboxPath = toolboxPath,
                    DisplayName = Path.GetFileNameWithoutExtension(toolboxPath)
                };
            }

            var orderedToolNames = ReadPythonToolboxToolNames(toolboxBlock.Value.Body);

            var catalog = new ToolboxCatalog
            {
                ToolboxPath = toolboxPath,
                DisplayName = ReadPythonStringAssignment(toolboxBlock.Value.Body, "label") ?? Path.GetFileNameWithoutExtension(toolboxPath),
                Alias = ReadPythonStringAssignment(toolboxBlock.Value.Body, "alias") ?? string.Empty
            };

            var candidateBlocks = orderedToolNames.Count > 0
                ? orderedToolNames
                    .SelectMany(name => classBlocks.Where(block => block.Name.Equals(name, StringComparison.Ordinal)).Take(1))
                    .ToList()
                : classBlocks
                    .Where(block => !block.Name.Equals("Toolbox", StringComparison.OrdinalIgnoreCase))
                    .Where(block => block.Body.Contains("def execute", StringComparison.Ordinal) || block.Body.Contains("self.label", StringComparison.Ordinal))
                    .ToList();

            foreach (var group in candidateBlocks.GroupBy(block => ReadPythonStringAssignment(block.Body, "category") ?? DefaultToolsetName))
            {
                var tools = group
                    .Select(block => new ToolboxTool
                    {
                        Name = block.Name,
                        Caption = ReadPythonStringAssignment(block.Body, "label") ?? block.Name,
                        ToolPath = Path.Combine(toolboxPath, block.Name),
                        SourceToolPath = block.Name
                    })
                    .ToList();

                AddToolsetPath(catalog.Toolsets, group.Key, tools);
            }

            return catalog;
        }

        private static ToolboxCatalog LoadToolboxWithArcPyDescribe(string toolboxPath)
        {
            var catalog = new ToolboxCatalog
            {
                ToolboxPath = toolboxPath,
                DisplayName = Path.GetFileNameWithoutExtension(toolboxPath)
            };

            var pythonExe = GetArcGisProPythonPath();
            if (string.IsNullOrWhiteSpace(pythonExe) || !File.Exists(pythonExe))
                return catalog;

            var scriptPath = Path.Combine(Path.GetTempPath(), $"gis_toolbox_inspect_{Guid.NewGuid():N}.py");
            File.WriteAllText(scriptPath, """
import arcpy
import html
import json
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

path = sys.argv[1]
root = arcpy.Describe(path)

def read_text(obj, *names):
    for name in names:
        try:
            value = getattr(obj, name)
            if value is not None and str(value).strip():
                return str(value)
        except Exception:
            pass
    return ""

def child_count(obj):
    try:
        children = getattr(obj, "children", None) or []
        return len(children)
    except Exception:
        return 0

def relative_source(catalog_path, fallback):
    if catalog_path and catalog_path.lower().startswith(path.lower()):
        value = catalog_path[len(path):].lstrip("\\/")
        if value:
            return value
    return fallback

def looks_like_toolbox_path(value):
    if not value:
        return False
    bad_terms = ("输入", "输出", "值为空", "数据类型", "工作空间", "要素图层", "镶嵌数据集")
    if any(term in value for term in bad_terms):
        return False
    bad_prefixes = ("GP", "DE", "esri", "OBJECTID", "PATH", "NAME", "Workspace")
    if value.startswith(bad_prefixes):
        return False
    return "\\" in value or "/" in value or bool(re.search(r"[\u4e00-\u9fff]", value))

def read_legacy_tbx_metadata(toolbox_path):
    if not toolbox_path.lower().endswith(".tbx"):
        return {"displayName": "", "tools": []}

    try:
        import olefile
    except Exception:
        return {"displayName": "", "tools": []}

    try:
        ole = olefile.OleFileIO(toolbox_path)
    except Exception:
        return {"displayName": "", "tools": []}

    try:
        toolbox_display_name = ""
        known_toolsets = set()
        try:
            contents = ole.openstream("Contents").read().decode("utf-8", errors="ignore")
            toolbox_match = re.search(r"<toolbox\s+name=\"([^\"]+)\"", contents)
            if toolbox_match:
                toolbox_display_name = html.unescape(toolbox_match.group(1)).strip()
            for name in re.findall(r"<toolset\s+name=\"([^\"]+)\"", contents):
                value = html.unescape(name).strip()
                if value:
                    known_toolsets.add(value)
        except Exception:
            pass

        stream_names = []
        for entry in ole.listdir(streams=True, storages=False):
            stream_name = "/".join(entry)
            if re.fullmatch(r"Tool\d+", stream_name):
                stream_names.append(stream_name)

        def stream_index(stream_name):
            return int(stream_name[4:])

        metadata = []
        for stream_name in sorted(stream_names, key=stream_index):
            try:
                data = ole.openstream(stream_name).read()
            except Exception:
                continue

            utf8_text = data.decode("utf-8", errors="ignore")
            utf16_text = data.decode("utf-16le", errors="ignore")
            xml_name = ""
            xml_caption = ""
            tag_match = re.search(r"<tool\s+[^>]*>", utf8_text)
            if tag_match:
                tag = tag_match.group(0)
                name_match = re.search(r"\bname=\"([^\"]*)\"", tag)
                caption_match = re.search(r"\bdisplayname=\"([^\"]*)\"", tag)
                if name_match:
                    xml_name = html.unescape(name_match.group(1)).strip()
                if caption_match:
                    xml_caption = html.unescape(caption_match.group(1)).strip()

            runs = [
                item.strip()
                for item in re.findall(r"[\u4e00-\u9fffA-Za-z0-9_\\/（）()\\-]{3,}", utf16_text)
                if item.strip()
            ]

            category = ""
            for item in runs[:12]:
                if item in known_toolsets:
                    category = item
                    break

            if not category:
                for item in runs[1:8]:
                    if item != xml_caption and item != xml_name and looks_like_toolbox_path(item):
                        category = item
                        break

            caption = xml_caption or (runs[0] if runs else xml_name)
            metadata.append({
                "internalName": xml_name,
                "caption": caption,
                "category": category
            })

        return {"displayName": toolbox_display_name, "tools": metadata}
    finally:
        try:
            ole.close()
        except Exception:
            pass

tools = []
legacy_metadata = read_legacy_tbx_metadata(path)
legacy_tools = legacy_metadata.get("tools", [])

def visit(node, groups):
    try:
        children = getattr(node, "children", None) or []
    except Exception:
        children = []

    for child in children:
        data_type = read_text(child, "dataType").lower()
        name = read_text(child, "name", "baseName")
        caption = read_text(child, "displayName", "label", "name", "baseName")
        catalog_path = read_text(child, "catalogPath")
        source = relative_source(catalog_path, name)

        if data_type == "tool" or child_count(child) == 0:
            if name:
                legacy = legacy_tools[len(tools)] if len(tools) < len(legacy_tools) else {}
                legacy_caption = legacy.get("caption") if legacy else ""
                legacy_category = legacy.get("category") if legacy else ""
                tools.append({
                    "name": name,
                    "caption": legacy_caption or caption or name,
                    "source": source or name,
                    "category": legacy_category or "\\".join(groups)
                })
        else:
            group_name = caption or name
            visit(child, groups + ([group_name] if group_name else []))

visit(root, [])
print(json.dumps({
    "displayName": legacy_metadata.get("displayName") or read_text(root, "baseName", "name"),
    "alias": read_text(root, "aliasName", "alias"),
    "tools": tools
}, ensure_ascii=False))
""", Encoding.UTF8);

            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = $"\"{scriptPath}\" \"{toolboxPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                process.StartInfo.Environment["PYTHONIOENCODING"] = "utf-8";

                process.Start();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(120000))
                {
                    TryKill(process);
                    return catalog;
                }

                var output = outputTask.GetAwaiter().GetResult();
                _ = errorTask.GetAwaiter().GetResult();
                if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                    return catalog;

                var node = ParseJsonObjectFromProcessOutput(output);
                if (node is null)
                    return catalog;

                var displayName = node?["displayName"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(displayName))
                    catalog.DisplayName = displayName;

                var alias = node?["alias"]?.GetValue<string>() ?? string.Empty;
                catalog.Alias = alias;

                foreach (var toolNode in node?["tools"]?.AsArray() ?? new JsonArray())
                {
                    if (toolNode is not JsonObject toolObject)
                        continue;

                    var toolName = toolObject["name"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(toolName))
                        continue;

                    var caption = toolObject["caption"]?.GetValue<string>() ?? toolName;
                    var source = toolObject["source"]?.GetValue<string>() ?? toolName;
                    var toolsetPath = toolObject["category"]?.GetValue<string>() ?? DefaultToolsetName;
                    var toolEntry = new ToolboxTool
                    {
                        Name = toolName,
                        Caption = caption,
                        ToolPath = Path.Combine(toolboxPath, source),
                        SourceToolPath = source
                    };

                    AddToolsetPath(catalog.Toolsets, toolsetPath, new List<ToolboxTool> { toolEntry });
                }
            }
            finally
            {
                TryDelete(scriptPath);
            }

            return catalog;
        }

        private static string? GetArcGisProPythonPath()
        {
            var frameworkLocation = typeof(FrameworkApplication).Assembly.Location;
            var proBin = Path.GetDirectoryName(frameworkLocation);
            if (string.IsNullOrWhiteSpace(proBin))
                return null;

            return Path.Combine(proBin, "Python", "envs", "arcgispro-py3", "python.exe");
        }

        private static JsonObject? ReadJsonObject(ZipArchive archive, string entryName)
        {
            var text = ReadZipText(archive, entryName);
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text) as JsonObject;
        }

        private static Dictionary<string, string> ReadResourceMap(ZipArchive archive, string entryName)
        {
            var node = ReadJsonObject(archive, entryName);
            var map = node?["map"] as JsonObject;
            if (map is null)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return map
                .Where(item => item.Value is not null)
                .ToDictionary(item => item.Key, item => item.Value!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
        }

        private static string? ReadZipText(ZipArchive archive, string entryName)
        {
            var entry = archive.GetEntry(entryName);
            if (entry is null)
                return null;

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        private static string ResolveToolsetName(string rawName, IReadOnlyDictionary<string, string> resources)
        {
            if (rawName.Equals("<root>", StringComparison.OrdinalIgnoreCase))
                return DefaultToolsetName;

            return ResolveResource(rawName, resources) ?? rawName;
        }

        private static void AddToolsetPath(List<ToolboxToolset> rootToolsets, string rawPath, List<ToolboxTool> tools)
        {
            if (tools.Count == 0)
                return;

            var parts = SplitToolsetPath(rawPath).ToList();
            if (parts.Count == 0)
                parts.Add(DefaultToolsetName);

            var currentLevel = rootToolsets;
            ToolboxToolset? current = null;

            foreach (var part in parts)
            {
                current = currentLevel.FirstOrDefault(toolset => toolset.Name.Equals(part, StringComparison.OrdinalIgnoreCase));
                if (current is null)
                {
                    current = new ToolboxToolset { Name = part };
                    currentLevel.Add(current);
                }

                currentLevel = current.Children;
            }

            current?.Tools.AddRange(tools);
        }

        private static IEnumerable<string> SplitToolsetPath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                yield return DefaultToolsetName;
                yield break;
            }

            foreach (var part in rawPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return string.IsNullOrWhiteSpace(part) ? DefaultToolsetName : part;
        }

        private static JsonObject? ParseJsonObjectFromProcessOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return null;

            try
            {
                return JsonNode.Parse(output) as JsonObject;
            }
            catch
            {
            }

            var lines = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Reverse();

            foreach (var line in lines)
            {
                try
                {
                    return JsonNode.Parse(line) as JsonObject;
                }
                catch
                {
                }
            }

            return null;
        }

        private static string? ResolveResource(string? rawValue, IReadOnlyDictionary<string, string> resources)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
                return null;

            const string resourcePrefix = "$rc:";
            if (rawValue.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var key = rawValue[resourcePrefix.Length..];
                if (resources.TryGetValue(key, out var value))
                    return value;
            }

            return rawValue;
        }

        private static IEnumerable<string> ReadToolNames(JsonNode? toolsetNode)
        {
            var tools = toolsetNode?["tools"] as JsonArray;
            if (tools is null)
                return Enumerable.Empty<string>();

            return tools
                .Select(item => item?.GetValue<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))!;
        }

        private static string ReadAtbxToolCaption(ZipArchive archive, string toolName)
        {
            var resourceMap = ReadResourceMap(archive, $"{toolName}.tool/tool.content.rc");
            if (resourceMap.TryGetValue("title", out var title) && !string.IsNullOrWhiteSpace(title))
                return title;

            var content = ReadJsonObject(archive, $"{toolName}.tool/tool.content");
            var resolved = ResolveResource(content?["displayname"]?.GetValue<string>(), resourceMap);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;

            var metadataText = ReadZipText(archive, $"{toolName}.tool/tool.metadata.xml");
            if (!string.IsNullOrWhiteSpace(metadataText))
            {
                try
                {
                    var document = XDocument.Parse(metadataText);
                    var resTitle = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "resTitle")?.Value;
                    if (!string.IsNullOrWhiteSpace(resTitle))
                        return resTitle;
                }
                catch
                {
                    return toolName;
                }
            }

            return toolName;
        }

        private static List<PythonClassBlock> ReadPythonClassBlocks(string source)
        {
            var matches = Regex.Matches(
                source,
                $@"(?ms)^class\s+(?<name>{PythonIdentifierPattern})\s*(?:\([^)]*\))?\s*:\s*(?<body>.*?)(?=^class\s+{PythonIdentifierPattern}\s*(?:\([^)]*\))?\s*:|\z)");

            return matches
                .Select(match => new PythonClassBlock(match.Groups["name"].Value, match.Groups["body"].Value))
                .ToList();
        }

        private static PythonClassBlock? SelectBestPythonToolboxBlock(IEnumerable<PythonClassBlock> classBlocks)
        {
            var candidates = classBlocks
                .Where(block => block.Name.Equals("Toolbox", StringComparison.OrdinalIgnoreCase))
                .Select(block => new
                {
                    Block = block,
                    Score = ScorePythonToolboxBlock(block, classBlocks)
                })
                .OrderByDescending(item => item.Score)
                .ThenByDescending(item => item.Block.Body.Length)
                .ToList();

            return candidates.Count > 0 ? candidates[0].Block : null;
        }

        private static int ScorePythonToolboxBlock(PythonClassBlock toolboxBlock, IEnumerable<PythonClassBlock> classBlocks)
        {
            var toolNames = ReadPythonToolboxToolNames(toolboxBlock.Body);
            if (toolNames.Count == 0)
                return 0;

            var toolClassNames = new HashSet<string>(classBlocks.Select(block => block.Name), StringComparer.Ordinal);
            var matchedTools = toolNames.Count(toolName => toolClassNames.Contains(toolName));
            var score = matchedTools * 1000 + toolNames.Count * 10;

            if (!string.IsNullOrWhiteSpace(ReadPythonStringAssignment(toolboxBlock.Body, "label")))
                score += 2;

            if (!string.IsNullOrWhiteSpace(ReadPythonStringAssignment(toolboxBlock.Body, "alias")))
                score += 1;

            return score;
        }

        private static (string ToolsetPath, string ToolName) SplitArcPyToolName(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                return (DefaultToolsetName, string.Empty);

            var normalized = toolName.Replace('/', '\\');
            var separatorIndex = normalized.LastIndexOf('\\');
            if (separatorIndex > 0 && separatorIndex < normalized.Length - 1)
                return (normalized[..separatorIndex], normalized[(separatorIndex + 1)..]);

            return (DefaultToolsetName, toolName);
        }

        private static List<string> ReadPythonToolboxToolNames(string toolboxBody)
        {
            if (string.IsNullOrWhiteSpace(toolboxBody))
                return new List<string>();

            var match = Regex.Match(toolboxBody, @"self\.tools\s*=\s*\[(?<tools>.*?)\]", RegexOptions.Singleline);
            if (!match.Success)
                return new List<string>();

            return Regex.Matches(match.Groups["tools"].Value, PythonIdentifierPattern)
                .Select(item => item.Value)
                .Where(name => !name.Equals("self", StringComparison.OrdinalIgnoreCase))
                .Where(name => !name.Equals("None", StringComparison.OrdinalIgnoreCase))
                .Where(name => !name.Equals("True", StringComparison.OrdinalIgnoreCase))
                .Where(name => !name.Equals("False", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static string? ReadPythonStringAssignment(string body, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(body))
                return null;

            var match = Regex.Match(
                body,
                $@"self\.{Regex.Escape(propertyName)}\s*=\s*(?<quote>[""'])(?<value>.*?)(\k<quote>)",
                RegexOptions.Singleline);

            return match.Success ? match.Groups["value"].Value : null;
        }

        private static void RemoveEmptyToolsets(ToolboxCatalog catalog)
        {
            catalog.Toolsets = RemoveEmptyToolsets(catalog.Toolsets);

            if (catalog.Toolsets.Count == 1 && string.IsNullOrWhiteSpace(catalog.Toolsets[0].Name))
                catalog.Toolsets[0].Name = DefaultToolsetName;
        }

        private static List<ToolboxToolset> RemoveEmptyToolsets(IEnumerable<ToolboxToolset> toolsets)
        {
            var result = new List<ToolboxToolset>();

            foreach (var toolset in toolsets)
            {
                toolset.Children = RemoveEmptyToolsets(toolset.Children);
                if (toolset.Tools.Count > 0 || toolset.Children.Count > 0)
                    result.Add(toolset);
            }

            return result;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        private readonly record struct PythonClassBlock(string Name, string Body);

        private const string PythonIdentifierPattern = @"[\p{L}_][\p{L}\p{Mn}\p{Nd}_]*";
    }
}

