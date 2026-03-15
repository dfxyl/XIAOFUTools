using System.Collections.Generic;
using XIAOFUTools.Tools.InternetTileDownload;
using XIAOFUTools.Tools.InternetTileDownload.Services;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class InternetTileRequestExpanderTests
{
    [Fact]
    public void Expand_ArcGisRestTile_UsesPathSegments()
    {
        var definition = CreateDefinition(
            InternetTileServiceKind.ArcGisRestTile,
            InternetTileTemplateMode.ArcGisRestTile,
            "https://services.arcgisonline.com/arcgis/rest/services/World_Imagery/MapServer/tile/{level}/{row}/{col}");

        var url = InternetTileRequestExpander.Expand(definition, "5", 10, 12);

        Assert.Equal("https://services.arcgisonline.com/arcgis/rest/services/World_Imagery/MapServer/tile/5/10/12", url);
    }

    [Fact]
    public void Expand_TmsTemplate_UsesBottomOriginRow()
    {
        var definition = CreateDefinition(
            InternetTileServiceKind.Xyz,
            InternetTileTemplateMode.PathSegments,
            "https://example.com/tms/{level}/{col}/{row}.png",
            rowOrigin: InternetTileRowOrigin.Bottom,
            matrixHeight: 8);

        var url = InternetTileRequestExpander.Expand(definition, "3", 1, 2);

        Assert.Equal("https://example.com/tms/3/2/6.png", url);
    }

    [Fact]
    public void Expand_QuadKeyTemplate_GeneratesQuadKey()
    {
        var definition = CreateDefinition(
            InternetTileServiceKind.QuadKey,
            InternetTileTemplateMode.QuadKey,
            "https://ecn.t0.tiles.virtualearth.net/tiles/a{quadkey}.jpeg?g=1");

        var url = InternetTileRequestExpander.Expand(definition, "3", 5, 3);

        Assert.Equal("https://ecn.t0.tiles.virtualearth.net/tiles/a213.jpeg?g=1", url);
    }

    [Fact]
    public void Expand_SubdomainTemplate_UsesDeterministicSubdomain()
    {
        var definition = CreateDefinition(
            InternetTileServiceKind.Xyz,
            InternetTileTemplateMode.QueryParameters,
            "https://{subdomain}.example.com/tiles?x={col}&y={row}&z={level}",
            subdomains: ["t0", "t1", "t2", "t3"]);

        var url = InternetTileRequestExpander.Expand(definition, "4", 1, 2);

        Assert.Equal("https://t3.example.com/tiles?x=2&y=1&z=4", url);
    }

    private static InternetTileServiceDefinition CreateDefinition(
        InternetTileServiceKind kind,
        InternetTileTemplateMode templateMode,
        string template,
        InternetTileRowOrigin rowOrigin = InternetTileRowOrigin.Top,
        int matrixHeight = 16,
        IReadOnlyList<string>? subdomains = null)
    {
        return new InternetTileServiceDefinition
        {
            ServiceKind = kind,
            TemplateMode = templateMode,
            UrlTemplate = template,
            SourceSpatialReferenceText = "EPSG:3857",
            MatrixProfile = InternetTileMatrixProfile.WebMercator,
            RowOrigin = rowOrigin,
            Subdomains = subdomains ?? [],
            Levels =
            [
                new InternetTileLevelDefinition
                {
                    LevelId = "3",
                    Resolution = 19567.87924100512d,
                    TopLeftX = -20037508.342789244d,
                    TopLeftY = 20037508.342789244d,
                    TileWidth = 256,
                    TileHeight = 256,
                    MatrixWidth = 8,
                    MatrixHeight = matrixHeight
                },
                new InternetTileLevelDefinition
                {
                    LevelId = "4",
                    Resolution = 9783.93962050256d,
                    TopLeftX = -20037508.342789244d,
                    TopLeftY = 20037508.342789244d,
                    TileWidth = 256,
                    TileHeight = 256,
                    MatrixWidth = 16,
                    MatrixHeight = matrixHeight
                },
                new InternetTileLevelDefinition
                {
                    LevelId = "5",
                    Resolution = 4891.96981025128d,
                    TopLeftX = -20037508.342789244d,
                    TopLeftY = 20037508.342789244d,
                    TileWidth = 256,
                    TileHeight = 256,
                    MatrixWidth = 32,
                    MatrixHeight = matrixHeight
                }
            ]
        };
    }
}
