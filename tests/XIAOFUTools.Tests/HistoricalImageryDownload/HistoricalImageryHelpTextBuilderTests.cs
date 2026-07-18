using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalImageryHelpTextBuilderTests
{
    [Fact]
    public void Build_ContainsHistoricalDownloadUsageAndFallbackModes()
    {
        var text = HistoricalImageryHelpTextBuilder.Build();

        Assert.Contains("历史影像下载工具说明", text);
        Assert.Contains("查询列表", text);
        Assert.Contains("缺失处理", text);
        Assert.Contains("分别输出", text);
        Assert.Contains("混合日期", text);
        Assert.Contains("默认使用“分别输出”", text);
        Assert.Contains("GeoTIFF", text);
    }
}
