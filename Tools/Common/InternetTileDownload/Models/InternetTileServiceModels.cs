#nullable enable

using System;
using System.Collections.Generic;

namespace XIAOFUTools.Tools.InternetTileDownload
{
    public enum InternetTileServiceKind
    {
        Wmts = 0,
        Xyz = 1,
        ArcGisRestTile = 2,
        QuadKey = 3
    }

    internal enum InternetTileTemplateMode
    {
        QueryParameters = 0,
        PathSegments = 1,
        RestfulWmts = 2,
        ArcGisRestTile = 3,
        QuadKey = 4
    }

    internal enum InternetTileRowOrigin
    {
        Top = 0,
        Bottom = 1
    }

    internal enum InternetTileVendorKind
    {
        Unknown = 0,
        Tianditu = 1,
        Jilin1 = 2,
        ArcGisOnline = 3,
        GoogleLike = 4
    }

    internal enum InternetTileMatrixProfile
    {
        WebMercator = 0,
        Geographic = 1,
        Unknown = 2
    }

    internal sealed class InternetTileLevelDefinition
    {
        public string LevelId { get; init; } = string.Empty;

        public double Resolution { get; init; }

        public double TopLeftX { get; init; }

        public double TopLeftY { get; init; }

        public int TileWidth { get; init; } = 256;

        public int TileHeight { get; init; } = 256;

        public int MatrixWidth { get; init; }

        public int MatrixHeight { get; init; }
    }

    internal sealed class InternetTileTemplateParseResult
    {
        public InternetTileServiceKind ServiceKind { get; init; }

        public string NormalizedTemplate { get; init; } = string.Empty;

        public string? CapabilitiesUrl { get; init; }

        public string? LayerIdentifier { get; init; }

        public string? MatrixSetIdentifier { get; init; }

        public string? StyleIdentifier { get; init; }

        public string? Format { get; init; }

        public string SourceSpatialReferenceText { get; init; } = string.Empty;

        public InternetTileMatrixProfile MatrixProfile { get; init; } = InternetTileMatrixProfile.Unknown;

        public InternetTileTemplateMode TemplateMode { get; init; } = InternetTileTemplateMode.QueryParameters;

        public InternetTileRowOrigin RowOrigin { get; init; } = InternetTileRowOrigin.Top;

        public InternetTileVendorKind VendorKind { get; init; } = InternetTileVendorKind.Unknown;

        public IReadOnlyList<string> Subdomains { get; init; } = Array.Empty<string>();
    }

    internal sealed class InternetTileServiceDefinition
    {
        public InternetTileServiceKind ServiceKind { get; init; }

        public string UrlTemplate { get; init; } = string.Empty;

        public string SourceSpatialReferenceText { get; init; } = string.Empty;

        public InternetTileMatrixProfile MatrixProfile { get; init; } = InternetTileMatrixProfile.Unknown;

        public InternetTileTemplateMode TemplateMode { get; init; } = InternetTileTemplateMode.QueryParameters;

        public InternetTileRowOrigin RowOrigin { get; init; } = InternetTileRowOrigin.Top;

        public InternetTileVendorKind VendorKind { get; init; } = InternetTileVendorKind.Unknown;

        public string? CapabilitiesUrl { get; init; }

        public string? LayerIdentifier { get; init; }

        public string? MatrixSetIdentifier { get; init; }

        public string? StyleIdentifier { get; init; }

        public string? Format { get; init; }

        public IReadOnlyList<string> Subdomains { get; init; } = Array.Empty<string>();

        public IReadOnlyList<InternetTileLevelDefinition> Levels { get; init; } = Array.Empty<InternetTileLevelDefinition>();
    }

    internal sealed class InternetTileDefinition
    {
        public string LevelId { get; init; } = string.Empty;

        public int Row { get; init; }

        public int Column { get; init; }

        public int PixelOffsetX { get; init; }

        public int PixelOffsetY { get; init; }

        public double MinX { get; init; }

        public double MinY { get; init; }

        public double MaxX { get; init; }

        public double MaxY { get; init; }
    }

    internal sealed class InternetTilePlan
    {
        public string LevelId { get; init; } = string.Empty;

        public int PixelWidth { get; init; }

        public int PixelHeight { get; init; }

        public double OriginX { get; init; }

        public double OriginY { get; init; }

        public double PixelSizeX { get; init; }

        public double PixelSizeY { get; init; }

        public IReadOnlyList<InternetTileDefinition> Tiles { get; init; } = Array.Empty<InternetTileDefinition>();
    }
}
