using XIAOFUTools.Features.General.InternetTileDownload.Services;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class InternetTileHelpTextBuilderTests
{
    [Fact]
    public void Build_ContainsSupportedServiceTypes()
    {
        var text = InternetTileHelpTextBuilder.Build();

        Assert.Contains("WMTS", text);
        Assert.Contains("XYZ", text);
        Assert.Contains("当前视图", text);
        Assert.Contains("框选范围", text);
    }
}
