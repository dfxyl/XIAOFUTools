using XIAOFUTools.Tools.Common.RasterExport;

namespace XIAOFUTools.Tests.RasterExport;

public class ArcGisRasterTileCacheTests
{
    [Fact]
    public void GetWorldFilePath_UsesPgwForPngTiles()
    {
        var worldFilePath = ArcGisRasterTileCache.GetWorldFilePath(@"C:\temp\tile_1_2.png");

        Assert.Equal(@"C:\temp\tile_1_2.pgw", worldFilePath);
    }

    [Fact]
    public void BuildWorldFileContent_UsesPixelCenterCoordinates()
    {
        var content = ArcGisRasterTileCache.BuildWorldFileContent(
            minX: 100,
            minY: 180,
            maxX: 140,
            maxY: 220,
            pixelWidth: 4,
            pixelHeight: 4);

        var lines = content.Split(Environment.NewLine);
        Assert.Equal("10", lines[0]);
        Assert.Equal("0", lines[1]);
        Assert.Equal("0", lines[2]);
        Assert.Equal("-10", lines[3]);
        Assert.Equal("105", lines[4]);
        Assert.Equal("215", lines[5]);
    }

    [Fact]
    public void BuildTilePath_CombinesSessionDirectoryAndTileIdentity()
    {
        var tilePath = ArcGisRasterTileCache.BuildTilePath(@"C:\cache", "L18", 12, 34);

        Assert.Equal(@"C:\cache\L18_r12_c34.png", tilePath);
    }

    [Fact]
    public void GetPixelWindow_ClampsToIntersectingEnvelope()
    {
        var window = ArcGisRasterTileCache.GetPixelWindow(
            rasterMinX: 0,
            rasterMinY: 0,
            rasterMaxX: 256,
            rasterMaxY: 256,
            pixelWidth: 256,
            pixelHeight: 256,
            clipMinX: 64,
            clipMinY: 32,
            clipMaxX: 192,
            clipMaxY: 224);

        Assert.Equal(64, window.StartX);
        Assert.Equal(192, window.EndXExclusive);
        Assert.Equal(32, window.StartY);
        Assert.Equal(224, window.EndYExclusive);
    }

    [Fact]
    public void GetPixelWindow_ReturnsEmptyWhenClipOutsideRaster()
    {
        var window = ArcGisRasterTileCache.GetPixelWindow(
            rasterMinX: 0,
            rasterMinY: 0,
            rasterMaxX: 256,
            rasterMaxY: 256,
            pixelWidth: 256,
            pixelHeight: 256,
            clipMinX: 300,
            clipMinY: 300,
            clipMaxX: 400,
            clipMaxY: 400);

        Assert.True(window.IsEmpty);
    }
}
