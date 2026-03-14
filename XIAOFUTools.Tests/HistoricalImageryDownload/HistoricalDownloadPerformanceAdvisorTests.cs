using XIAOFUTools.Tools.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalDownloadPerformanceAdvisorTests
{
    [Fact]
    public void RecommendTileConcurrency_RespectsCpuTileCountAndPreciseClip()
    {
        var fast = HistoricalDownloadPerformanceAdvisor.RecommendTileConcurrency(cpuCount: 16, tileCount: 120, preciseClip: false);
        var clipped = HistoricalDownloadPerformanceAdvisor.RecommendTileConcurrency(cpuCount: 16, tileCount: 120, preciseClip: true);

        Assert.InRange(fast, 8, 24);
        Assert.InRange(clipped, 4, fast);
        Assert.True(fast >= clipped);
    }

    [Fact]
    public void Evaluate_BlocksHugeWorkload()
    {
        var evaluation = HistoricalDownloadPerformanceAdvisor.Evaluate(
            tilesPerVersion: 7000,
            versionCount: 3,
            preciseClip: true);

        Assert.True(evaluation.ShouldBlock);
        Assert.NotEmpty(evaluation.Messages);
    }

    [Fact]
    public void Evaluate_WarnsAndEnablesFastClipForLargePreciseClip()
    {
        var evaluation = HistoricalDownloadPerformanceAdvisor.Evaluate(
            tilesPerVersion: 400,
            versionCount: 2,
            preciseClip: true);

        Assert.False(evaluation.ShouldBlock);
        Assert.True(evaluation.ShouldWarn);
        Assert.True(evaluation.UseFastClip);
    }
}
