using XIAOFUTools.Features.Analysis.LandClassTable;
using XIAOFUTools.Features.Analysis.LandClassTable.Core;

namespace XIAOFUTools.Tests.LandClassTable;

public sealed class LandClassTableBuilderTests
{
    [Fact]
    public void BuildRightHolderName_RemovesBlankAndDuplicateValuesWhilePreservingSourceOrder()
    {
        string value = LandClassReportHeaderBuilder.BuildRightHolderName(new[]
        {
            " 甲单位 ",
            string.Empty,
            "乙单位",
            "甲单位",
            "  ",
            "丙单位"
        });

        Assert.Equal("甲单位、乙单位、丙单位", value);
    }

    [Fact]
    public void BuildTable_MapsLandClassNamesAndCreatesParentTotals()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 100.0) },
            new[]
            {
                new LandClassIntersectionArea("项目A", "果园", 25.0),
                new LandClassIntersectionArea("项目A", "乔木林地", 75.0)
            },
            decimalPlaces: 2);

        Assert.Contains(table.Columns, c => c.HeaderText == "农用地" && c.Level == LandClassTableColumnLevel.TopGroup);
        Assert.Contains(table.Columns, c => c.HeaderText == "果园" && c.Code == "0201");
        Assert.Contains(table.Columns, c => c.HeaderText == "乔木林地" && c.Code == "0301");
        Assert.DoesNotContain(table.Columns, c => c.Key == "AGR_PLANTATION");

        var row = Assert.Single(table.Rows);
        Assert.Equal(100.0, row.TotalArea);
        Assert.Equal(100.0, row.Values["AGRICULTURAL_TOTAL"]);
        Assert.Equal(25.0, row.Values["0201"]);
        Assert.Equal(75.0, row.Values["0301"]);
    }

    [Fact]
    public void BuildTable_MatchesClassCodeInsideText()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 40.0) },
            new[] { new LandClassIntersectionArea("项目A", "0204K 可调整其他园地", 40.0) },
            decimalPlaces: 2);

        var row = Assert.Single(table.Rows);
        Assert.Contains(table.Columns, c => c.HeaderText == "其他园地" && c.Code == "0204");
        Assert.Equal(40.0, row.Values["0204"]);
    }

    [Fact]
    public void BuildTable_ScalesIntersectionsToSelectedProjectArea()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 60.0) },
            new[]
            {
                new LandClassIntersectionArea("项目A", "果园", 30.0),
                new LandClassIntersectionArea("项目A", "乔木林地", 70.0)
            },
            decimalPlaces: 2);

        var row = Assert.Single(table.Rows);
        Assert.Equal(18.0, row.Values["0201"]);
        Assert.Equal(42.0, row.Values["0301"]);
        Assert.Equal(60.0, row.Values["AGRICULTURAL_TOTAL"]);
    }

    [Fact]
    public void BuildTable_KeepsSiblingLandClassColumnWhenOnlyOneLeafClassExists()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 100.0) },
            new[] { new LandClassIntersectionArea("项目A", "村庄", 100.0) },
            decimalPlaces: 2);

        Assert.Contains(table.Columns, c => c.HeaderText == "城市" && c.Code == "201");
        Assert.Contains(table.Columns, c => c.HeaderText == "村庄" && c.Code == "203");

        var row = Assert.Single(table.Rows);
        Assert.False(row.Values.ContainsKey("201"));
        Assert.Equal(100.0, row.Values["203"]);
    }

    [Fact]
    public void BuildTable_KeepsFarmlandColumnsWhenAgriculturalAreaIsZero()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 100.0) },
            new[] { new LandClassIntersectionArea("项目A", "村庄", 100.0) },
            decimalPlaces: 2);

        Assert.Contains(table.Columns, c => c.Key == "AGRICULTURAL_TOTAL");
        Assert.Contains(table.Columns, c => c.Key == "AGR_FARMLAND");
        Assert.Contains(table.Columns, c => c.Key == "0101");
        Assert.Contains(table.Columns, c => c.Key == "0103");
    }

    [Fact]
    public void BuildTable_KeepsCityAndVillageColumnsByDefault()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 100.0) },
            new[] { new LandClassIntersectionArea("项目A", "水田", 100.0) },
            decimalPlaces: 2);

        Assert.Contains(table.Columns, c => c.Key == "CONSTRUCTION_TOTAL");
        Assert.Contains(table.Columns, c => c.Key == "CON_URBAN");
        Assert.Contains(table.Columns, c => c.Key == "201");
        Assert.Contains(table.Columns, c => c.Key == "203");
    }

    [Fact]
    public void BuildTable_UsesLandClassOwnershipFieldsForRows()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 100.0) },
            new[]
            {
                new LandClassIntersectionArea("项目A", "村庄", 30.0, "甲村", "30"),
                new LandClassIntersectionArea("项目A", "城市", 70.0, "乙单位", "20")
            },
            decimalPlaces: 2);

        Assert.Equal(2, table.Rows.Count);
        Assert.Contains(table.Rows, x => x.OwnerUnitName == "甲村" && x.OwnerNatureName == "集体" && x.Values["203"] == 30.0);
        Assert.Contains(table.Rows, x => x.OwnerUnitName == "乙单位" && x.OwnerNatureName == "国有" && x.Values["201"] == 70.0);
    }

    [Fact]
    public void BuildTable_DoesNotKeepSecondGroupSubtotalWhenOnlyOneLeafExists()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 100.0) },
            new[] { new LandClassIntersectionArea("项目A", "农村道路", 100.0) },
            decimalPlaces: 2);

        Assert.Contains(table.Columns, c => c.Key == "1006");
        Assert.DoesNotContain(table.Columns, c => c.Key == "AGR_TRANSPORT");
    }

    [Fact]
    public void BuildTable_AdjustsRoundedLeafAreasToMatchProjectTotal()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 1.0) },
            new[]
            {
                new LandClassIntersectionArea("项目A", "水田", 1.0),
                new LandClassIntersectionArea("项目A", "旱地", 1.0),
                new LandClassIntersectionArea("项目A", "果园", 1.0)
            },
            decimalPlaces: 2);

        var row = Assert.Single(table.Rows);
        double leafTotal = row.Values["0101"] + row.Values["0103"] + row.Values["0201"];
        Assert.Equal(1.0, leafTotal);
        Assert.Equal(1.0, row.Values["AGRICULTURAL_TOTAL"]);
    }

    [Fact]
    public void BuildTable_UsesPlotNameToKeepRowsSeparate()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 10.0, "地块1") },
            new[]
            {
                new LandClassIntersectionArea("项目A", "水田", 4.0, PlotName: "地块1"),
                new LandClassIntersectionArea("项目A", "旱地", 6.0, PlotName: "地块1")
            },
            decimalPlaces: 2);

        var row = Assert.Single(table.Rows);
        Assert.Equal("地块1", row.PlotName);
        Assert.Equal(10.0, row.TotalArea);
    }

    [Fact]
    public void GetPlotNameMergeRanges_MergesOnlyContiguousRowsFromTheSameProjectAndPlot()
    {
        var rows = new[]
        {
            new LandClassTableRow("项目A", "地块1", "甲单位", "集体", 10, new Dictionary<string, double>()),
            new LandClassTableRow("项目A", "地块1", "乙单位", "国有", 20, new Dictionary<string, double>()),
            new LandClassTableRow("项目A", "地块2", "丙单位", "国有", 30, new Dictionary<string, double>()),
            new LandClassTableRow("项目B", "地块1", "丁单位", "集体", 40, new Dictionary<string, double>())
        };

        var range = Assert.Single(LandClassTableRowGrouping.GetPlotNameMergeRanges(rows));

        Assert.Equal(0, range.StartIndex);
        Assert.Equal(1, range.EndIndex);
    }

    [Fact]
    public void BuildTable_KeepsGroupValueSeparateFromPlotName()
    {
        var table = LandClassTableBuilder.BuildTable(
            new[] { new LandClassProjectArea("项目A", 10.0, "地块1", "一组") },
            new[]
            {
                new LandClassIntersectionArea("项目A", "水田", 10.0, PlotName: "地块1", GroupValue: "一组")
            },
            decimalPlaces: 2);

        var row = Assert.Single(table.Rows);
        Assert.Equal("地块1", row.PlotName);
        Assert.Equal(10.0, row.Values["0101"]);
    }

    [Theory]
    [InlineData("其中可调整人工牧草地", "0403")]
    [InlineData("0403K", "0403")]
    [InlineData("养殖坑塘", "1104")]
    [InlineData("1104K", "1104")]
    [InlineData("1104A", "1104")]
    [InlineData("DLBM=1104K", "1104")]
    public void ResolveDefinition_TrimsAdjustableSuffixToMainLandClass(string value, string expectedCode)
    {
        var definition = LandClassTableBuilder.ResolveDefinition(value);

        Assert.NotNull(definition);
        Assert.Equal(expectedCode, definition.Code);
    }
}
