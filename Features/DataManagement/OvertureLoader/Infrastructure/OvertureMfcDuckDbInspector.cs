using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;
using XIAOFUTools.Shared.Diagnostics;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class OvertureMfcDuckDbInspector : IOvertureMfcDuckDbInspector
    {
        private readonly DuckDBConnection _connection;
        private readonly IOvertureDuckDbInitializer _initializer;
        private readonly Action<string> _log;
        private bool _initialized;

        internal OvertureMfcDuckDbInspector(
            DuckDBConnection connection,
            IOvertureDuckDbInitializer initializer,
            Action<string>? log = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
            _log = log ?? Console.WriteLine;
        }

        public static OvertureMfcDuckDbInspector CreateDefault(
            string assemblyLocation,
            Action<string>? log = null)
        {
            var connection = new DuckDBConnection("DataSource=:memory:");
            var appLogger = new AppLogger();
            if (log != null)
            {
                appLogger.EntryWritten += (_, entry) => log(entry.Message);
            }

            var initializer = new OvertureDuckDbInitializer(
                new DuckDbConnectionCommandExecutor(connection),
                new FileSystemOvertureDuckDbExtensionCatalog(),
                appLogger,
                assemblyLocation);
            return new OvertureMfcDuckDbInspector(connection, initializer, log);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (_initialized)
            {
                return;
            }

            await _initializer.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _initialized = true;
        }

        public async Task<OvertureMfcDatasetInspection> InspectDatasetAsync(
            string datasetName,
            IReadOnlyList<string> parquetFiles,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(datasetName);
            ArgumentNullException.ThrowIfNull(parquetFiles);
            if (parquetFiles.Count == 0)
            {
                throw new ArgumentException("Parquet 文件集合不能为空。", nameof(parquetFiles));
            }

            if (!_initialized)
            {
                throw new InvalidOperationException("MFC DuckDB 检查器尚未初始化。");
            }

            var sampleFile = parquetFiles[0];
            _log($"Using sample file for general schema: {NormalizePath(sampleFile)}");
            var columns = await ReadSchemaAsync(sampleFile, cancellationToken)
                .ConfigureAwait(false);
            var hasGeometry = columns.Any(column => column.Name.Equals(
                OvertureMfcSchemaPlanner.GeometryColumn,
                StringComparison.OrdinalIgnoreCase));
            var geometryType = hasGeometry
                ? await DetectGeometryTypeAsync(
                    datasetName,
                    parquetFiles,
                    cancellationToken).ConfigureAwait(false)
                : null;

            return new OvertureMfcDatasetInspection(columns, geometryType);
        }

        public ValueTask DisposeAsync()
        {
            _connection.Dispose();
            return ValueTask.CompletedTask;
        }

        private async Task<IReadOnlyList<OvertureDuckDbColumn>> ReadSchemaAsync(
            string parquetFile,
            CancellationToken cancellationToken)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = OvertureMfcSchemaPlanner.BuildDescribeSql(parquetFile);
            using var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            var columns = new List<OvertureDuckDbColumn>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                columns.Add(new OvertureDuckDbColumn(
                    reader.GetString(0),
                    reader.GetString(1).ToUpperInvariant()));
            }

            return columns;
        }

        private async Task<string?> DetectGeometryTypeAsync(
            string datasetName,
            IReadOnlyList<string> parquetFiles,
            CancellationToken cancellationToken)
        {
            _log($"Starting geometry type/SRID detection for dataset '{datasetName}'...");
            foreach (var parquetFile in parquetFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalizedPath = NormalizePath(parquetFile);
                _log($"  Checking file: {normalizedPath}");
                try
                {
                    using var command = _connection.CreateCommand();
                    command.CommandText = OvertureMfcSchemaPlanner
                        .BuildGeometryTypeSql(parquetFile);
                    using var reader = await command
                        .ExecuteReaderAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
                        reader.IsDBNull(0))
                    {
                        continue;
                    }

                    var geometryType = reader.GetValue(0)?.ToString();
                    if (!string.IsNullOrWhiteSpace(geometryType))
                    {
                        _log(
                            $"MFC Generation: Detected geometry type '{geometryType}' " +
                            $"for dataset '{datasetName}' using file '{normalizedPath}'. " +
                            $"SRID assumed as {OvertureMfcSchemaPlanner.DefaultWkid}.");
                        return geometryType;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _log(
                        $"Warning: Error executing geometry detection query on " +
                        $"'{normalizedPath}' for dataset '{datasetName}': " +
                        $"{exception.Message}. Trying next file if available.");
                }
            }

            _log(
                $"Warning: Could not detect a specific geometry type for dataset " +
                $"'{datasetName}' after checking all files. Defaulting geometry definition.");
            return null;
        }

        private static string NormalizePath(string path) => path.Replace('\\', '/');
    }
}
