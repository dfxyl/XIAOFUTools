#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Infrastructure
{
    internal static class ToolboxCustomCatalogService
    {
        public const string CustomCatalogKind = ToolboxCatalogRules.CustomCatalogKind;
        public const string ToolboxCatalogKind = "Toolbox";
        public const string SystemToolKind = "System";
        public const string CommandToolKind = "Command";
        public const string MapToolKind = "MapTool";
        public const string ToolboxToolKind = "Toolbox";

        private const string CustomCatalogDirectoryName = "Custom";
        private const string CustomCatalogExtension = ".giscustom";

        public static ToolboxCatalog CreateCatalog(string displayName)
        {
            Directory.CreateDirectory(CustomCatalogDirectory);

            return new ToolboxCatalog
            {
                CatalogKind = CustomCatalogKind,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "自定义组" : displayName.Trim(),
                ToolboxPath = Path.Combine(CustomCatalogDirectory, $"{Guid.NewGuid():N}{CustomCatalogExtension}"),
                MenuMode = "Inherit",
                Toolsets = new List<ToolboxToolset>()
            };
        }

        public static bool IsCustomCatalog(ToolboxCatalog? catalog)
        {
            return ToolboxCatalogRules.IsCustomCatalog(catalog);
        }

        public static bool IsSystemTool(ToolboxTool? tool)
        {
            return tool is not null &&
                   tool.ToolKind.Equals(SystemToolKind, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCommandTool(ToolboxTool? tool)
        {
            return tool is not null &&
                   tool.ToolKind.Equals(CommandToolKind, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsMapTool(ToolboxTool? tool)
        {
            return tool is not null &&
                   tool.ToolKind.Equals(MapToolKind, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDefaultToolsetName(string? name)
        {
            return string.IsNullOrWhiteSpace(name) ||
                   name.Equals("工具", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("Tools", StringComparison.OrdinalIgnoreCase) ||
                   name.Equals("默认工具集", StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildSystemToolPath(string toolboxAlias, string toolName)
        {
            return $"{toolboxAlias.Trim()}.{toolName.Trim()}";
        }

        public static void NormalizeCatalog(ToolboxCatalog catalog)
        {
            catalog.CatalogKind = NormalizeCatalogKind(catalog.CatalogKind, catalog.ToolboxPath);
            catalog.MenuMode = NormalizeCatalogMenuMode(catalog.MenuMode);
            catalog.Toolsets ??= new List<ToolboxToolset>();

            foreach (var toolset in catalog.Toolsets)
                NormalizeToolset(toolset);
        }

        private static string CustomCatalogDirectory => Path.Combine(ToolboxInternalStorageService.StorageRoot, CustomCatalogDirectoryName);

        private static void NormalizeToolset(ToolboxToolset toolset)
        {
            toolset.Name ??= string.Empty;
            toolset.Children ??= new List<ToolboxToolset>();
            toolset.Tools ??= new List<ToolboxTool>();

            foreach (var child in toolset.Children)
                NormalizeToolset(child);

            foreach (var tool in toolset.Tools)
            {
                tool.Name ??= string.Empty;
                tool.Caption ??= string.Empty;
                tool.ToolPath ??= string.Empty;
                tool.SourceToolPath ??= string.Empty;
                tool.ToolKind = NormalizeToolKind(tool.ToolKind, tool.ToolPath);
            }
        }

        private static string NormalizeCatalogKind(string? catalogKind, string? toolboxPath)
        {
            if (!string.IsNullOrWhiteSpace(catalogKind))
            {
                return catalogKind.Equals(CustomCatalogKind, StringComparison.OrdinalIgnoreCase)
                    ? CustomCatalogKind
                    : ToolboxCatalogKind;
            }

            return IsCustomPath(toolboxPath) ? CustomCatalogKind : ToolboxCatalogKind;
        }

        private static string NormalizeToolKind(string? toolKind, string? toolPath)
        {
            if (!string.IsNullOrWhiteSpace(toolKind))
            {
                return toolKind.Equals(SystemToolKind, StringComparison.OrdinalIgnoreCase)
                    ? SystemToolKind
                    : toolKind.Equals(CommandToolKind, StringComparison.OrdinalIgnoreCase)
                        ? CommandToolKind
                        : toolKind.Equals(MapToolKind, StringComparison.OrdinalIgnoreCase)
                            ? MapToolKind
                    : ToolboxToolKind;
            }

            if (!string.IsNullOrWhiteSpace(toolPath) &&
                !Path.IsPathRooted(toolPath) &&
                toolPath.Contains('.', StringComparison.Ordinal))
            {
                return SystemToolKind;
            }

            return ToolboxToolKind;
        }

        private static string NormalizeCatalogMenuMode(string? menuMode)
        {
            return menuMode switch
            {
                "Auto" => "Auto",
                "Toolbox" => "Toolbox",
                "Toolset" => "Toolset",
                "Tree" => "Tree",
                "Flat" => "Flat",
                _ => "Inherit"
            };
        }

        private static bool IsCustomPath(string? toolboxPath)
        {
            if (string.IsNullOrWhiteSpace(toolboxPath))
                return false;

            var extension = Path.GetExtension(toolboxPath);
            return extension.Equals(CustomCatalogExtension, StringComparison.OrdinalIgnoreCase);
        }
    }
}
