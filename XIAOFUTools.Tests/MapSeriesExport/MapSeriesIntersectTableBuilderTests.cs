using XIAOFUTools.Tools.Output.MapSeriesExport;

namespace XIAOFUTools.Tests.MapSeriesExport;

public sealed class MapSeriesIntersectTableBuilderTests
{
    [Fact]
    public void BuildRows_AddsOtherAndTotalWithRoundedAreas()
    {
        var rows = MapSeriesIntersectTableBuilder.BuildRows(
            new[]
            {
                new MapSeriesIntersectArea("林地", 100.0),
                new MapSeriesIntersectArea("林地", 25.0),
                new MapSeriesIntersectArea("耕地", 50.0)
            },
            redlineArea: 200.0,
            areaUnit: "平方米",
            decimalPlaces: 2);

        Assert.Equal(4, rows.Count);
        Assert.Equal("林地", rows[0].Category);
        Assert.Equal(125.0, rows[0].Area);
        Assert.Equal("耕地", rows[1].Category);
        Assert.Equal(50.0, rows[1].Area);
        Assert.Equal("其他", rows[2].Category);
        Assert.Equal(25.0, rows[2].Area);
        Assert.Equal("合计", rows[3].Category);
        Assert.Equal(200.0, rows[3].Area);
    }

    [Fact]
    public void BuildRows_ScalesOverlappedAreasToCurrentRedlineArea()
    {
        var rows = MapSeriesIntersectTableBuilder.BuildRows(
            new[]
            {
                new MapSeriesIntersectArea("建设用地", 80.0),
                new MapSeriesIntersectArea("农用地", 80.0)
            },
            redlineArea: 100.0,
            areaUnit: "平方米",
            decimalPlaces: 2);

        Assert.Equal(3, rows.Count);
        Assert.Equal(50.0, rows[0].Area);
        Assert.Equal(50.0, rows[1].Area);
        Assert.Equal(100.0, rows[2].Area);
    }

    [Fact]
    public void BuildRows_DoesNotDisplayZeroOther()
    {
        var rows = MapSeriesIntersectTableBuilder.BuildRows(
            new[]
            {
                new MapSeriesIntersectArea("林地", 40.0),
                new MapSeriesIntersectArea("耕地", 60.0)
            },
            redlineArea: 100.0,
            areaUnit: "平方米",
            decimalPlaces: 2);

        Assert.DoesNotContain(rows, row => row.Category == "其他");
        Assert.Equal(3, rows.Count);
        Assert.Equal(100.0, rows.Last().Area);
    }

    [Fact]
    public void BuildRows_MergesOneMinimumUnitOtherIntoLargestNextDigitRemainder()
    {
        var rows = MapSeriesIntersectTableBuilder.BuildRows(
            new[]
            {
                new MapSeriesIntersectArea("林地", 50.112),
                new MapSeriesIntersectArea("耕地", 49.883)
            },
            redlineArea: 100.0,
            areaUnit: "平方米",
            decimalPlaces: 2);

        Assert.DoesNotContain(rows, row => row.Category == "其他");
        Assert.Equal("林地", rows[0].Category);
        Assert.Equal(50.11, rows[0].Area);
        Assert.Equal("耕地", rows[1].Category);
        Assert.Equal(49.89, rows[1].Area);
        Assert.Equal("合计", rows[2].Category);
        Assert.Equal(100.0, rows[2].Area);
    }
}
