using System.Text.Json;
using DuckDB.NET.Data;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class OvertureMfcGeneratorTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Generator_InspectsLocalParquetAndWritesCompatibleMfc()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"xiaofutools-mfc-generator-{Guid.NewGuid():N}");
        var dataDirectory = Path.Combine(directory, "data");
        var datasetDirectory = Path.Combine(dataDirectory, "address");
        var parquetPath = Path.Combine(datasetDirectory, "part-000.parquet");
        var outputPath = Path.Combine(directory, "connections", "overture.mfc");
        Directory.CreateDirectory(datasetDirectory);

        try
        {
            await CreateParquetAsync(parquetPath);
            using var connection = new DuckDBConnection("DataSource=:memory:");
            await using var inspector = new OvertureMfcDuckDbInspector(
                connection,
                new OpenConnectionInitializer(connection));
            var generator = new OvertureMfcGenerator(
                inspector,
                new OvertureMfcJsonWriter());

            var generated = await generator.GenerateAsync(
                dataDirectory,
                outputPath,
                log: null,
                CancellationToken.None);

            Assert.True(generated);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var dataset = document.RootElement.GetProperty("datasets")[0];
            Assert.Equal("address", dataset.GetProperty("name").GetString());
            Assert.Equal("String", dataset.GetProperty("fields")[0].GetProperty("type").GetString());
            Assert.Equal("bbox_xmin", dataset.GetProperty("fields")[1].GetProperty("name").GetString());
            Assert.False(dataset.TryGetProperty("geometry", out _));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task CreateParquetAsync(string parquetPath)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE source(id VARCHAR, country VARCHAR, version BIGINT); " +
            "INSERT INTO source VALUES ('a-1', 'CN', 1); " +
            $"COPY source TO '{parquetPath.Replace("'", "''", StringComparison.Ordinal)}' " +
            "(FORMAT 'PARQUET');";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class OpenConnectionInitializer(DuckDBConnection connection)
        : IOvertureDuckDbInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            return connection.OpenAsync(cancellationToken);
        }
    }
}
