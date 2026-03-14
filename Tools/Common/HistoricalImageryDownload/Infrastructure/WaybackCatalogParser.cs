#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure
{
    internal static class WaybackCatalogParser
    {
        public static IReadOnlyList<HistoricalVersionItem> Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<HistoricalVersionItem>();
            }

            var items = new List<HistoricalVersionItem>();
            using var document = JsonDocument.Parse(json);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                var item = property.Value;
                var title = item.TryGetProperty("itemTitle", out var titleProperty)
                    ? titleProperty.GetString() ?? string.Empty
                    : string.Empty;

                items.Add(new HistoricalVersionItem
                {
                    Provider = HistoricalImageryProviderType.Wayback,
                    VersionId = property.Name,
                    DisplayDate = ExtractDate(title) ?? property.Name,
                    Summary = title,
                    TileUrlTemplate = item.TryGetProperty("itemURL", out var itemUrlProperty)
                        ? itemUrlProperty.GetString()
                        : null,
                    MetadataLayerUrl = item.TryGetProperty("metadataLayerUrl", out var metadataProperty)
                        ? metadataProperty.GetString()
                        : null
                });
            }

            return items
                .OrderByDescending(item => item.DisplayDate, StringComparer.Ordinal)
                .ToArray();
        }

        private static string? ExtractDate(string title)
        {
            for (var index = 0; index <= title.Length - 10; index++)
            {
                var candidate = title.Substring(index, 10).Trim();
                if (candidate.Length == 10 && DateOnly.TryParse(candidate, out _))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
