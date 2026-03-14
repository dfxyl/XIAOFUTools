#nullable enable

using System.Collections.Generic;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    public sealed class HistoricalAreaContext
    {
        public HistoricalAreaSourceType SourceType { get; set; }

        public bool PreferSelection { get; set; } = true;

        public bool RequiresFeatureLayer => SourceType == HistoricalAreaSourceType.FeatureLayer;
    }

    public sealed class HistoricalResolvedArea
    {
        public HistoricalAreaSourceType SourceType { get; set; }

        public bool RequiresPreciseClip { get; set; }

        public ArcGIS.Core.Geometry.Polygon? AreaGeometry { get; set; }

        public ArcGIS.Core.Geometry.Envelope? Extent { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    public sealed class HistoricalDownloadExecutionRequest
    {
        public HistoricalDownloadRequest Request { get; set; } = new();

        public HistoricalResolvedArea ResolvedArea { get; set; } = new();

        public string? TargetSpatialReferenceText { get; set; }
    }

    public sealed class HistoricalDownloadResult
    {
        public string OutputFilePath { get; set; } = string.Empty;

        public List<string> OutputFilePaths { get; } = new();

        public int TotalTileCount { get; set; }

        public int DownloadedTileCount { get; set; }

        public bool HasPartialCoverage { get; set; }

        public List<string> Messages { get; } = new();
    }
}
