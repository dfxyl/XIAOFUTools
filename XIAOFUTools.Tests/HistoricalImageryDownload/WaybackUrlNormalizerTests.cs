using XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class WaybackUrlNormalizerTests
{
    [Fact]
    public void Normalize_RewritesAnyWaybackHostToPreferredHost()
    {
        var normalized = WaybackUrlNormalizer.Normalize(
            "https://wayback-b.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/MapServer/tilemap/64001/18/1/2?f=json",
            "wayback-a.maptiles.arcgis.com");

        Assert.Equal(
            "https://wayback-a.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/MapServer/tilemap/64001/18/1/2?f=json",
            normalized);
    }

    [Fact]
    public void AreEquivalent_TreatsWaybackAAndBAsSameResource()
    {
        var matched = WaybackUrlNormalizer.AreEquivalent(
            "https://wayback-a.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/52304/{level}/{row}/{col}",
            "https://wayback-b.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/WMTS/1.0.0/default028mm/MapServer/tile/52304/{level}/{row}/{col}");

        Assert.True(matched);
    }
}
