#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Services
{
    internal enum WaybackMetadataSourceType
    {
        MapLayer = 0,
        GlobalLatest = 1
    }

    internal sealed record WaybackMapLayerReference(string? LayerName, string? LayerUri);

    internal sealed class WaybackMetadataSourceCandidate
    {
        public required HistoricalVersionItem Version { get; init; }

        public required WaybackMetadataSourceType SourceType { get; init; }

        public string? LayerName { get; init; }

        public string DisplayText
        {
            get
            {
                var versionText = string.IsNullOrWhiteSpace(Version.DisplayDate) ? Version.VersionId : Version.DisplayDate;
                return SourceType == WaybackMetadataSourceType.GlobalLatest
                    ? $"全局最新版本 - {versionText}"
                    : $"{LayerName ?? "当前地图图层"} - {versionText}";
            }
        }
    }

    internal sealed class WaybackMetadataSourceResolution
    {
        public bool RequiresSelection { get; init; }

        public WaybackMetadataSourceCandidate? AutoSelectedCandidate { get; init; }

        public IReadOnlyList<WaybackMetadataSourceCandidate> Candidates { get; init; } = Array.Empty<WaybackMetadataSourceCandidate>();
    }

    internal static class WaybackMetadataSourceResolver
    {
        private static readonly Regex ReleaseIdRegex = new(@"/(?:tile|tilemap)/(\d+)/", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ReleaseDateRegex = new(@"(\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);

        public static WaybackMetadataSourceResolution Resolve(
            IEnumerable<HistoricalVersionItem> catalogVersions,
            IEnumerable<WaybackMapLayerReference> mapLayers)
        {
            var catalog = catalogVersions
                .Where(version => version.Provider == HistoricalImageryProviderType.Wayback)
                .OrderByDescending(version => ParseDateOrMin(version.DisplayDate))
                .ToList();

            if (catalog.Count == 0)
            {
                return new WaybackMetadataSourceResolution();
            }

            var latest = catalog[0];
            var matchedCandidates = mapLayers
                .Select(layer => TryCreateLayerCandidate(catalog, layer))
                .Where(candidate => candidate != null)
                .Cast<WaybackMetadataSourceCandidate>()
                .ToList();

            if (matchedCandidates.Count == 0)
            {
                return new WaybackMetadataSourceResolution
                {
                    AutoSelectedCandidate = CreateGlobalLatestCandidate(latest)
                };
            }

            if (matchedCandidates.Count == 1)
            {
                return new WaybackMetadataSourceResolution
                {
                    AutoSelectedCandidate = matchedCandidates[0]
                };
            }

            var candidates = new List<WaybackMetadataSourceCandidate>(matchedCandidates);
            if (matchedCandidates.All(candidate => !string.Equals(candidate.Version.VersionId, latest.VersionId, StringComparison.Ordinal)))
            {
                candidates.Add(CreateGlobalLatestCandidate(latest));
            }

            return new WaybackMetadataSourceResolution
            {
                RequiresSelection = true,
                Candidates = candidates
            };
        }

        private static WaybackMetadataSourceCandidate? TryCreateLayerCandidate(
            IReadOnlyList<HistoricalVersionItem> catalog,
            WaybackMapLayerReference layer)
        {
            var version = TryMatchByReleaseId(catalog, layer.LayerUri)
                ?? TryMatchByReleaseDateWhenWaybackLayer(catalog, layer.LayerName, layer.LayerUri)
                ?? TryMatchByReleaseDateWhenWaybackLayer(catalog, layer.LayerUri, layer.LayerName);

            if (version == null)
            {
                return null;
            }

            return new WaybackMetadataSourceCandidate
            {
                SourceType = WaybackMetadataSourceType.MapLayer,
                Version = version,
                LayerName = layer.LayerName
            };
        }

        private static HistoricalVersionItem? TryMatchByReleaseId(IEnumerable<HistoricalVersionItem> catalog, string? layerUri)
        {
            if (string.IsNullOrWhiteSpace(layerUri))
            {
                return null;
            }

            var match = ReleaseIdRegex.Match(layerUri);
            if (!match.Success)
            {
                return null;
            }

            var releaseId = match.Groups[1].Value;
            return catalog.FirstOrDefault(version => string.Equals(version.VersionId, releaseId, StringComparison.Ordinal));
        }

        private static HistoricalVersionItem? TryMatchByReleaseDate(IEnumerable<HistoricalVersionItem> catalog, string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = ReleaseDateRegex.Match(text);
            if (!match.Success)
            {
                return null;
            }

            var releaseDate = match.Groups[1].Value;
            return catalog.FirstOrDefault(version => string.Equals(version.DisplayDate, releaseDate, StringComparison.Ordinal));
        }

        private static HistoricalVersionItem? TryMatchByReleaseDateWhenWaybackLayer(
            IEnumerable<HistoricalVersionItem> catalog,
            string? text,
            string? fallbackMarkerSource)
        {
            return ContainsWaybackMarker(text) || ContainsWaybackMarker(fallbackMarkerSource)
                ? TryMatchByReleaseDate(catalog, text)
                : null;
        }

        private static WaybackMetadataSourceCandidate CreateGlobalLatestCandidate(HistoricalVersionItem latest)
        {
            return new WaybackMetadataSourceCandidate
            {
                SourceType = WaybackMetadataSourceType.GlobalLatest,
                Version = latest
            };
        }

        private static DateOnly ParseDateOrMin(string? value)
            => DateOnly.TryParse(value, out var parsed) ? parsed : DateOnly.MinValue;

        private static bool ContainsWaybackMarker(string? text)
            => !string.IsNullOrWhiteSpace(text) &&
               (text.Contains("wayback", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("world_imagery", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("world imagery", StringComparison.OrdinalIgnoreCase));
    }
}
