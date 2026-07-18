using XIAOFUTools.Features.Conversion.TxtToFeature.Core;

namespace XIAOFUTools.Tests.TxtToFeature;

public sealed class TxtPlotParserTests
{
    [Fact]
    public void Parse_ReadsMultiplePlotsAttributesAndRings()
    {
        var lines = new[]
        {
            "[属性描述]",
            "坐标系=CGCS2000",
            "[地块坐标]",
            "DK001,一号地块,@",
            "J1,1,30.0,120.0",
            "J2,1,30.0,121.0",
            "J3,1,31.0,121.0",
            "DK002,二号地块,@",
            "J1,1,32.0,122.0",
            "J2,1,32.0,123.0",
            "J3,1,33.0,123.0"
        };

        var plots = TxtPlotParser.Parse(lines, "编号,名称,@", false);

        Assert.Equal(2, plots.Count);
        Assert.Equal("DK001", plots[0].Attributes["编号"]);
        Assert.Equal("一号地块", plots[0].Attributes["名称"]);
        Assert.Single(plots[0].Rings);
        Assert.Equal(3, plots[0].Rings[0].Points.Count);
        Assert.Equal(120.0, plots[0].Rings[0].Points[0].X);
        Assert.Equal(30.0, plots[0].Rings[0].Points[0].Y);
    }

    [Fact]
    public void Parse_SwapsCoordinatesAndSupportsNegativeValues()
    {
        var lines = new[]
        {
            "[地块坐标]",
            "DK001,@",
            "P1,2,-30.5,120.25",
            "P2,2,-30.6,120.35",
            "P3,2,-30.7,120.45"
        };

        var plots = TxtPlotParser.Parse(lines, "编号,@", true);

        var point = Assert.Single(plots).Rings.Single().Points[0];
        Assert.Equal(-30.5, point.X);
        Assert.Equal(120.25, point.Y);
        Assert.Equal(2, point.RingNumber);
    }

    [Fact]
    public void Parse_IgnoresIncompletePlotsAndCoordinatesOutsideSection()
    {
        var lines = new[]
        {
            "P0,1,20,100",
            "[地块坐标]",
            "EMPTY,@",
            "INVALID",
            "VALID,@",
            "P1,1,20,100",
            "P2,1,21,100",
            "P3,1,21,101"
        };

        var plots = TxtPlotParser.Parse(lines, "编号,@", false);

        var plot = Assert.Single(plots);
        Assert.Equal("VALID", plot.Attributes["编号"]);
    }
}
