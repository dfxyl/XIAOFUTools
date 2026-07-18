using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class OvertureDuckDbDataSession : IOvertureDuckDbDataSession
    {
        private readonly DuckDBConnection _connection;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public OvertureDuckDbDataSession(DuckDBConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<OvertureIngestResult> IngestAsync(
            string parquetPath,
            OvertureExtent? extent,
            CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var command = _connection.CreateCommand();
                command.CommandText = OvertureDuckDbDataPlan.BuildSchemaProbeSql(parquetPath);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                command.CommandText = "DESCRIBE temp";
                var columnCount = 0;
                using (var reader = await command
                    .ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        columnCount++;
                    }
                }

                command.CommandText = OvertureDuckDbDataPlan.BuildIngestSql(parquetPath, extent);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                command.CommandText = "SELECT COUNT(*) FROM current_table";
                var count = await command
                    .ExecuteScalarAsync(cancellationToken)
                    .ConfigureAwait(false);
                return new OvertureIngestResult(Convert.ToInt64(count), columnCount);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<DataTable> GetPreviewDataAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var command = _connection.CreateCommand();
                command.CommandText = OvertureDuckDbDataPlan.PreviewSql;
                using var reader = await command
                    .ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);
                var table = new DataTable();
                table.Load(reader);
                return table;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IReadOnlyList<string>> GetGeometryTypesAsync(
            CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var command = _connection.CreateCommand();
                command.CommandText = OvertureDuckDbDataPlan.GeometryTypesSql;
                using var reader = await command
                    .ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);
                var types = new List<string>();
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var geometryType = reader.IsDBNull(0) ? null : reader.GetString(0);
                    if (!string.IsNullOrWhiteSpace(geometryType))
                    {
                        types.Add(geometryType);
                    }
                }
                return types;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task ExportGeoParquetAsync(
            string selectQuery,
            string outputPath,
            CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var command = _connection.CreateCommand();
                command.CommandText = OvertureDuckDbDataPlan.BuildGeoParquetCopySql(
                    selectQuery,
                    outputPath);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
