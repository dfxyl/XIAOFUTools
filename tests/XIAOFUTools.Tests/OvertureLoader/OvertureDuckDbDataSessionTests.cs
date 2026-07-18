using DuckDB.NET.Data;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class OvertureDuckDbDataSessionTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Session_ExportsIngestsAndPreviewsLocalParquet()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"xiaofutools-duckdb-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var parquetPath = Path.Combine(directory, "source.parquet");

        try
        {
            using var connection = new DuckDBConnection("DataSource=:memory:");
            await connection.OpenAsync();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE source(id INTEGER, name VARCHAR); " +
                                      "INSERT INTO source VALUES (1, 'A'), (2, 'B');";
                await command.ExecuteNonQueryAsync();
            }

            var session = new OvertureDuckDbDataSession(connection);
            await session.ExportGeoParquetAsync(
                "SELECT * FROM source",
                parquetPath,
                CancellationToken.None);
            var ingestResult = await session.IngestAsync(
                parquetPath,
                null,
                CancellationToken.None);
            var preview = await session.GetPreviewDataAsync(CancellationToken.None);

            Assert.Equal(2, ingestResult.RowCount);
            Assert.Equal(3, ingestResult.ColumnCount);
            Assert.True(ingestResult.HasRows);
            Assert.Equal(2, preview.Rows.Count);
            Assert.Equal("A", preview.Rows[0]["name"]);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
