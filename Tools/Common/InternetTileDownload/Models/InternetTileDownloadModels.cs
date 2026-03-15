#nullable enable

using System.Collections.Generic;
using ArcGIS.Core.Geometry;

namespace XIAOFUTools.Tools.InternetTileDownload
{
    internal enum InternetTileAreaSourceType
    {
        CurrentView = 0,
        CustomExtent = 1,
        FeatureLayer = 2
    }

    internal sealed class InternetTileAreaContext
    {
        public InternetTileAreaSourceType SourceType { get; set; }

        public bool PreferSelection { get; set; } = true;
    }

    internal sealed class InternetTileResolvedArea
    {
        public InternetTileAreaSourceType SourceType { get; set; }

        public bool RequiresPreciseClip { get; set; }

        public Polygon? AreaGeometry { get; set; }

        public Envelope? Extent { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    internal sealed class InternetTileDownloadRequest
    {
        public InternetTileServiceDefinition ServiceDefinition { get; set; } = new();

        public string LevelId { get; set; } = string.Empty;

        public string OutputFilePath { get; set; } = string.Empty;

        public bool UseCache { get; set; } = true;
    }

    internal sealed class InternetTileDownloadExecutionRequest
    {
        public InternetTileDownloadRequest Request { get; set; } = new();

        public InternetTileResolvedArea ResolvedArea { get; set; } = new();

        public string? TargetSpatialReferenceText { get; set; }
    }

    internal sealed class InternetTileDownloadResult
    {
        public string OutputFilePath { get; set; } = string.Empty;

        public int TotalTileCount { get; set; }

        public int DownloadedTileCount { get; set; }

        public bool HasPartialCoverage { get; set; }

        public List<string> Messages { get; } = new();
    }

    internal sealed class InternetTileDownloadInspectionResult
    {
        public bool CanDownload { get; init; }

        public bool ShouldWarn { get; init; }

        public bool ShouldBlock { get; init; }

        public bool UseFastClip { get; init; }

        public int TotalTileCount { get; init; }

        public int CheckedTileCount { get; init; }

        public int AvailableTileCount { get; init; }

        public int RecommendedTileConcurrency { get; init; }

        public IReadOnlyList<string> Messages { get; init; } = new List<string>();
    }
}
