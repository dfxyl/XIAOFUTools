using XIAOFUTools.Features.Analysis.IntersectSummary;
using XIAOFUTools.Features.Analysis.IntersectSummary.Core;

namespace XIAOFUTools.Tests.IntersectSummary;

public sealed class IntersectSummaryTableBuilderTests
{
    [Fact]
    public void Build_AppliesAliasesLargestRemainderAndTotalRow()
    {
        var results = new[]
        {
            CreateResult("A区", "工业", 1.005),
            CreateResult("A区", "仓储", 2.005)
        };

        var table = IntersectSummaryTableBuilder.Build(
            results,
            new[] { "region" },
            new[] { "class" },
            new Dictionary<string, string> { ["region"] = "区域名称" },
            new Dictionary<string, string> { ["class"] = "地类名称" },
            "平方米",
            2);

        Assert.Equal(new[] { "区域名称", "地类名称", "面积(平方米)" },
            table.Columns.Cast<System.Data.DataColumn>().Select(column => column.ColumnName));
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(1.01, table.Rows[0][2]);
        Assert.Equal(2.00, table.Rows[1][2]);
        Assert.Equal("合计", table.Rows[2][0]);
        Assert.Equal(3.01, table.Rows[2][2]);
    }

    [Fact]
    public void ApplyLargestRemainderMethod_BalancesEachRegionIndependently()
    {
        var results = new[]
        {
            CreateResult("A区", "一类", 0.006),
            CreateResult("A区", "二类", 0.006),
            CreateResult("B区", "一类", 0.006),
            CreateResult("B区", "二类", 0.006)
        };

        var adjusted = IntersectSummaryTableBuilder.ApplyLargestRemainderMethod(
            results,
            new[] { "region" },
            "平方米",
            2);

        Assert.Equal(0.01, adjusted[0]);
        Assert.Equal(0.00, adjusted[1]);
        Assert.Equal(0.01, adjusted[2]);
        Assert.Equal(0.00, adjusted[3]);
    }

    private static IntersectSummaryResultItem CreateResult(string region, string landClass, double area)
    {
        return new IntersectSummaryResultItem
        {
            RegionValues = new Dictionary<string, object> { ["region"] = region },
            ClassValues = new Dictionary<string, object> { ["class"] = landClass },
            Area = area,
            AdjustedArea = area
        };
    }
}
