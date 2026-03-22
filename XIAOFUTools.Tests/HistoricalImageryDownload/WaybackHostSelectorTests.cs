using System;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class WaybackHostSelectorTests
{
    [Fact]
    public async Task SelectPreferredHostAsync_ReturnsFastestAvailableHost()
    {
        var host = await WaybackHostSelector.SelectPreferredHostAsync(
            ["wayback-a.maptiles.arcgis.com", "wayback-b.maptiles.arcgis.com"],
            (candidate, _) => Task.FromResult<TimeSpan?>(candidate.Contains("-a", StringComparison.Ordinal) ? TimeSpan.FromMilliseconds(10) : TimeSpan.FromMilliseconds(40)),
            CancellationToken.None);

        Assert.Equal("wayback-a.maptiles.arcgis.com", host);
    }

    [Fact]
    public async Task SelectPreferredHostAsync_FallsBackToHealthyHostWhenAnotherHostFails()
    {
        var host = await WaybackHostSelector.SelectPreferredHostAsync(
            ["wayback-a.maptiles.arcgis.com", "wayback-b.maptiles.arcgis.com"],
            (candidate, _) => Task.FromResult<TimeSpan?>(candidate.Contains("-a", StringComparison.Ordinal) ? null : TimeSpan.FromMilliseconds(15)),
            CancellationToken.None);

        Assert.Equal("wayback-b.maptiles.arcgis.com", host);
    }
}
