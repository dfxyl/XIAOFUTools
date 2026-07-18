using System;

namespace XIAOFUTools.Features.Custom.GisToolPocket.Core
{
    internal static class ToolboxCatalogRules
    {
        public const string CustomCatalogKind = "Custom";

        public static bool IsCustomCatalog(ToolboxCatalog? catalog)
        {
            return catalog is not null &&
                   string.Equals(catalog.CatalogKind, CustomCatalogKind, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDefaultToolsetName(string? name)
        {
            return string.IsNullOrWhiteSpace(name) ||
                   string.Equals(name, "默认工具集", StringComparison.OrdinalIgnoreCase);
        }
    }
}
