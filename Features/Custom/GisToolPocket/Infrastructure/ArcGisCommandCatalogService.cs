#nullable enable

using ArcGIS.Desktop.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Infrastructure
{
    internal static class ArcGisCommandCatalogService
    {
        private const string LocaleFolderName = "zh-CN";
        private const string CacheDirectoryName = "CommandCatalog";
        private const string CacheFileName = "arcgis-command-catalog.json";
        private const string CacheSchemaVersion = "2";

        private static readonly string[] SupportedElementNames = { "button", "tool" };
        private static readonly Dictionary<string, string> BuiltInSourceDisplayNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Catalog"] = "目录",
            ["Core"] = "核心",
            ["Editing"] = "编辑",
            ["Geoprocessing"] = "地理处理",
            ["GeoProcessing"] = "地理处理",
            ["KnowledgeGraph"] = "知识图谱",
            ["Layout"] = "布局",
            ["Mapping"] = "地图",
            ["Metadata"] = "元数据",
            ["ParcelFabric"] = "宗地",
            ["Parcel"] = "宗地",
            ["SceneLayers"] = "场景图层",
            ["Sharing"] = "共享",
            ["Tasks"] = "任务",
            ["UtilityNetwork"] = "公用网络"
        };

        private static readonly Dictionary<string, string> GeoprocessingToolboxAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["3D Analyst Tools.tbx"] = "3d",
            ["Analysis Tools.tbx"] = "analysis",
            ["Cartography Tools.tbx"] = "cartography",
            ["Conversion Tools.tbx"] = "conversion",
            ["Crime Analysis and Safety Tools.tbx"] = "ca",
            ["Data Management Tools.tbx"] = "management",
            ["Data Reviewer Tools.tbx"] = "Reviewer",
            ["Defense Tools.tbx"] = "defense",
            ["Editing Tools.tbx"] = "edit",
            ["GeoAI Tools.tbx"] = "geoai",
            ["GeoAnalytics Desktop Tools.tbx"] = "gapro",
            ["GeoAnalytics Tools.tbx"] = "geoanalytics",
            ["Geocoding Tools.tbx"] = "geocoding",
            ["Geostatistical Analyst Tools.tbx"] = "ga",
            ["Image Analyst Tools.tbx"] = "ia",
            ["Indoor Positioning Tools.tbx"] = "indoorpositioning",
            ["Indoors Tools.tbx"] = "indoors",
            ["Intelligence Tools.tbx"] = "intelligence",
            ["Knowledge Graph Tools.tbx"] = "kg",
            ["Linear Referencing Tools.tbx"] = "lr",
            ["Location Referencing Tools.tbx"] = "locref",
            ["Model Tools.tbx"] = "mb",
            ["Multidimension Tools.tbx"] = "md",
            ["Network Analyst Tools.tbx"] = "na",
            ["Network Diagram Tools.tbx"] = "nd",
            ["Oriented Imagery Tools.tbx"] = "oi",
            ["Parcel Tools.tbx"] = "parcel",
            ["Public Transit Tools.tbx"] = "transit",
            ["Raster Analysis Tools.tbx"] = "ra",
            ["ReadyToUseServiceTools.tbx"] = "agolservices",
            ["Reality Mapping Tools.tbx"] = "rm",
            ["Server Tools.tbx"] = "server",
            ["Space Time Pattern Mining Tools.tbx"] = "stpm",
            ["Spatial Analyst Tools.tbx"] = "sa",
            ["Spatial Statistics Tools.tbx"] = "stats",
            ["Standard Feature Analysis Tools.tbx"] = "sfa",
            ["Trace Network Tools.tbx"] = "tn",
            ["Utility Network Tools.tbx"] = "un"
        };
        private static readonly object SyncRoot = new();
        private static string? _cachedSignature;
        private static List<ArcGisCommandDefinition>? _cachedCommands;

        public static List<ArcGisCommandDefinition> LoadCommands()
        {
            var descriptors = BuildDescriptors();
            var signature = BuildSignature(descriptors);

            lock (SyncRoot)
            {
                if (_cachedCommands is not null &&
                    string.Equals(_cachedSignature, signature, StringComparison.Ordinal))
                {
                    return Clone(_cachedCommands);
                }
            }

            var cached = TryLoadFromDiskCache(signature);
            if (cached is not null)
            {
                lock (SyncRoot)
                {
                    _cachedSignature = signature;
                    _cachedCommands = cached;
                }

                return Clone(cached);
            }

            var results = new Dictionary<string, ArcGisCommandDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var descriptor in descriptors)
            {
                try
                {
                    if (descriptor.SourceKind == ArcGisCommandSourceKind.BuiltIn)
                        ReadDamlFile(descriptor.BasePath, descriptor.LocalizedPath, descriptor.SourceName, results);
                    else
                        ReadAddInDaml(descriptor.BasePath, descriptor.SourceName, results);
                }
                catch
                {
                }
            }

            ReadBuiltInGeoprocessingTools(results);

            var commands = results.Values
                .Where(item => !string.IsNullOrWhiteSpace(item.Caption))
                .OrderBy(item => item.Source, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Caption, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            SaveToDiskCache(signature, commands);

            lock (SyncRoot)
            {
                _cachedSignature = signature;
                _cachedCommands = commands;
            }

            return Clone(commands);
        }

        private static List<ArcGisCommandFileDescriptor> BuildDescriptors()
        {
            var descriptors = new List<ArcGisCommandFileDescriptor>();
            descriptors.AddRange(EnumerateBuiltInDamlFiles());
            descriptors.AddRange(EnumerateInstalledAddIns());
            return descriptors
                .OrderBy(item => item.SourceName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.BasePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<ArcGisCommandFileDescriptor> EnumerateBuiltInDamlFiles()
        {
            var extensionRoot = ResolveExtensionRoot();
            if (string.IsNullOrWhiteSpace(extensionRoot) || !Directory.Exists(extensionRoot))
                yield break;

            foreach (var baseFile in Directory.EnumerateFiles(extensionRoot, "*.daml", SearchOption.AllDirectories))
            {
                if (baseFile.Contains($"{Path.DirectorySeparatorChar}{LocaleFolderName}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                    continue;

                var sourceName = BuildBuiltInSourceName(extensionRoot, baseFile);
                var localizedPath = Path.Combine(Path.GetDirectoryName(baseFile)!, LocaleFolderName, Path.GetFileName(baseFile));
                yield return new ArcGisCommandFileDescriptor(
                    baseFile,
                    File.Exists(localizedPath) ? localizedPath : null,
                    sourceName,
                    ArcGisCommandSourceKind.BuiltIn);
            }
        }

        private static IEnumerable<ArcGisCommandFileDescriptor> EnumerateInstalledAddIns()
        {
            var addInRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ArcGIS", "AddIns", "ArcGISPro");
            if (!Directory.Exists(addInRoot))
                yield break;

            foreach (var file in Directory.EnumerateFiles(addInRoot, "*.esriAddinX", SearchOption.AllDirectories))
            {
                yield return new ArcGisCommandFileDescriptor(
                    file,
                    null,
                    $"加载项 / {Path.GetFileNameWithoutExtension(file)}",
                    ArcGisCommandSourceKind.AddIn);
            }
        }

        private static string? ResolveExtensionRoot()
        {
            var binRoot = Path.GetDirectoryName(typeof(FrameworkApplication).Assembly.Location);
            if (string.IsNullOrWhiteSpace(binRoot))
                return null;

            return Path.Combine(binRoot, "Extensions");
        }

        private static string? ResolveProRoot()
        {
            var binRoot = Path.GetDirectoryName(typeof(FrameworkApplication).Assembly.Location);
            return string.IsNullOrWhiteSpace(binRoot) ? null : Path.GetFullPath(Path.Combine(binRoot, ".."));
        }

        private static string? ResolveGeoprocessingHelpRoot()
        {
            var proRoot = ResolveProRoot();
            if (string.IsNullOrWhiteSpace(proRoot))
                return null;

            return Path.Combine(proRoot, "Resources", "Help", LocaleFolderName, "gp", "Toolboxes");
        }

        private static string? ResolveToolChangesPath()
        {
            var proRoot = ResolveProRoot();
            if (string.IsNullOrWhiteSpace(proRoot))
                return null;

            return Path.Combine(proRoot, "Resources", "ArcToolBox", "Scripts", "toolChanges.xml");
        }

        private static string BuildBuiltInSourceName(string extensionRoot, string damlPath)
        {
            var directory = Path.GetDirectoryName(damlPath);
            if (string.IsNullOrWhiteSpace(directory))
                return "ArcGIS Pro";

            var relativeDirectory = Path.GetRelativePath(extensionRoot, directory);
            if (string.IsNullOrWhiteSpace(relativeDirectory) || relativeDirectory == ".")
                return "ArcGIS Pro";

            var firstSegment = relativeDirectory
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            if (string.IsNullOrWhiteSpace(firstSegment))
                return "ArcGIS Pro";

            var displayName = BuiltInSourceDisplayNames.TryGetValue(firstSegment, out var localizedName)
                ? localizedName
                : firstSegment;

            return $"ArcGIS Pro / {displayName}";
        }

        private static void ReadDamlFile(
            string basePath,
            string? localizedPath,
            string sourceName,
            IDictionary<string, ArcGisCommandDefinition> target)
        {
            var baseDocument = XDocument.Load(basePath);
            var localizedCaptions = !string.IsNullOrWhiteSpace(localizedPath) && File.Exists(localizedPath)
                ? BuildCaptionMap(XDocument.Load(localizedPath))
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            ReadCommandsFromDocument(baseDocument, localizedCaptions, sourceName, target);
        }

        private static void ReadAddInDaml(string addInPath, string sourceName, IDictionary<string, ArcGisCommandDefinition> target)
        {
            using var archive = ZipFile.OpenRead(addInPath);
            var entry = archive.Entries.FirstOrDefault(item => item.FullName.Equals("Config.daml", StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                return;

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            ReadCommandsFromDocument(document, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), sourceName, target);
        }

        private static Dictionary<string, string> BuildCaptionMap(XDocument document)
        {
            return document
                .Descendants()
                .Where(element => SupportedElementNames.Contains(element.Name.LocalName, StringComparer.OrdinalIgnoreCase))
                .Select(element => new
                {
                    Id = element.Attribute("id")?.Value,
                    Caption = NormalizeCaption(element.Attribute("caption")?.Value)
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Caption))
                .GroupBy(item => item.Id!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Last().Caption!, StringComparer.OrdinalIgnoreCase);
        }

        private static void ReadCommandsFromDocument(
            XDocument document,
            IReadOnlyDictionary<string, string> localizedCaptions,
            string sourceName,
            IDictionary<string, ArcGisCommandDefinition> target)
        {
            foreach (var element in document.Descendants().Where(item => SupportedElementNames.Contains(item.Name.LocalName, StringComparer.OrdinalIgnoreCase)))
            {
                var id = element.Attribute("id")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var commandKind = ResolveCommandKind(element.Name.LocalName);
                if (commandKind == ArcGisCommandKind.Unsupported)
                    continue;

                var className = element.Attribute("className")?.Value?.Trim() ?? string.Empty;
                var caption = localizedCaptions.TryGetValue(id, out var localizedCaption)
                    ? localizedCaption
                    : NormalizeCaption(element.Attribute("caption")?.Value) ?? id;

                target[id] = new ArcGisCommandDefinition
                {
                    Id = id,
                    Caption = caption,
                    ClassName = className,
                    Source = sourceName,
                    Kind = commandKind
                };
            }
        }

        private static void ReadBuiltInGeoprocessingTools(IDictionary<string, ArcGisCommandDefinition> target)
        {
            var helpRoot = ResolveGeoprocessingHelpRoot();
            if (string.IsNullOrWhiteSpace(helpRoot) || !Directory.Exists(helpRoot))
                return;

            var aliasHints = ReadToolAliasHints();
            foreach (var toolboxDirectory in Directory.EnumerateDirectories(helpRoot))
            {
                var toolboxName = Path.GetFileName(toolboxDirectory);
                if (string.IsNullOrWhiteSpace(toolboxName))
                    continue;

                var defaultAlias = ResolveToolboxAlias(toolboxName, toolboxDirectory, aliasHints);
                if (string.IsNullOrWhiteSpace(defaultAlias))
                    continue;

                var toolboxTitle = ReadResourceMap(Path.Combine(toolboxDirectory, "toolbox.content.rc"))
                    .TryGetValue("title", out var title) && !string.IsNullOrWhiteSpace(title)
                        ? title
                        : Path.GetFileNameWithoutExtension(toolboxName);

                var sourceName = $"ArcGIS Pro / 地理处理 / {toolboxTitle}";
                foreach (var toolDirectory in Directory.EnumerateDirectories(toolboxDirectory, "*.tool"))
                {
                    var toolName = Path.GetFileNameWithoutExtension(toolDirectory);
                    if (string.IsNullOrWhiteSpace(toolName))
                        continue;

                    var alias = ResolveToolAlias(toolName, defaultAlias, aliasHints);
                    if (string.IsNullOrWhiteSpace(alias))
                        continue;

                    var caption = ReadResourceMap(Path.Combine(toolDirectory, "tool.content.rc"))
                        .TryGetValue("title", out var toolTitle) && !string.IsNullOrWhiteSpace(toolTitle)
                            ? toolTitle
                            : toolName;

                    var id = $"{alias}.{toolName}";
                    target[id] = new ArcGisCommandDefinition
                    {
                        Id = id,
                        Caption = caption,
                        Source = sourceName,
                        Kind = ArcGisCommandKind.GeoprocessingTool
                    };
                }
            }
        }

        private static Dictionary<string, List<string>> ReadToolAliasHints()
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var toolChangesPath = ResolveToolChangesPath();
            if (string.IsNullOrWhiteSpace(toolChangesPath) || !File.Exists(toolChangesPath))
                return result;

            try
            {
                var document = XDocument.Load(toolChangesPath);
                foreach (var value in document.Descendants()
                             .Where(element => element.Name.LocalName is "ToolName" or "NewTool")
                             .Select(element => element.Value?.Trim())
                             .Where(value => !string.IsNullOrWhiteSpace(value)))
                {
                    var separatorIndex = value!.LastIndexOf('_');
                    if (separatorIndex <= 0 || separatorIndex >= value.Length - 1)
                        continue;

                    var toolName = value[..separatorIndex];
                    var alias = value[(separatorIndex + 1)..];
                    if (string.IsNullOrWhiteSpace(toolName) || string.IsNullOrWhiteSpace(alias))
                        continue;

                    if (!result.TryGetValue(toolName, out var aliases))
                    {
                        aliases = new List<string>();
                        result[toolName] = aliases;
                    }

                    if (!aliases.Contains(alias, StringComparer.OrdinalIgnoreCase))
                        aliases.Add(alias);
                }
            }
            catch
            {
            }

            return result;
        }

        private static string ResolveToolboxAlias(
            string toolboxName,
            string toolboxDirectory,
            IReadOnlyDictionary<string, List<string>> aliasHints)
        {
            if (GeoprocessingToolboxAliases.TryGetValue(toolboxName, out var alias))
                return alias;

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var toolDirectory in Directory.EnumerateDirectories(toolboxDirectory, "*.tool"))
            {
                var toolName = Path.GetFileNameWithoutExtension(toolDirectory);
                if (string.IsNullOrWhiteSpace(toolName) || !aliasHints.TryGetValue(toolName, out var aliases))
                    continue;

                foreach (var item in aliases)
                    counts[item] = counts.TryGetValue(item, out var count) ? count + 1 : 1;
            }

            return counts
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Key)
                .FirstOrDefault() ?? string.Empty;
        }

        private static string ResolveToolAlias(
            string toolName,
            string defaultAlias,
            IReadOnlyDictionary<string, List<string>> aliasHints)
        {
            if (aliasHints.TryGetValue(toolName, out var aliases) &&
                aliases.Any(alias => alias.Equals(defaultAlias, StringComparison.OrdinalIgnoreCase)))
            {
                return defaultAlias;
            }

            return defaultAlias;
        }

        private static Dictionary<string, string> ReadResourceMap(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                var node = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) as JsonObject;
                var map = node?["map"] as JsonObject;
                if (map is null)
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                return map
                    .Where(item => item.Value is not null)
                    .ToDictionary(item => item.Key, item => item.Value!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static ArcGisCommandKind ResolveCommandKind(string elementName)
        {
            if (elementName.Equals("tool", StringComparison.OrdinalIgnoreCase))
                return ArcGisCommandKind.MapTool;

            if (elementName.Equals("button", StringComparison.OrdinalIgnoreCase))
                return ArcGisCommandKind.Command;

            return ArcGisCommandKind.Unsupported;
        }

        private static string? NormalizeCaption(string? caption)
        {
            return string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        }

        private static string BuildSignature(IEnumerable<ArcGisCommandFileDescriptor> descriptors)
        {
            var builder = new StringBuilder();
            builder.Append("schema=").Append(CacheSchemaVersion).AppendLine();
            foreach (var descriptor in descriptors)
            {
                AppendFileSignature(builder, descriptor.BasePath);
                AppendFileSignature(builder, descriptor.LocalizedPath);
                builder.Append('|').Append(descriptor.SourceName).Append('|').Append(descriptor.SourceKind).AppendLine();
            }

            AppendDirectorySignature(builder, ResolveGeoprocessingHelpRoot(), "*content.rc");
            AppendFileSignature(builder, ResolveToolChangesPath());

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
            return Convert.ToHexString(bytes);
        }

        private static void AppendDirectorySignature(StringBuilder builder, string? directoryPath, string searchPattern)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                builder.Append("<missing-directory>").AppendLine();
                return;
            }

            foreach (var filePath in Directory.EnumerateFiles(directoryPath, searchPattern, SearchOption.AllDirectories)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                AppendFileSignature(builder, filePath);
            }
        }

        private static void AppendFileSignature(StringBuilder builder, string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                builder.Append("<missing>").AppendLine();
                return;
            }

            var fileInfo = new FileInfo(filePath);
            builder
                .Append(fileInfo.FullName)
                .Append('|')
                .Append(fileInfo.Length)
                .Append('|')
                .Append(fileInfo.LastWriteTimeUtc.Ticks)
                .AppendLine();
        }

        private static List<ArcGisCommandDefinition>? TryLoadFromDiskCache(string signature)
        {
            try
            {
                var cachePath = GetCachePath();
                if (!File.Exists(cachePath))
                    return null;

                var cache = JsonSerializer.Deserialize<ArcGisCommandCatalogCache>(File.ReadAllText(cachePath, Encoding.UTF8));
                if (cache is null || !string.Equals(cache.Signature, signature, StringComparison.Ordinal) || cache.Items.Count == 0)
                    return null;

                return cache.Items;
            }
            catch
            {
                return null;
            }
        }

        private static void SaveToDiskCache(string signature, List<ArcGisCommandDefinition> items)
        {
            try
            {
                var cachePath = GetCachePath();
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                var cache = new ArcGisCommandCatalogCache
                {
                    Signature = signature,
                    Items = items
                };

                File.WriteAllText(cachePath, JsonSerializer.Serialize(cache), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static string GetCachePath()
        {
            return Path.Combine(ToolboxInternalStorageService.StorageRoot, CacheDirectoryName, CacheFileName);
        }

        private static List<ArcGisCommandDefinition> Clone(IEnumerable<ArcGisCommandDefinition> items)
        {
            return items
                .Select(item => new ArcGisCommandDefinition
                {
                    Id = item.Id,
                    Caption = item.Caption,
                    ClassName = item.ClassName,
                    Source = item.Source,
                    Kind = item.Kind
                })
                .ToList();
        }
    }

    internal enum ArcGisCommandSourceKind
    {
        BuiltIn,
        AddIn
    }

    internal sealed class ArcGisCommandFileDescriptor
    {
        public ArcGisCommandFileDescriptor(string basePath, string? localizedPath, string sourceName, ArcGisCommandSourceKind sourceKind)
        {
            BasePath = basePath;
            LocalizedPath = localizedPath;
            SourceName = sourceName;
            SourceKind = sourceKind;
        }

        public string BasePath { get; }

        public string? LocalizedPath { get; }

        public string SourceName { get; }

        public ArcGisCommandSourceKind SourceKind { get; }
    }

    internal sealed class ArcGisCommandCatalogCache
    {
        public string Signature { get; set; } = string.Empty;

        public List<ArcGisCommandDefinition> Items { get; set; } = new();
    }

}
