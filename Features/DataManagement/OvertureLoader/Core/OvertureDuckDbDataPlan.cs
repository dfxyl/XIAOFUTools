using System;
using System.Globalization;
using System.IO;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Core
{
    internal sealed record OvertureExtent(
        double XMin,
        double YMin,
        double XMax,
        double YMax);

    internal sealed record OvertureIngestResult(long RowCount, int ColumnCount)
    {
        public bool HasRows => RowCount > 0;
    }

    internal static class OvertureDuckDbDataPlan
    {
        public const string PreviewSql = "SELECT * FROM current_table LIMIT 1000";

        public const string GeometryTypesSql =
            "SELECT DISTINCT ST_GeometryType(geometry) as geom_type " +
            "FROM current_table WHERE geometry IS NOT NULL";

        public static string BuildSchemaProbeSql(string parquetPath)
        {
            return $@"
CREATE OR REPLACE TABLE temp AS
SELECT * FROM read_parquet('{EscapeLiteral(parquetPath)}', filename=true, hive_partitioning=1) LIMIT 0;";
        }

        public static string BuildIngestSql(string parquetPath, OvertureExtent? extent)
        {
            var spatialFilter = extent == null
                ? string.Empty
                : $@"
WHERE bbox.xmin >= {FormatNumber(extent.XMin)}
  AND bbox.ymin >= {FormatNumber(extent.YMin)}
  AND bbox.xmax <= {FormatNumber(extent.XMax)}
  AND bbox.ymax <= {FormatNumber(extent.YMax)}";
            return $@"
CREATE OR REPLACE TABLE current_table AS
SELECT *
FROM read_parquet('{EscapeLiteral(parquetPath)}', filename=true, hive_partitioning=1)
{spatialFilter};";
        }

        public static string BuildGeometrySelectSql(string geometryType)
        {
            if (string.IsNullOrWhiteSpace(geometryType))
            {
                throw new ArgumentException("几何类型不能为空。", nameof(geometryType));
            }

            return
                "SELECT * EXCLUDE geometry, geometry FROM current_table " +
                $"WHERE ST_GeometryType(geometry) = '{EscapeLiteral(geometryType)}'";
        }

        public static string BuildGeoParquetCopySql(string selectQuery, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(selectQuery))
            {
                throw new ArgumentException("导出查询不能为空。", nameof(selectQuery));
            }
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("输出路径不能为空。", nameof(outputPath));
            }

            var normalizedPath = Path.GetFullPath(outputPath).Replace('\\', '/');
            return $@"
COPY (
    {selectQuery}
) TO '{EscapeLiteral(normalizedPath)}'
WITH (
    FORMAT 'PARQUET',
    ROW_GROUP_SIZE 100000,
    COMPRESSION 'ZSTD'
);";
        }

        private static string EscapeLiteral(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("SQL 路径或值不能为空。", nameof(value));
            }

            return value.Replace("'", "''", StringComparison.Ordinal);
        }

        private static string FormatNumber(double value)
        {
            if (!double.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "范围坐标必须是有限数值。");
            }

            return value.ToString("G", CultureInfo.InvariantCulture);
        }
    }
}
