using XIAOFUTools.Tools.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalImageryHelpTextBuilderTests
{
    [Fact]
    public void Build_ContainsGoogleNetworkEnvironmentNotice()
    {
        var helpText = HistoricalImageryHelpTextBuilder.Build();

        Assert.Contains("Google 服务需要特殊网络环境", helpText);
    }

    [Fact]
    public void Build_ContainsHistoryAndAllModeDescriptions()
    {
        var helpText = HistoricalImageryHelpTextBuilder.Build();

        Assert.Contains("查询历史", helpText);
        Assert.Contains("查询全部", helpText);
        Assert.Contains("Wayback", helpText);
    }
}
