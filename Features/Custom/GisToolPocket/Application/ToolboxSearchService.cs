#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using XIAOFUTools.Features.Custom.GisToolPocket.Core;
using XIAOFUTools.Features.Custom.GisToolPocket.Infrastructure;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Application
{
    internal sealed class ToolboxSearchEntry
    {
        public ToolboxTool Tool { get; init; } = new();

        public string CatalogTitle { get; init; } = string.Empty;

        public string ToolsetPath { get; init; } = string.Empty;

        public string DisplayText { get; set; } = string.Empty;

        public string Tooltip { get; set; } = string.Empty;

        public string SearchText { get; set; } = string.Empty;
    }

    internal static class ToolboxSearchService
    {
        public static List<ToolboxSearchEntry> Search(string query, int limit = 12)
        {
            var entries = BuildIndex();
            var normalizedQuery = Normalize(query);

            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return entries
                    .OrderBy(entry => entry.CatalogTitle, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(entry => entry.ToolsetPath, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(entry => entry.DisplayText, StringComparer.CurrentCultureIgnoreCase)
                    .Take(limit)
                    .ToList();
            }

            return entries
                .Select(entry => new
                {
                    Entry = entry,
                    Score = Score(entry, normalizedQuery)
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Entry.CatalogTitle, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Entry.DisplayText, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => item.Entry)
                .Take(limit)
                .ToList();
        }

        private static List<ToolboxSearchEntry> BuildIndex()
        {
            var configuration = ToolboxConfigurationStore.Load();
            var result = new List<ToolboxSearchEntry>();
            var displayCounts = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);

            foreach (var catalog in configuration.Catalogs)
            {
                ToolboxCustomCatalogService.NormalizeCatalog(catalog);
                if (!IsSearchableCatalog(catalog))
                    continue;

                foreach (var entry in EnumerateCatalogTools(catalog))
                {
                    var displayText = BuildDisplayText(entry.Tool, entry.CatalogTitle, entry.ToolsetPath);
                    if (displayCounts.TryGetValue(displayText, out var count))
                    {
                        count++;
                        displayCounts[displayText] = count;
                        displayText = $"{displayText} ({count})";
                    }
                    else
                    {
                        displayCounts[displayText] = 1;
                    }

                    entry.DisplayText = displayText;
                    entry.Tooltip = BuildTooltip(entry.Tool, entry.CatalogTitle, entry.ToolsetPath);
                    entry.SearchText = Normalize(string.Join(" ",
                        entry.Tool.Caption,
                        entry.Tool.Name,
                        entry.Tool.ToolPath,
                        entry.Tool.SourceToolPath,
                        entry.CatalogTitle,
                        entry.ToolsetPath));
                    result.Add(entry);
                }
            }

            return result;
        }

        private static bool IsSearchableCatalog(ToolboxCatalog catalog)
        {
            if (!catalog.IsVisible)
                return false;

            return ToolboxCustomCatalogService.IsCustomCatalog(catalog) ||
                   (!string.IsNullOrWhiteSpace(catalog.ToolboxPath) && File.Exists(catalog.ToolboxPath));
        }

        private static IEnumerable<ToolboxSearchEntry> EnumerateCatalogTools(ToolboxCatalog catalog)
        {
            foreach (var entry in EnumerateToolsets(catalog.Toolsets, catalog.DisplayTitle, string.Empty))
                yield return entry;
        }

        private static IEnumerable<ToolboxSearchEntry> EnumerateToolsets(
            IEnumerable<ToolboxToolset> toolsets,
            string catalogTitle,
            string parentPath)
        {
            foreach (var toolset in toolsets)
            {
                if (toolset is null || !toolset.IsVisible)
                    continue;

                var toolsetPath = BuildToolsetPath(parentPath, toolset.Name);
                foreach (var tool in toolset.Tools)
                {
                    if (IsSearchableTool(tool))
                    {
                        yield return new ToolboxSearchEntry
                        {
                            Tool = tool,
                            CatalogTitle = catalogTitle,
                            ToolsetPath = toolsetPath
                        };
                    }
                }

                foreach (var childEntry in EnumerateToolsets(toolset.Children, catalogTitle, toolsetPath))
                    yield return childEntry;
            }
        }

        private static bool IsSearchableTool(ToolboxTool tool)
        {
            return tool is not null &&
                   tool.IsVisible &&
                   !string.IsNullOrWhiteSpace(tool.ToolPath);
        }

        private static string BuildToolsetPath(string parentPath, string toolsetName)
        {
            if (ToolboxCustomCatalogService.IsDefaultToolsetName(toolsetName))
                return parentPath;

            if (string.IsNullOrWhiteSpace(parentPath))
                return toolsetName;

            return $"{parentPath} / {toolsetName}";
        }

        private static string BuildDisplayText(ToolboxTool tool, string catalogTitle, string toolsetPath)
        {
            var toolTitle = ResolveToolTitle(tool);
            var location = string.IsNullOrWhiteSpace(toolsetPath)
                ? catalogTitle
                : $"{catalogTitle} / {toolsetPath}";

            return string.IsNullOrWhiteSpace(location) ? toolTitle : $"{toolTitle} - {location}";
        }

        private static string BuildTooltip(ToolboxTool tool, string catalogTitle, string toolsetPath)
        {
            var parts = new List<string>
            {
                ResolveToolTitle(tool)
            };

            var location = string.IsNullOrWhiteSpace(toolsetPath)
                ? catalogTitle
                : $"{catalogTitle} / {toolsetPath}";
            if (!string.IsNullOrWhiteSpace(location))
                parts.Add(location);

            if (!string.IsNullOrWhiteSpace(tool.ToolPath))
                parts.Add(tool.ToolPath);

            return string.Join(Environment.NewLine, parts);
        }

        private static string ResolveToolTitle(ToolboxTool tool)
        {
            return string.IsNullOrWhiteSpace(tool.Caption) ? tool.Name : tool.Caption;
        }

        private static int Score(ToolboxSearchEntry entry, string normalizedQuery)
        {
            var toolCaption = Normalize(entry.Tool.Caption);
            var toolName = Normalize(entry.Tool.Name);
            var catalogTitle = Normalize(entry.CatalogTitle);
            var toolsetPath = Normalize(entry.ToolsetPath);

            if (toolCaption.Equals(normalizedQuery, StringComparison.Ordinal))
                return 1000;

            if (toolName.Equals(normalizedQuery, StringComparison.Ordinal))
                return 950;

            if (toolCaption.StartsWith(normalizedQuery, StringComparison.Ordinal))
                return 800;

            if (toolName.StartsWith(normalizedQuery, StringComparison.Ordinal))
                return 760;

            if (toolCaption.Contains(normalizedQuery, StringComparison.Ordinal))
                return 620;

            if (toolName.Contains(normalizedQuery, StringComparison.Ordinal))
                return 580;

            if (catalogTitle.Contains(normalizedQuery, StringComparison.Ordinal))
                return 420;

            if (toolsetPath.Contains(normalizedQuery, StringComparison.Ordinal))
                return 360;

            return entry.SearchText.Contains(normalizedQuery, StringComparison.Ordinal) ? 240 : 0;
        }

        private static string Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();
        }
    }
}

