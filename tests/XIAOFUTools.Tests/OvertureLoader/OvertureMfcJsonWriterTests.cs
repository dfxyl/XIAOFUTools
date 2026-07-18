using System.Text.Json;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class OvertureMfcJsonWriterTests
{
    [Fact]
    public async Task WriteAsync_PreservesMfcJsonPropertyNamesAndOmitsNullGeometry()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"xiaofutools-mfc-json-{Guid.NewGuid():N}");
        var outputPath = Path.Combine(directory, "connection.mfc");
        var definition = new OvertureMfcDefinition
        {
            Connection = new OvertureMfcConnection
            {
                Properties = new OvertureMfcConnectionProperties
                {
                    Path = @"C:\Overture\Data"
                }
            }
        };
        definition.Datasets.Add(new OvertureMfcDataset
        {
            Name = "address",
            Alias = "address",
            Fields = [new OvertureMfcField("id", "String")]
        });

        try
        {
            await new OvertureMfcJsonWriter().WriteAsync(
                definition,
                outputPath,
                CancellationToken.None);

            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            var root = document.RootElement;
            Assert.Equal("filesystem", root.GetProperty("connection").GetProperty("type").GetString());
            Assert.Equal(
                @"C:\Overture\Data",
                root.GetProperty("connection").GetProperty("properties").GetProperty("path").GetString());
            var dataset = root.GetProperty("datasets")[0];
            Assert.Equal("parquet", dataset.GetProperty("properties").GetProperty("fileformat").GetString());
            Assert.Equal("String", dataset.GetProperty("fields")[0].GetProperty("type").GetString());
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
}
