using XIAOFUTools.Tools.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class WaybackLiveProviderTests
{
    [Fact]
    public async Task QueryVersionsAtBeijing_ReturnsVersions()
    {
        var provider = new WaybackHistoricalImageryProvider();

        var historyVersions = await provider.QueryVersionsAsync(new HistoricalVersionQuery
        {
            Longitude = 116.391,
            Latitude = 39.907,
            ZoomLevel = 18
        });

        var allVersions = await provider.QueryVersionsAsync(new HistoricalVersionQuery
        {
            Longitude = 116.391,
            Latitude = 39.907,
            ZoomLevel = 18,
            IncludeAllVersions = true
        });

        Assert.NotEmpty(historyVersions);
        Assert.NotEmpty(allVersions);
        Assert.True(allVersions.Count >= historyVersions.Count);
        Assert.All(historyVersions, version => Assert.Equal(version.VersionId, version.ChangeKey));
    }
}
