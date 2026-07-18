using System;
using System.Threading;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure
{
    internal sealed class DuckDbConnectionCommandExecutor : IOvertureDuckDbCommandExecutor
    {
        private readonly DuckDBConnection _connection;

        public DuckDbConnectionCommandExecutor(DuckDBConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            return _connection.OpenAsync(cancellationToken);
        }

        public async Task ExecuteNonQueryAsync(
            string commandText,
            CancellationToken cancellationToken)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
