using Microsoft.Data.Sqlite;
using XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure;

namespace XIAOFUTools.Tests.AIAssistant;

[Trait("Category", "Integration")]
[Collection(AIAssistantSqliteCollection.Name)]
public sealed class AIAssistantSessionRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "XIAOFUTools.Tests",
        Guid.NewGuid().ToString("N"));

    public AIAssistantSessionRepositoryTests()
    {
        SQLitePCL.Batteries_V2.Init();
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void SessionLifecycle_PreservesConversationAndExecutionContracts()
    {
        var (repository, factory) = CreateRepository();
        const string sessionId = "session-1";

        repository.CreateSession(sessionId, "初始标题");
        repository.SaveMessage(sessionId, "user", "用户消息", 12, images: "[]", turnId: "turn-1");
        repository.SaveMessage(sessionId, "assistant", "助手消息", 18, thinking: "分析", turnId: "turn-1");
        repository.SaveToolCall(sessionId, "layer_query", "{\"limit\":1}", "{\"count\":1}", "success", "turn-1", "call-1");
        var executionId = repository.SavePythonExecution(
            sessionId,
            "code-1",
            "print('ok')",
            "ok",
            string.Empty,
            true,
            25);
        repository.SaveContextSummary(sessionId, "上下文摘要", 2, 2, 20);

        var history = repository.GetConversationHistory(sessionId, 0);
        Assert.Collection(
            history,
            message =>
            {
                Assert.Equal("user", message.Role);
                Assert.Equal("用户消息", message.Content);
                Assert.Equal("[]", message.Images);
                Assert.Equal("turn-1", message.TurnId);
            },
            message =>
            {
                Assert.Equal("assistant", message.Role);
                Assert.Equal("助手消息", message.Content);
                Assert.Equal("分析", message.Thinking);
            });
        Assert.Equal(2, repository.GetMessageCount(sessionId));
        Assert.Equal(history[1].Id, repository.GetLatestMessageId(sessionId));
        Assert.Single(repository.GetMessagesAfterId(sessionId, history[0].Id));

        var recentSession = Assert.Single(repository.GetRecentSessions());
        Assert.Equal(sessionId, recentSession.SessionId);
        Assert.Equal("初始标题", recentSession.Title);

        var toolCall = Assert.Single(repository.GetToolCallHistory(sessionId, 0));
        Assert.Equal("layer_query", toolCall.ToolName);
        Assert.Equal("turn-1", toolCall.TurnId);
        Assert.Equal("call-1", toolCall.CallId);

        Assert.True(executionId > 0);
        var execution = Assert.Single(repository.GetPythonExecutions(sessionId));
        Assert.Equal("code-1", execution.CodeId);
        Assert.True(execution.Success);
        Assert.Equal(executionId, repository.GetLatestPythonExecution(sessionId, "code-1").Id);

        var summary = repository.GetLatestContextSummary(sessionId);
        Assert.Equal("上下文摘要", summary.Summary);
        Assert.Equal(2, summary.SummarizedUpToId);

        repository.UpdateSessionTitle(sessionId, "更新标题");
        SetLastActivity(factory, sessionId, "2000-01-01 00:00:00");
        repository.UpdateSessionActivity(sessionId);
        recentSession = Assert.Single(repository.GetRecentSessions());
        Assert.Equal("更新标题", recentSession.Title);
        Assert.True(recentSession.LastActivity.Year > 2000);

        repository.ClearConversationHistory(sessionId);
        Assert.Empty(repository.GetConversationHistory(sessionId, 0));
        Assert.Empty(repository.GetToolCallHistory(sessionId, 0));
        Assert.Empty(repository.GetPythonExecutions(sessionId));
        Assert.Null(repository.GetLatestContextSummary(sessionId));
        Assert.Equal(1, CountRows(factory, "sessions", sessionId));

        repository.DeleteSession(sessionId);
        Assert.Equal(0, CountRows(factory, "sessions", sessionId));
    }

    private (AIAssistantSessionRepository Repository, AIAssistantConnectionFactory Factory) CreateRepository()
    {
        var factory = new AIAssistantConnectionFactory(Path.Combine(_directory, "aiassistant.db"));
        var settingsRepository = new AIAssistantSettingsRepository(factory);
        var migrator = new AIAssistantDatabaseMigrator(factory, settingsRepository, () => { });
        migrator.Migrate();
        return (new AIAssistantSessionRepository(factory), factory);
    }

    private static void SetLastActivity(
        AIAssistantConnectionFactory factory,
        string sessionId,
        string value)
    {
        using var connection = factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE sessions SET last_activity = @value WHERE session_id = @sid";
        command.Parameters.AddWithValue("@value", value);
        command.Parameters.AddWithValue("@sid", sessionId);
        command.ExecuteNonQuery();
    }

    private static int CountRows(
        AIAssistantConnectionFactory factory,
        string tableName,
        string sessionId)
    {
        using var connection = factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE session_id = @sid";
        command.Parameters.AddWithValue("@sid", sessionId);
        return Convert.ToInt32(command.ExecuteScalar());
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
