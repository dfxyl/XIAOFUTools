using Microsoft.Data.Sqlite;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Database.Infrastructure;

namespace XIAOFUTools.Tests.AIAssistant;

[Trait("Category", "Integration")]
[Collection(AIAssistantSqliteCollection.Name)]
public sealed class AIServiceRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "XIAOFUTools.Tests",
        Guid.NewGuid().ToString("N"));

    public AIServiceRepositoryTests()
    {
        SQLitePCL.Batteries_V2.Init();
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void CrudAndDefaultSelection_PreserveServiceConfigurationContract()
    {
        var repository = CreateRepository();
        repository.Insert(CreateService("服务 A", "model-a"));
        repository.Insert(CreateService("服务 B", "model-b"));

        var services = repository.GetAll();
        Assert.Equal(2, services.Count);
        var serviceA = services.Single(service => service.ModelName == "model-a");
        var serviceB = services.Single(service => service.ModelName == "model-b");

        serviceA.Name = "服务 A 更新";
        serviceA.MaxTokens = 8192;
        serviceA.ContextWindowTokens = 32768;
        serviceA.Temperature = 0.25;
        serviceA.SupportsVision = true;
        repository.Update(serviceA);
        repository.SetDefault(serviceA.Id);

        var loaded = repository.GetDefault();
        Assert.Equal(serviceA.Id, loaded.Id);
        Assert.Equal("服务 A 更新", loaded.Name);
        Assert.Equal(8192, loaded.MaxTokens);
        Assert.Equal(32768, loaded.ContextWindowTokens);
        Assert.Equal(0.25, loaded.Temperature, 3);
        Assert.True(loaded.SupportsVision);
        Assert.Single(repository.GetAll(), service => service.IsDefault);

        repository.Delete(serviceB.Id);
        Assert.Single(repository.GetAll());
    }

    [Fact]
    public void ResetToDefaults_AppliesPolicyAndCreatesSingleDefault()
    {
        var repository = CreateRepository();
        repository.Insert(CreateService("自定义服务", "custom-model"));

        repository.ResetToDefaults();

        var services = repository.GetAll();
        Assert.Equal(AIAssistantDefaultServicePolicy.InitialServices.Count, services.Count);
        Assert.Single(services, service => service.IsDefault);
        Assert.DoesNotContain(services, service => service.ModelName == "custom-model");
    }

    private AIServiceRepository CreateRepository()
    {
        var factory = new AIAssistantConnectionFactory(Path.Combine(_directory, "aiassistant.db"));
        var settingsRepository = new AIAssistantSettingsRepository(factory);
        new AIAssistantDatabaseMigrator(factory, settingsRepository, () => { }).Migrate();
        return new AIServiceRepository(factory);
    }

    private static AIServiceConfig CreateService(string name, string modelName)
    {
        return new AIServiceConfig
        {
            Name = name,
            ApiEndpoint = "https://example.test/v1",
            ModelName = modelName,
            ApiKey = "test-key",
            MaxTokens = 4096,
            ContextWindowTokens = 16384,
            Temperature = 0.7,
            SupportsVision = false
        };
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
