using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

[Trait("Category", "LiveNetwork")]
public class WaybackLiveProviderTests
{
    [Fact]
    public async Task QueryVersionsAtBeijing_ReturnsVersions()
    {
        var provider = new WaybackHistoricalImageryProvider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var historyVersions = await provider.QueryVersionsAsync(new HistoricalVersionQuery
        {
            Longitude = 116.391,
            Latitude = 39.907,
            ZoomLevel = 18
        }, timeout.Token);

        var allVersions = await provider.QueryVersionsAsync(new HistoricalVersionQuery
        {
            Longitude = 116.391,
            Latitude = 39.907,
            ZoomLevel = 18,
            IncludeAllVersions = true
        }, timeout.Token);

        Assert.NotEmpty(historyVersions);
        Assert.NotEmpty(allVersions);
        Assert.True(allVersions.Count >= historyVersions.Count);
        Assert.All(historyVersions, version => Assert.Equal(version.VersionId, version.ChangeKey));
    }
}
