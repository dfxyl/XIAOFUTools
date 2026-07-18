using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Application
{
    internal interface IOvertureDuckDbInitializer
    {
        Task InitializeAsync(CancellationToken cancellationToken);
    }

    internal interface IOvertureDuckDbCommandExecutor
    {
        Task OpenAsync(CancellationToken cancellationToken);

        Task ExecuteNonQueryAsync(string commandText, CancellationToken cancellationToken);
    }

    internal interface IOvertureDuckDbExtensionCatalog
    {
        IReadOnlyList<string> GetExtensionFiles(string extensionDirectory);
    }

    internal interface IOvertureDuckDbDataSession
    {
        Task<OvertureIngestResult> IngestAsync(
            string parquetPath,
            OvertureExtent? extent,
            CancellationToken cancellationToken);

        Task<DataTable> GetPreviewDataAsync(CancellationToken cancellationToken);

        Task<IReadOnlyList<string>> GetGeometryTypesAsync(
            CancellationToken cancellationToken);

        Task ExportGeoParquetAsync(
            string selectQuery,
            string outputPath,
            CancellationToken cancellationToken);
    }

    internal interface IOvertureGeoParquetExporter
    {
        Task<string> ExportAsync(
            string selectQuery,
            string outputPath,
            string layerName,
            IProgress<string>? progress,
            CancellationToken cancellationToken);
    }

    internal interface IOvertureMfcGenerator
    {
        Task<bool> GenerateAsync(
            string sourceDataFolder,
            string outputMfcFilePath,
            Action<string>? log,
            CancellationToken cancellationToken);
    }

    internal interface IOvertureMfcDuckDbInspector : IAsyncDisposable
    {
        Task InitializeAsync(CancellationToken cancellationToken);

        Task<OvertureMfcDatasetInspection> InspectDatasetAsync(
            string datasetName,
            IReadOnlyList<string> parquetFiles,
            CancellationToken cancellationToken);
    }

    internal interface IOvertureMfcJsonWriter
    {
        Task WriteAsync(
            OvertureMfcDefinition definition,
            string outputPath,
            CancellationToken cancellationToken);
    }
}
