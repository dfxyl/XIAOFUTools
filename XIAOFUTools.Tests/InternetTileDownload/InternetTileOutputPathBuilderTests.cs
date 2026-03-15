using System.IO;
using XIAOFUTools.Tools.InternetTileDownload.Services;
using Xunit;

namespace XIAOFUTools.Tests.InternetTileDownload;

public class InternetTileOutputPathBuilderTests
{
    [Fact]
    public void BuildForFolder_UsesHostAndLevelInFileName()
    {
        var path = InternetTileOutputPathBuilder.BuildForFolder(
            @"D:\Output",
            "https://t0.tianditu.gov.cn/img_w/wmts?SERVICE=WMTS&REQUEST=GetTile",
            "18");

        Assert.Equal(Path.Combine(@"D:\Output", "t0_tianditu_gov_cn_L18.tif"), path);
    }
}
