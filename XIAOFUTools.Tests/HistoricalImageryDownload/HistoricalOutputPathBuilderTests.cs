using XIAOFUTools.Tools.HistoricalImageryDownload;
using XIAOFUTools.Tools.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalOutputPathBuilderTests
{
    [Fact]
    public void BuildForFolder_UsesProviderDateAndZoomInFileName()
    {
        var version = new HistoricalVersionItem
        {
            Provider = HistoricalImageryProviderType.Wayback,
            DisplayDate = "2024-03-01"
        };

        var path = HistoricalOutputPathBuilder.BuildForFolder(@"D:\Output", version, 18);

        Assert.Equal(@"D:\Output\Wayback_2024-03-01_Z18.tif", path);
    }

    [Fact]
    public void BuildForFolder_PrefersAcquisitionDateWhenAvailable()
    {
        var version = new HistoricalVersionItem
        {
            Provider = HistoricalImageryProviderType.GoogleEarth,
            DisplayDate = "2024-03-01",
            AcquisitionDate = "2024-02-26"
        };

        var path = HistoricalOutputPathBuilder.BuildForFolder(@"D:\Output", version, 19);

        Assert.Equal(@"D:\Output\Google_2024-02-26_Z19.tif", path);
    }
}
