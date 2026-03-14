using XIAOFUTools.Tools.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalRasterExportOptionsTests
{
    [Fact]
    public void CreateCopyOptions_UsesLosslessCompression()
    {
        var options = HistoricalRasterExportOptions.CreateCopyOptions();

        Assert.Contains("COMPRESS=DEFLATE", options);
        Assert.Contains("PREDICTOR=2", options);
        Assert.DoesNotContain("COMPRESS=JPEG", options);
        Assert.DoesNotContain("PHOTOMETRIC=YCBCR", options);
    }

    [Fact]
    public void CreateWarpParameters_UsesImageFriendlyResamplingAndLosslessCompression()
    {
        var options = HistoricalRasterExportOptions.CreateWarpParameters("EPSG:3857", "EPSG:4490");

        Assert.Contains("COMPRESS=DEFLATE", options);
        Assert.Contains("PREDICTOR=2", options);
        Assert.Contains("cubic", options);
        Assert.DoesNotContain("COMPRESS=JPEG", options);
        Assert.DoesNotContain("PHOTOMETRIC=YCBCR", options);
        Assert.DoesNotContain("near", options);
    }
}
