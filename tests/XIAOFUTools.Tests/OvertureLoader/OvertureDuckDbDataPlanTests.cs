using System.Globalization;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class OvertureDuckDbDataPlanTests
{
    [Fact]
    public void IngestSql_UsesInvariantExtentAndEscapesPath()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var sql = OvertureDuckDbDataPlan.BuildIngestSql(
                "s3://bucket/o'brien.parquet",
                new OvertureExtent(1.25, 2.5, 3.75, 4.125));

            Assert.Contains("o''brien.parquet", sql, StringComparison.Ordinal);
            Assert.Contains("bbox.xmin >= 1.25", sql, StringComparison.Ordinal);
            Assert.Contains("bbox.ymax <= 4.125", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("1,25", sql, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void GeometryAndCopySql_PreserveExportContract()
    {
        var selectSql = OvertureDuckDbDataPlan.BuildGeometrySelectSql("MULTIPOLYGON");
        var copySql = OvertureDuckDbDataPlan.BuildGeoParquetCopySql(
            selectSql,
            Path.Combine(Path.GetTempPath(), "o'brien.parquet"));

        Assert.Contains("SELECT * EXCLUDE geometry, geometry", selectSql, StringComparison.Ordinal);
        Assert.Contains("ST_GeometryType(geometry) = 'MULTIPOLYGON'", selectSql, StringComparison.Ordinal);
        Assert.Contains("FORMAT 'PARQUET'", copySql, StringComparison.Ordinal);
        Assert.Contains("ROW_GROUP_SIZE 100000", copySql, StringComparison.Ordinal);
        Assert.Contains("COMPRESSION 'ZSTD'", copySql, StringComparison.Ordinal);
        Assert.Contains("o''brien.parquet", copySql, StringComparison.Ordinal);
    }

    [Fact]
    public void Extent_RejectsNonFiniteCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OvertureDuckDbDataPlan.BuildIngestSql(
                "data.parquet",
                new OvertureExtent(double.NaN, 0, 1, 1)));
    }
}
