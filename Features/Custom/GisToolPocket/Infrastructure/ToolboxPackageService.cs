#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;

using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Infrastructure
{
    internal static class ToolboxPackageService
    {
        private const string PackageManifestFileName = "package.manifest.json";
        private const string PackageToolboxesDirectoryName = "toolboxes";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public static void Export(IReadOnlyCollection<ToolboxCatalog> catalogs, string defaultPackageName, string? targetPackagePath = null)
        {
            Export(catalogs, "Auto", defaultPackageName, targetPackagePath);
        }

        public static void Export(IReadOnlyCollection<ToolboxCatalog> catalogs, string menuMode, string defaultPackageName, string? targetPackagePath = null)
        {
            if (catalogs.Count == 0)
                throw new InvalidOperationException("没有可导出的工具箱。");

            var packagePath = ResolveExportPath(targetPackagePath, defaultPackageName);
            var stagingRoot = Path.Combine(Path.GetTempPath(), $"gis_toolbox_package_{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingRoot);

            try
            {
                var manifest = new ToolboxPackageManifest
                {
                    PackageKind = "AddInToolboxPackage",
                    PackageName = defaultPackageName,
                    CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
                    MenuMode = NormalizeMenuMode(menuMode)
                };

                for (var i = 0; i < catalogs.Count; i++)
                {
                    var catalog = catalogs.ElementAt(i);
                    var sourcePath = ResolveSourcePath(catalog);
                    var isCustomCatalog = ToolboxCustomCatalogService.IsCustomCatalog(catalog);
                    if (!isCustomCatalog && (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)))
                        throw new FileNotFoundException($"工具箱源文件不存在：{catalog.DisplayTitle}", sourcePath);

                    var entryRootName = $"{i + 1:00}_{SanitizeFileName(catalog.DisplayTitle)}";
                    var entryRelativePath = isCustomCatalog
                        ? string.Empty
                        : BuildPackageRelativePath(entryRootName, sourcePath!);

                    if (!isCustomCatalog)
                    {
                        var destinationPath = Path.Combine(stagingRoot, entryRelativePath);
                        CopySourceToPackage(sourcePath!, destinationPath);
                    }

                    manifest.Toolboxes.Add(new ToolboxPackageEntry
                    {
                        RelativePath = entryRelativePath.Replace('\\', '/'),
                        OriginalPath = string.IsNullOrWhiteSpace(catalog.OriginalToolboxPath) ? catalog.ToolboxPath : catalog.OriginalToolboxPath,
                        DisplayName = catalog.DisplayTitle,
                        Type = isCustomCatalog ? ToolboxCustomCatalogService.CustomCatalogKind : Path.GetExtension(sourcePath!).Trim('.').ToLowerInvariant(),
                        CatalogKind = catalog.CatalogKind,
                        MenuMode = catalog.MenuMode,
                        Alias = catalog.Alias,
                        IsVisible = catalog.IsVisible,
                        Toolsets = CloneToolsets(catalog.Toolsets)
                    });
                }

                File.WriteAllText(Path.Combine(stagingRoot, PackageManifestFileName), JsonSerializer.Serialize(manifest, JsonOptions), Encoding.UTF8);

                if (File.Exists(packagePath))
                    File.Delete(packagePath);

                ZipFile.CreateFromDirectory(stagingRoot, packagePath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
            finally
            {
                TryDeleteDirectory(stagingRoot);
            }
        }

        public static List<ToolboxCatalog> Import(string packagePath)
        {
            return ImportPackage(packagePath).Catalogs;
        }

        public static ToolboxPackageImportResult ImportPackage(string packagePath)
        {
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("包路径为空。", nameof(packagePath));

            if (!File.Exists(packagePath))
                throw new FileNotFoundException("包文件不存在。", packagePath);

            var extractRoot = Path.Combine(ToolboxInternalStorageService.StorageRoot, "Packages", ShortHash(packagePath));
            ResetDirectory(extractRoot);

            ZipFile.ExtractToDirectory(packagePath, extractRoot, overwriteFiles: true);

            var manifestPath = Path.Combine(extractRoot, PackageManifestFileName);
            if (!File.Exists(manifestPath))
                throw new InvalidOperationException("压缩包缺少 package.manifest.json。");

            var manifest = JsonSerializer.Deserialize<ToolboxPackageManifest>(File.ReadAllText(manifestPath, Encoding.UTF8), JsonOptions)
                ?? throw new InvalidOperationException("压缩包清单解析失败。");

            var result = new ToolboxPackageImportResult
            {
                PackageName = manifest.PackageName,
                MenuMode = NormalizeMenuMode(manifest.MenuMode),
                Manifest = manifest
            };

            foreach (var entry in manifest.Toolboxes)
            {
                ToolboxCatalog catalog;
                if (entry.CatalogKind.Equals(ToolboxCustomCatalogService.CustomCatalogKind, StringComparison.OrdinalIgnoreCase))
                {
                    catalog = ToolboxCustomCatalogService.CreateCatalog(entry.DisplayName);
                }
                else
                {
                    var toolboxPath = Path.Combine(extractRoot, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(toolboxPath))
                        throw new FileNotFoundException($"包内工具箱不存在：{entry.RelativePath}", toolboxPath);

                    catalog = TryLoadCatalogFromPackageEntry(toolboxPath, entry);
                }

                catalog.DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? catalog.DisplayName : entry.DisplayName;
                catalog.OriginalToolboxPath = entry.OriginalPath;
                catalog.CatalogKind = entry.CatalogKind;
                catalog.MenuMode = string.IsNullOrWhiteSpace(entry.MenuMode) ? "Inherit" : entry.MenuMode;
                if (string.IsNullOrWhiteSpace(catalog.Alias))
                    catalog.Alias = entry.Alias;

                if (entry.Toolsets.Count > 0)
                    catalog.Toolsets = RebaseToolsets(entry.Toolsets, catalog.ToolboxPath);

                catalog.PackageManifest = manifest;
                result.Catalogs.Add(catalog);
            }

            return result;
        }

        public static string? ResolveSourcePath(ToolboxCatalog catalog)
        {
            if (catalog is null)
                return null;

            if (!string.IsNullOrWhiteSpace(catalog.OriginalToolboxPath) && File.Exists(catalog.OriginalToolboxPath))
                return catalog.OriginalToolboxPath;

            return string.IsNullOrWhiteSpace(catalog.ToolboxPath) ? null : catalog.ToolboxPath;
        }

        private static string ResolveExportPath(string? targetPackagePath, string defaultPackageName)
        {
            if (!string.IsNullOrWhiteSpace(targetPackagePath))
                return targetPackagePath;

            var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var fileName = $"{SanitizeFileName(defaultPackageName)}_{DateTime.Now:yyyyMMddHHmmss}.zip";
            return Path.Combine(folder, fileName);
        }

        private static string BuildPackageRelativePath(string entryRootName, string sourcePath)
        {
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            return extension switch
            {
                ".pyt" => Path.Combine(PackageToolboxesDirectoryName, entryRootName, Path.GetFileName(sourcePath)),
                ".tbx" => Path.Combine(PackageToolboxesDirectoryName, entryRootName, Path.GetFileName(sourcePath)),
                ".atbx" => Path.Combine(PackageToolboxesDirectoryName, entryRootName, Path.GetFileName(sourcePath)),
                _ => Path.Combine(PackageToolboxesDirectoryName, entryRootName, Path.GetFileName(sourcePath))
            };
        }

        private static void CopySourceToPackage(string sourcePath, string destinationPath)
        {
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (extension == ".pyt" && Directory.Exists(Path.GetDirectoryName(sourcePath)))
            {
                var destinationDirectory = Path.GetDirectoryName(destinationPath)!;
                CopyPythonToolboxBundle(sourcePath, destinationDirectory);
                return;
            }

            var parent = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);

            CopyFileReplacing(sourcePath, destinationPath);
        }

        private static ToolboxCatalog TryLoadCatalogFromPackageEntry(string toolboxPath, ToolboxPackageEntry entry)
        {
            try
            {
                return ToolboxCatalogService.Load(toolboxPath);
            }
            catch when (entry.Toolsets.Count > 0)
            {
                return new ToolboxCatalog
                {
                    ToolboxPath = toolboxPath,
                    DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                        ? Path.GetFileNameWithoutExtension(toolboxPath)
                        : entry.DisplayName,
                    Alias = entry.Alias,
                    IsVisible = entry.IsVisible,
                    Toolsets = RebaseToolsets(entry.Toolsets, toolboxPath)
                };
            }
        }

        private static void CopyDirectory(string sourceDirectory, string targetDirectory)
        {
            Directory.CreateDirectory(targetDirectory);

            foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
            }

            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                var targetPath = Path.Combine(targetDirectory, relativePath);
                var parent = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                CopyFileReplacing(file, targetPath);
            }
        }

        private static void CopyPythonToolboxBundle(string sourcePath, string targetDirectory)
        {
            var sourceDirectory = Path.GetDirectoryName(sourcePath)!;
            var mainToolboxPath = Path.GetFullPath(sourcePath);

            Directory.CreateDirectory(targetDirectory);

            foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                if (ShouldSkipPythonBundleDirectory(directory))
                    continue;

                var relativePath = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
            }

            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                if (!ShouldCopyPythonBundleFile(file, mainToolboxPath))
                    continue;

                var relativePath = Path.GetRelativePath(sourceDirectory, file);
                var targetPath = Path.Combine(targetDirectory, relativePath);
                var parent = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrWhiteSpace(parent))
                    Directory.CreateDirectory(parent);

                CopyFileReplacing(file, targetPath);
            }
        }

        private static bool ShouldCopyPythonBundleFile(string filePath, string mainToolboxPath)
        {
            if (IsUnderSkippedPythonBundleDirectory(filePath))
                return false;

            var fullPath = Path.GetFullPath(filePath);
            if (fullPath.Equals(mainToolboxPath, StringComparison.OrdinalIgnoreCase))
                return true;

            var extension = Path.GetExtension(fullPath);
            return !extension.Equals(".pyt", StringComparison.OrdinalIgnoreCase) &&
                   !extension.Equals(".tbx", StringComparison.OrdinalIgnoreCase) &&
                   !extension.Equals(".atbx", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipPythonBundleDirectory(string directoryPath)
        {
            var name = Path.GetFileName(directoryPath);
            return name.Equals("__pycache__", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("obj", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsUnderSkippedPythonBundleDirectory(string filePath)
        {
            var current = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (ShouldSkipPythonBundleDirectory(current))
                    return true;

                current = Path.GetDirectoryName(current);
            }

            return false;
        }

        private static List<ToolboxToolset> CloneToolsets(IEnumerable<ToolboxToolset> toolsets)
        {
            return toolsets.Select(CloneToolset).ToList();
        }

        private static ToolboxToolset CloneToolset(ToolboxToolset toolset)
        {
            return new ToolboxToolset
            {
                Name = toolset.Name,
                IsVisible = toolset.IsVisible,
                Children = toolset.Children.Select(CloneToolset).ToList(),
                Tools = toolset.Tools
                    .Select(tool => new ToolboxTool
                    {
                        Name = tool.Name,
                        Caption = tool.Caption,
                        ToolPath = tool.ToolPath,
                        SourceToolPath = tool.SourceToolPath,
                        ToolKind = tool.ToolKind,
                        IsVisible = tool.IsVisible
                    })
                    .ToList()
            };
        }

        private static List<ToolboxToolset> RebaseToolsets(IEnumerable<ToolboxToolset> toolsets, string toolboxPath)
        {
            return toolsets.Select(toolset => RebaseToolset(toolset, toolboxPath)).ToList();
        }

        private static ToolboxToolset RebaseToolset(ToolboxToolset toolset, string toolboxPath)
        {
            return new ToolboxToolset
            {
                Name = toolset.Name,
                IsVisible = toolset.IsVisible,
                Children = toolset.Children.Select(child => RebaseToolset(child, toolboxPath)).ToList(),
                Tools = toolset.Tools
                    .Select(tool => new ToolboxTool
                    {
                        Name = tool.Name,
                        Caption = tool.Caption,
                        ToolKind = tool.ToolKind,
                        IsVisible = tool.IsVisible,
                        SourceToolPath = string.IsNullOrWhiteSpace(tool.SourceToolPath) ? tool.Name : tool.SourceToolPath,
                        ToolPath = ToolboxCustomCatalogService.IsSystemTool(tool) ||
                                   ToolboxCustomCatalogService.IsCommandTool(tool) ||
                                   ToolboxCustomCatalogService.IsMapTool(tool)
                            ? (string.IsNullOrWhiteSpace(tool.SourceToolPath) ? tool.ToolPath : tool.SourceToolPath)
                            : RebaseToolPath(toolboxPath, string.IsNullOrWhiteSpace(tool.SourceToolPath) ? tool.Name : tool.SourceToolPath)
                    })
                    .ToList()
            };
        }

        private static string RebaseToolPath(string toolboxPath, string sourceToolPath)
        {
            if (string.IsNullOrWhiteSpace(sourceToolPath))
                return toolboxPath;

            if (Path.IsPathRooted(sourceToolPath))
            {
                var suffix = ExtractToolboxRelativeSuffix(sourceToolPath);
                return string.IsNullOrWhiteSpace(suffix) ? sourceToolPath : Path.Combine(toolboxPath, suffix);
            }

            return Path.Combine(toolboxPath, sourceToolPath);
        }

        private static string ExtractToolboxRelativeSuffix(string sourceToolPath)
        {
            var normalized = sourceToolPath.Replace('/', '\\');
            var markers = new[] { ".tbx\\", ".atbx\\", ".pyt\\" };
            foreach (var marker in markers)
            {
                var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                    return normalized[(index + marker.Length)..];
            }

            return string.Empty;
        }

        private static void ResetDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                ClearReadOnlyAttributes(directory);
                Directory.Delete(directory, true);
            }

            Directory.CreateDirectory(directory);
        }

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    ClearReadOnlyAttributes(directory);
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
            }
        }

        private static void CopyFileReplacing(string sourcePath, string targetPath)
        {
            if (File.Exists(targetPath))
                File.SetAttributes(targetPath, FileAttributes.Normal);

            File.Copy(sourcePath, targetPath, true);
            File.SetAttributes(targetPath, File.GetAttributes(targetPath) & ~FileAttributes.ReadOnly);
        }

        private static void ClearReadOnlyAttributes(string directory)
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            foreach (var childDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories))
                File.SetAttributes(childDirectory, FileAttributes.Normal);

            File.SetAttributes(directory, FileAttributes.Normal);
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
                builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);

            return builder.Length == 0 ? "toolbox" : builder.ToString();
        }

        private static string ShortHash(string value)
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(value).ToLowerInvariant()));
            return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
        }

        private static string NormalizeMenuMode(string menuMode)
        {
            return menuMode switch
            {
                "Toolbox" => "Toolbox",
                "Toolset" => "Toolset",
                _ => "Auto"
            };
        }
    }

    internal sealed class ToolboxPackageImportResult
    {
        public string PackageName { get; set; } = string.Empty;

        public string MenuMode { get; set; } = "Auto";

        public ToolboxPackageManifest Manifest { get; set; } = new();

        public List<ToolboxCatalog> Catalogs { get; } = new();
    }
}

