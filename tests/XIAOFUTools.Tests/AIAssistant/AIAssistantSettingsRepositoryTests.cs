using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure;
using Microsoft.Data.Sqlite;

namespace XIAOFUTools.Tests.AIAssistant;

[Trait("Category", "Integration")]
[Collection(AIAssistantSqliteCollection.Name)]
public sealed class AIAssistantSettingsRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "XIAOFUTools.Tests",
        Guid.NewGuid().ToString("N"));

    public AIAssistantSettingsRepositoryTests()
    {
        SQLitePCL.Batteries_V2.Init();
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void SearchSettings_SaveAndLoadPreservesDatabaseContract()
    {
        var repository = CreateRepository();
        repository.SaveSearchSettings(new SearchSettings
        {
            TavilyApiKey = "tavily-key",
            BraveApiKey = "brave-key",
            EnableSearXNG = false,
            SearXNGInstanceUrl = "https://search.example.test",
            EnableBingScraper = false
        });

        var loaded = repository.GetSearchSettings();

        Assert.Equal("tavily-key", loaded.TavilyApiKey);
        Assert.Equal("brave-key", loaded.BraveApiKey);
        Assert.False(loaded.EnableSearXNG);
        Assert.Equal("https://search.example.test", loaded.SearXNGInstanceUrl);
        Assert.False(loaded.EnableBingScraper);
    }

    [Fact]
    public void ToolSettings_SaveAndLoadPreservesPerModeValues()
    {
        var repository = CreateRepository();
        var settings = new ToolExecutionSettings
        {
            PromptBeforeToolInChat = false,
            SensitiveMode = false
        };
        settings.EnsureDefaults();
        settings.SetToolMode("web_fetch", false, true);
        settings.SetToolMode("project_snapshot", true, false);

        repository.SaveToolExecutionSettings(settings);
        var loaded = repository.GetToolExecutionSettings();

        Assert.False(loaded.PromptBeforeToolInChat);
        Assert.False(loaded.SensitiveMode);
        Assert.False(loaded.ToolModes["web_fetch"].ChatEnabled);
        Assert.True(loaded.ToolModes["web_fetch"].AgentEnabled);
        Assert.True(loaded.ToolModes["project_snapshot"].ChatEnabled);
        Assert.False(loaded.ToolModes["project_snapshot"].AgentEnabled);
    }

    private AIAssistantSettingsRepository CreateRepository()
    {
        var factory = new AIAssistantConnectionFactory(Path.Combine(_directory, "aiassistant.db"));
        return new AIAssistantSettingsRepository(factory);
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
