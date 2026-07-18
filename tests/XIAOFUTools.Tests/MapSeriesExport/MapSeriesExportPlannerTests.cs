using XIAOFUTools.Features.Cartography.MapSeriesExport.Core;

namespace XIAOFUTools.Tests.MapSeriesExport;

public sealed class MapSeriesExportPlannerTests
{
    [Theory]
    [InlineData("1,3-5,8", 8, new[] { 1, 3, 4, 5, 8 })]
    [InlineData("5—3；2；20", 6, new[] { 2, 3, 4, 5 })]
    [InlineData("1～2，2，4", 4, new[] { 1, 2, 4 })]
    public void ParseRange_NormalizesSeparatorsBoundsAndDuplicates(
        string text,
        int pageCount,
        int[] expected)
    {
        Assert.Equal(expected, MapSeriesExportPlanner.ParseRange(text, pageCount));
    }

    [Fact]
    public void ResolvePageNumbers_SelectedModeFiltersAndSortsPages()
    {
        var pages = MapSeriesExportPlanner.ResolvePageNumbers(
            "选中导出",
            5,
            new[] { 5, 2, 2, 0, 8 },
            string.Empty);

        Assert.Equal(new[] { 2, 5 }, pages);
    }

    [Fact]
    public void CoordinateTableSettings_FormatsLabelsUsingPersistedOptions()
    {
        var settings = new CoordinateTableSettings
        {
            PointLabelPrefix = "J-",
            PointLabelSuffix = "号",
            EdgeLabelPrefix = "L=",
            EdgeLabelSuffix = "m",
            EdgeLabelDecimal = 2,
            EdgeLabelPadZeros = true
        };

        Assert.Equal("J-3号", settings.FormatPointLabel(3));
        Assert.Equal("L=2.30m", settings.FormatEdgeLabel(2.3));
    }
}
