using System.Collections.Generic;
using XIAOFUTools.Features.Custom.GisToolPocket.Core;
using XIAOFUTools.Features.Custom.GisToolPocket.Infrastructure;

namespace XIAOFUTools.Features.Custom.GisToolPocket.Application
{
    internal static class ToolboxApplicationService
    {
        public const string CommandToolKind = ToolboxCustomCatalogService.CommandToolKind;
        public const string MapToolKind = ToolboxCustomCatalogService.MapToolKind;
        public const string ToolboxToolKind = ToolboxCustomCatalogService.ToolboxToolKind;

        public static ToolboxConfiguration Load() => ToolboxConfigurationStore.Load();

        public static void Save(IEnumerable<ToolboxCatalog> catalogs, string menuMode) =>
            ToolboxConfigurationStore.Save(catalogs, menuMode);

        public static void SavePackage(IEnumerable<ToolboxCatalog> catalogs, string menuMode) =>
            ToolboxConfigurationStore.SavePackage(catalogs, menuMode);

        public static void ClearConfiguration() => ToolboxConfigurationStore.Clear();

        public static ToolboxCatalog CreateCatalog(string displayName) =>
            ToolboxCustomCatalogService.CreateCatalog(displayName);

        public static bool IsCustomCatalog(ToolboxCatalog? catalog) =>
            ToolboxCatalogRules.IsCustomCatalog(catalog);

        public static bool IsCommandTool(ToolboxTool? tool) =>
            ToolboxCustomCatalogService.IsCommandTool(tool);

        public static bool IsMapTool(ToolboxTool? tool) =>
            ToolboxCustomCatalogService.IsMapTool(tool);

        public static bool IsDefaultToolsetName(string? name) =>
            ToolboxCatalogRules.IsDefaultToolsetName(name);

        public static void NormalizeCatalog(ToolboxCatalog catalog) =>
            ToolboxCustomCatalogService.NormalizeCatalog(catalog);

        public static string Import(string sourcePath) =>
            ToolboxInternalStorageService.Import(sourcePath);

        public static bool IsInternalPath(string path) =>
            ToolboxInternalStorageService.IsInternalPath(path);

        public static void ClearStorage() => ToolboxInternalStorageService.ClearStorage();

        public static ToolboxCatalog LoadToolbox(string toolboxPath) =>
            ToolboxCatalogService.Load(toolboxPath);

        public static void Export(
            IReadOnlyCollection<ToolboxCatalog> catalogs,
            string menuMode,
            string defaultPackageName,
            string? targetPackagePath = null) =>
            ToolboxPackageService.Export(catalogs, menuMode, defaultPackageName, targetPackagePath);

        public static ToolboxPackageImportResult ImportPackage(string packagePath) =>
            ToolboxPackageService.ImportPackage(packagePath);

        public static List<ArcGisCommandDefinition> LoadCommands() =>
            ArcGisCommandCatalogService.LoadCommands();
    }
}
