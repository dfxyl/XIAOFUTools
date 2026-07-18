#nullable enable

using System;
using System.Collections.Generic;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Services
{
    internal sealed class HistoricalCoverageProbeRequest
    {
        public HistoricalVersionItem Version { get; init; } = new();

        public IReadOnlyList<HistoricalTileDefinition> Tiles { get; init; } = Array.Empty<HistoricalTileDefinition>();
    }

    internal sealed class HistoricalCoverageProbeResult
    {
        public int CheckedTileCount { get; init; }

        public int AvailableTileCount { get; init; }

        public bool HasCoverage => AvailableTileCount > 0;
    }

    internal sealed class HistoricalDownloadInspectionResult
    {
        public bool CanDownload { get; init; }

        public bool ShouldWarn { get; init; }

        public bool ShouldBlock { get; init; }

        public bool UseFastClip { get; init; }

        public int TotalTileCount { get; init; }

        public int CheckedTileCount { get; init; }

        public int AvailableTileCount { get; init; }

        public int RecommendedTileConcurrency { get; init; }

        public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();
    }
}
