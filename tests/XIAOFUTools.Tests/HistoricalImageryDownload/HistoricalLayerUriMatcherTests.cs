using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class HistoricalLayerUriMatcherTests
{
    [Fact]
    public void IsOutputFamilyMatch_MatchesExactPath()
    {
        var matched = HistoricalLayerUriMatcher.IsOutputFamilyMatch(
            @"D:\Output\Google_2025-05-20_Z19.tif",
            @"D:\Output\Google_2025-05-20_Z19.tif");

        Assert.True(matched);
    }

    [Fact]
    public void IsOutputFamilyMatch_MatchesResolvedDateSibling()
    {
        var matched = HistoricalLayerUriMatcher.IsOutputFamilyMatch(
            @"D:\Output\Google_2025-05-20_Z19.tif",
            @"D:\Output\Google_2025-05-18_Z19.tif");

        Assert.True(matched);
    }

    [Fact]
    public void IsOutputFamilyMatch_RejectsDifferentZoomSibling()
    {
        var matched = HistoricalLayerUriMatcher.IsOutputFamilyMatch(
            @"D:\Output\Google_2025-05-20_Z19.tif",
            @"D:\Output\Google_2025-05-18_Z18.tif");

        Assert.False(matched);
    }
}
