using Microsoft.Data.Sqlite;
using XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure;

namespace XIAOFUTools.Tests.AIAssistant;

[Trait("Category", "Integration")]
[Collection(AIAssistantSqliteCollection.Name)]
public sealed class AIAssistantDatabaseMigratorTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "XIAOFUTools.Tests",
        Guid.NewGuid().ToString("N"));

    public AIAssistantDatabaseMigratorTests()
    {
        SQLitePCL.Batteries_V2.Init();
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Migrate_CreatesCompatibleSchemaAndIsIdempotent()
    {
        var factory = new AIAssistantConnectionFactory(Path.Combine(_directory, "aiassistant.db"));
        var settingsRepository = new AIAssistantSettingsRepository(factory);
        var defaultServiceRuns = 0;
        var migrator = new AIAssistantDatabaseMigrator(
            factory,
            settingsRepository,
            () => defaultServiceRuns++);

        migrator.Migrate();
        migrator.Migrate();

        using var connection = factory.OpenConnection();
        var tables = ReadNames(connection, "table");
        Assert.Contains("ai_services", tables);
        Assert.Contains("conversation_history", tables);
        Assert.Contains("tool_call_history", tables);
        Assert.Contains("sessions", tables);
        Assert.Contains("python_executions", tables);
        Assert.Contains("context_summaries", tables);
        Assert.Contains("search_settings", tables);
        Assert.Contains("tool_settings", tables);
        Assert.Contains("context_window_tokens", ReadColumns(connection, "ai_services"));
        Assert.Contains("turn_id", ReadColumns(connection, "conversation_history"));
        Assert.Contains("call_id", ReadColumns(connection, "tool_call_history"));
        Assert.Equal(2, defaultServiceRuns);
    }

    private static HashSet<string> ReadNames(SqliteConnection connection, string type)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = @type";
        command.Parameters.AddWithValue("@type", type);
        using var reader = command.ExecuteReader();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static HashSet<string> ReadColumns(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName})";
        using var reader = command.ExecuteReader();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }

        return names;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }
}
