using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport.Core
{
    internal static class MapSeriesExportPlanner
    {
        internal static IReadOnlyList<int> ResolvePageNumbers(
            string exportMode,
            int pageCount,
            IEnumerable<int> selectedPages,
            string pageRange)
        {
            return exportMode switch
            {
                "全部导出" => Enumerable.Range(1, Math.Max(0, pageCount)).ToArray(),
                "选中导出" => Normalize(selectedPages, pageCount),
                "指定页面" => ParseRange(pageRange, pageCount),
                _ => Array.Empty<int>()
            };
        }

        internal static IReadOnlyList<int> ParseRange(string range, int pageCount)
        {
            if (string.IsNullOrWhiteSpace(range) || pageCount <= 0)
            {
                return Array.Empty<int>();
            }

            var pages = new HashSet<int>();
            var parts = range.Split(
                new[] { ',', '，', ';', '；' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                var rangeParts = Regex.Split(part, "[-—~～]");
                if (rangeParts.Length == 2 &&
                    int.TryParse(rangeParts[0].Trim(), out var start) &&
                    int.TryParse(rangeParts[1].Trim(), out var end))
                {
                    if (start > end)
                    {
                        (start, end) = (end, start);
                    }

                    for (var page = Math.Max(1, start); page <= Math.Min(pageCount, end); page++)
                    {
                        pages.Add(page);
                    }
                }
                else if (rangeParts.Length == 1 &&
                         int.TryParse(part.Trim(), out var page) &&
                         page >= 1 && page <= pageCount)
                {
                    pages.Add(page);
                }
            }

            return pages.OrderBy(page => page).ToArray();
        }

        private static IReadOnlyList<int> Normalize(IEnumerable<int> pages, int pageCount)
        {
            return (pages ?? Array.Empty<int>())
                .Where(page => page >= 1 && page <= pageCount)
                .Distinct()
                .OrderBy(page => page)
                .ToArray();
        }
    }
}
