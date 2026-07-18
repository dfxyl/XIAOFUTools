using Microsoft.Data.Sqlite;

namespace XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure
{
    internal sealed class AIAssistantConnectionFactory
    {
        private readonly string _connectionString;

        public AIAssistantConnectionFactory(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Mode=ReadWriteCreate;Cache=Shared";
        }

        public SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;";
                command.ExecuteNonQuery();
            }

            return connection;
        }
    }
}
