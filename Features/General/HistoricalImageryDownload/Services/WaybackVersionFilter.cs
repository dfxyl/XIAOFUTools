#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Services
{
    public static class WaybackVersionFilter
    {
        public static IReadOnlyList<HistoricalVersionItem> FilterVersions(IEnumerable<HistoricalVersionItem> versions, bool includeAllVersions)
        {
            var allVersions = versions
                .OrderByDescending(item => ParseDateOrMin(item.DisplayDate))
                .ToList();

            if (includeAllVersions)
            {
                return allVersions;
            }

            var explicitChangeVersions = allVersions
                .Where(IsRealChangeRelease)
                .ToList();

            if (explicitChangeVersions.Count > 0)
            {
                return explicitChangeVersions;
            }

            var normalized = versions
                .Where(item => !string.IsNullOrWhiteSpace(item.AcquisitionDate))
                .OrderByDescending(item => ParseDateOrMin(item.DisplayDate))
                .ToList();

            if (normalized.Count == 0)
            {
                return allVersions;
            }

            var seenChangeKeys = new HashSet<string>(StringComparer.Ordinal);
            var changedVersions = new List<HistoricalVersionItem>();

            foreach (var version in normalized)
            {
                var changeKey = BuildChangeKey(version);
                if (string.IsNullOrWhiteSpace(changeKey))
                {
                    continue;
                }

                if (seenChangeKeys.Add(changeKey))
                {
                    changedVersions.Add(version);
                }
            }

            return changedVersions
                .OrderByDescending(item => ParseDateOrMin(item.AcquisitionDate))
                .ThenByDescending(item => ParseDateOrMin(item.DisplayDate))
                .ToArray();
        }

        public static IReadOnlyList<HistoricalVersionItem> GetChangedVersions(IEnumerable<HistoricalVersionItem> versions)
            => FilterVersions(versions, includeAllVersions: false);

        private static string? BuildChangeKey(HistoricalVersionItem version)
            => !string.IsNullOrWhiteSpace(version.ChangeKey)
                ? version.ChangeKey
                : version.AcquisitionDate;

        private static bool IsRealChangeRelease(HistoricalVersionItem version)
            => !string.IsNullOrWhiteSpace(version.ChangeKey)
                && string.Equals(version.ChangeKey, version.VersionId, StringComparison.Ordinal);

        private static DateOnly ParseDateOrMin(string? value)
            => DateOnly.TryParse(value, out var parsed) ? parsed : DateOnly.MinValue;
    }
}
