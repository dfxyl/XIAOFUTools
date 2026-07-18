using System;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Infrastructure.Google;

namespace XIAOFUTools.Tests.HistoricalImageryDownload;

public class GoogleQuadtreeCodecTests
{
    [Theory]
    [InlineData(0, 0, 1, "00")]
    [InlineData(1, 0, 1, "03")]
    [InlineData(0, 1, 1, "01")]
    [InlineData(1, 1, 1, "02")]
    public void CreatePath_ReturnsExpectedRootedQuadtreePath(int row, int column, int level, string expected)
    {
        var path = GoogleQuadtreeCodec.CreatePath(row, column, level);

        Assert.Equal(expected, path);
    }

    [Fact]
    public void EncodeDate_RoundTripsToOriginalDate()
    {
        var date = new DateOnly(2026, 2, 26);

        var encoded = GoogleQuadtreeCodec.EncodeDate(date);
        var decoded = GoogleQuadtreeCodec.DecodeDate(encoded);

        Assert.Equal(date, decoded);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("00", 1)]
    [InlineData("01", 2)]
    [InlineData("02", 3)]
    [InlineData("03", 4)]
    public void GetSubIndex_ReturnsExpectedValue(string path, int expected)
    {
        var subIndex = GoogleQuadtreeCodec.GetSubIndex(path);

        Assert.Equal(expected, subIndex);
    }
}
