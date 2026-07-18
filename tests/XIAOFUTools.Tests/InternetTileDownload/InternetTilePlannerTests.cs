using XIAOFUTools.Features.General.InternetTileDownload;
using XIAOFUTools.Features.General.InternetTileDownload.Core;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class InternetTilePlannerTests
{
    [Fact]
    public void Plan_WebMercatorLevel_ReturnsExpectedSingleTile()
    {
        var definition = new InternetTileServiceDefinition
        {
            ServiceKind = InternetTileServiceKind.Xyz,
            SourceSpatialReferenceText = "EPSG:3857",
            MatrixProfile = InternetTileMatrixProfile.WebMercator,
            Levels =
            [
                new InternetTileLevelDefinition
                {
                    LevelId = "1",
                    Resolution = 78271.51696402048d,
                    TopLeftX = -20037508.342789244d,
                    TopLeftY = 20037508.342789244d,
                    TileWidth = 256,
                    TileHeight = 256,
                    MatrixWidth = 2,
                    MatrixHeight = 2
                }
            ]
        };

        var plan = InternetTilePlanner.Plan(definition, "1", 1d, 1d, 1000d, 1000d);

        var tile = Assert.Single(plan.Tiles);
        Assert.Equal(1, tile.Column);
        Assert.Equal(0, tile.Row);
        Assert.Equal(0, tile.PixelOffsetX);
        Assert.Equal(0, tile.PixelOffsetY);
        Assert.Equal(256, plan.PixelWidth);
        Assert.Equal(256, plan.PixelHeight);
    }

    [Fact]
    public void Plan_GeographicLevel_ReturnsExpectedOriginAndTileBounds()
    {
        var definition = new InternetTileServiceDefinition
        {
            ServiceKind = InternetTileServiceKind.Wmts,
            SourceSpatialReferenceText = "EPSG:4326",
            MatrixProfile = InternetTileMatrixProfile.Geographic,
            Levels =
            [
                new InternetTileLevelDefinition
                {
                    LevelId = "0",
                    Resolution = 0.703125d,
                    TopLeftX = -180d,
                    TopLeftY = 90d,
                    TileWidth = 256,
                    TileHeight = 256,
                    MatrixWidth = 2,
                    MatrixHeight = 1
                }
            ]
        };

        var plan = InternetTilePlanner.Plan(definition, "0", -10d, -10d, -1d, 10d);

        var tile = Assert.Single(plan.Tiles);
        Assert.Equal(0, tile.Column);
        Assert.Equal(0, tile.Row);
        Assert.Equal(-180d, plan.OriginX, 6);
        Assert.Equal(90d, plan.OriginY, 6);
        Assert.Equal(-180d, tile.MinX, 6);
        Assert.Equal(-90d, tile.MinY, 6);
        Assert.Equal(0d, tile.MaxX, 6);
        Assert.Equal(90d, tile.MaxY, 6);
    }

    [Fact]
    public void Plan_OutOfCoverageExtent_ThrowsInvalidOperationException()
    {
        var definition = new InternetTileServiceDefinition
        {
            ServiceKind = InternetTileServiceKind.Wmts,
            SourceSpatialReferenceText = "EPSG:4326",
            MatrixProfile = InternetTileMatrixProfile.Geographic,
            Levels =
            [
                new InternetTileLevelDefinition
                {
                    LevelId = "0",
                    Resolution = 0.703125d,
                    TopLeftX = -180d,
                    TopLeftY = 90d,
                    TileWidth = 256,
                    TileHeight = 256,
                    MatrixWidth = 2,
                    MatrixHeight = 1
                }
            ]
        };

        Assert.Throws<InvalidOperationException>(() => InternetTilePlanner.Plan(definition, "0", 300d, 10d, 320d, 20d));
    }
}
