using Xunit;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalVersionSelectionSummaryBuilderTests
{
    [Fact]
    public void Build_ReturnsNotQueriedWhenEmpty()
    {
        var summary = HistoricalVersionSelectionSummaryBuilder.Build(0, 0);

        Assert.Equal("未查询下载时间", summary);
    }

    [Fact]
    public void Build_ReturnsUncheckedSummaryWhenNothingSelected()
    {
        var summary = HistoricalVersionSelectionSummaryBuilder.Build(5, 0);

        Assert.Equal("共 5 个时间，未勾选", summary);
    }

    [Fact]
    public void Build_ReturnsCheckedSummaryWhenSelected()
    {
        var summary = HistoricalVersionSelectionSummaryBuilder.Build(5, 2);

        Assert.Equal("已勾选 2 个时间，共 5 个", summary);
    }
}
