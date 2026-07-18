#nullable enable

using System;
using System.Collections.Generic;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    internal enum HistoricalTileScheme
    {
        GoogleGeographic = 0,
        WebMercator = 1
    }

    internal sealed class HistoricalTileDefinition
    {
        public int ZoomLevel { get; init; }

        public int Row { get; init; }

        public int Column { get; init; }

        public string? Path { get; init; }

        public int PixelOffsetX { get; init; }

        public int PixelOffsetY { get; init; }

        public double MinX { get; init; }

        public double MinY { get; init; }

        public double MaxX { get; init; }

        public double MaxY { get; init; }
    }

    internal sealed class HistoricalTilePlan
    {
        public HistoricalTileScheme Scheme { get; init; }

        public int PixelWidth { get; init; }

        public int PixelHeight { get; init; }

        public double OriginX { get; init; }

        public double OriginY { get; init; }

        public double PixelSizeX { get; init; }

        public double PixelSizeY { get; init; }

        public IReadOnlyList<HistoricalTileDefinition> Tiles { get; init; } = Array.Empty<HistoricalTileDefinition>();
    }

    internal sealed class HistoricalTileDownloadRequest
    {
        public HistoricalVersionItem Version { get; init; } = new();

        public HistoricalTileDefinition Tile { get; init; } = new();

        public bool UseCache { get; init; }

        public GoogleNearestDateFallbackMode GoogleNearestDateFallbackMode { get; init; } = GoogleNearestDateFallbackMode.SeparateOutputs;
    }

    internal sealed class HistoricalTileDownloadResult
    {
        public byte[]? ImageBytes { get; init; }

        public string? Message { get; init; }

        public bool UsedNearestDateFallback { get; init; }

        public string? ResolvedDisplayDate { get; init; }

        public bool HasData => ImageBytes != null && ImageBytes.Length > 0;
    }
}
