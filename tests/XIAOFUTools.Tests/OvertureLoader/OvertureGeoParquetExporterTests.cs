using System.Data;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;

namespace XIAOFUTools.Tests.OvertureLoader;

public sealed class OvertureGeoParquetExporterTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Exporter_ReleasesExistingMapMemberAndReplacesFile()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"xiaofutools-geoparquet-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, "land.parquet");
        await File.WriteAllTextAsync(outputPath, "old");
        var session = new WritingDataSession();
        var cleanup = new RecordingMapCleanupService();
        var exporter = new OvertureGeoParquetExporter(session, cleanup);

        try
        {
            var actualPath = await exporter.ExportAsync(
                "SELECT 1",
                outputPath,
                "land",
                progress: null,
                CancellationToken.None);

            Assert.Equal(outputPath, actualPath);
            Assert.Equal(outputPath, cleanup.FilePath);
            Assert.Equal("new", await File.ReadAllTextAsync(outputPath));
            Assert.Equal("SELECT 1", session.SelectQuery);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class WritingDataSession : IOvertureDuckDbDataSession
    {
        public string? SelectQuery { get; private set; }

        public Task<OvertureIngestResult> IngestAsync(
            string parquetPath,
            OvertureExtent? extent,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DataTable> GetPreviewDataAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<string>> GetGeometryTypesAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public async Task ExportGeoParquetAsync(
            string selectQuery,
            string outputPath,
            CancellationToken cancellationToken)
        {
            SelectQuery = selectQuery;
            await File.WriteAllTextAsync(outputPath, "new", cancellationToken);
        }
    }

    private sealed class RecordingMapCleanupService : IOvertureMapMemberCleanupService
    {
        public string? FilePath { get; private set; }

        public Task<int> RemoveFromActiveMapUsingFolderAsync(
            string folderPath,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> RemoveFromProjectMapsUsingFileAsync(
            string filePath,
            CancellationToken cancellationToken)
        {
            FilePath = filePath;
            return Task.FromResult(1);
        }
    }
}
