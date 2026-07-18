using XIAOFUTools.Features.General.HistoricalImagery;
using Xunit;

namespace XIAOFUTools.Tests.HistoricalImagery;

public class HistoricalImageryLayerRequestFactoryTests
{
    [Fact]
    public void TryCreate_ReturnsLayerNameAndUri_WhenVersionIsValid()
    {
        var ok = HistoricalImageryLayerRequestFactory.TryCreate(
            "2024-03-18",
            "Wayback 2024-03-18",
            "https://example.com/MapServer/tile/{z}/{y}/{x}",
            321,
            out var request,
            out var errorMessage);

        Assert.True(ok);
        Assert.NotNull(request);
        Assert.Equal("Wayback 2024-03-18", request!.LayerName);
        Assert.Equal("https://example.com/MapServer/tile/%7Bz%7D/%7By%7D/%7Bx%7D", request.LayerUri.AbsoluteUri);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryCreate_ReturnsFalse_WhenUrlIsMissing()
    {
        var ok = HistoricalImageryLayerRequestFactory.TryCreate(
            "2024-03-18",
            "Wayback 2024-03-18",
            string.Empty,
            321,
            out var request,
            out var errorMessage);

        Assert.False(ok);
        Assert.Null(request);
        Assert.Equal("所选历史影像版本没有可用的图层地址。", errorMessage);
    }

    [Fact]
    public void TryCreate_FallsBackToItemTitleAndVersionId_WhenDateIsMissing()
    {
        var ok = HistoricalImageryLayerRequestFactory.TryCreate(
            string.Empty,
            string.Empty,
            "https://example.com/MapServer/tile/{z}/{y}/{x}",
            654,
            out var request,
            out _);

        Assert.True(ok);
        Assert.NotNull(request);
        Assert.Equal("Wayback 654", request!.LayerName);
    }

    [Fact]
    public void TryCreate_NormalizesWaybackLayerHost()
    {
        var ok = HistoricalImageryLayerRequestFactory.TryCreate(
            "2024-03-18",
            "Wayback 2024-03-18",
            "https://wayback-a.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/64001/{level}/{row}/{col}",
            64001,
            out var request,
            out _);

        Assert.True(ok);
        Assert.NotNull(request);
        Assert.Equal(
            "https://wayback-b.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/64001/%7Blevel%7D/%7Brow%7D/%7Bcol%7D",
            request!.LayerUri.AbsoluteUri);
    }
}
