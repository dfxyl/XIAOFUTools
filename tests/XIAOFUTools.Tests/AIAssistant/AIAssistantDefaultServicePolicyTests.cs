using XIAOFUTools.Features.User.AIAssistant.Database;

namespace XIAOFUTools.Tests.AIAssistant;

public class AIAssistantDefaultServicePolicyTests
{
    [Fact]
    public void InitialServices_ProvidesNvidiaChatCompletionModelsWithMetadata()
    {
        var services = AIAssistantDefaultServicePolicy.InitialServices;

        Assert.Collection(
            services,
            service => AssertService(service, "MiniMax M3", "minimaxai/minimax-m3", 1000000, true, true),
            service => AssertService(service, "Qwen3.5 122B A10B", "qwen/qwen3.5-122b-a10b", 262144, true, false),
            service => AssertService(service, "Qwen3.5 397B A17B", "qwen/qwen3.5-397b-a17b", 262144, true, false),
            service => AssertService(service, "DeepSeek V4 Flash", "deepseek-ai/deepseek-v4-flash", 1000000, false, false),
            service => AssertService(service, "DeepSeek V4 Pro", "deepseek-ai/deepseek-v4-pro", 1000000, false, false),
            service => AssertService(service, "Gemma 4 31B IT", "google/gemma-4-31b-it", 256000, true, false),
            service => AssertService(service, "GLM 5.2", "z-ai/glm-5.2", 1000000, false, false));
    }

    [Theory]
    [InlineData("DeepSeek Chat", "https://api.deepseek.com/v1", "deepseek-chat")]
    [InlineData("DeepSeek Reasoner", "https://api.deepseek.com/v1", "deepseek-reasoner")]
    [InlineData("SiliconFlow GLM-4.5V", "https://api.siliconflow.cn/v1/", "zai-org/GLM-4.5V")]
    [InlineData("SiliconFlow GLM-4.6V", "https://api.siliconflow.cn/v1/", "zai-org/GLM-4.6V")]
    [InlineData("Moonshot Kimi K2", "https://api.moonshot.cn/v1", "kimi-k2-0905-preview")]
    [InlineData("Kimi K2.6", "https://integrate.api.nvidia.com/v1", "moonshotai/kimi-k2.6")]
    [InlineData("MiniMax M2.7", "https://integrate.api.nvidia.com/v1", "minimaxai/minimax-m2.7")]
    [InlineData("GLM 5.1", "https://integrate.api.nvidia.com/v1", "z-ai/glm-5.1")]
    public void IsLegacyBuiltInService_RecognizesPreviousBundledConfigs(string name, string endpoint, string model)
    {
        var config = new AIServiceConfig
        {
            Name = name,
            ApiEndpoint = endpoint,
            ModelName = model,
            ApiKey = "old-bundled-key"
        };

        Assert.True(AIAssistantDefaultServicePolicy.IsLegacyBuiltInService(config));
    }

    [Fact]
    public void IsLegacyBuiltInService_DoesNotRemoveCustomServiceUsingSameProvider()
    {
        var config = new AIServiceConfig
        {
            Name = "我的 DeepSeek",
            ApiEndpoint = "https://api.deepseek.com/v1",
            ModelName = "deepseek-chat",
            ApiKey = "user-owned-key"
        };

        Assert.False(AIAssistantDefaultServicePolicy.IsLegacyBuiltInService(config));
    }

    [Theory]
    [InlineData("minimaxai/minimax-m3", 1000000, true)]
    [InlineData("qwen/qwen3.5-122b-a10b", 262144, true)]
    [InlineData("qwen/qwen3.5-397b-a17b", 262144, true)]
    [InlineData("deepseek-ai/deepseek-v4-pro", 1000000, false)]
    [InlineData("google/gemma-4-31b-it", 256000, true)]
    [InlineData("z-ai/glm-5.2", 1000000, false)]
    public void TryGetKnownModelMetadata_ReturnsContextAndVisionSupport(string modelId, int contextTokens, bool supportsVision)
    {
        Assert.True(AIAssistantDefaultServicePolicy.TryGetKnownModel(modelId, out var model));
        Assert.Equal(contextTokens, model.ContextWindowTokens);
        Assert.Equal(supportsVision, model.SupportsVision);
        Assert.Equal(4096, model.DefaultMaxTokens);
    }

    [Fact]
    public void ApplyKnownModelDefaults_ClampsBundledNvidiaMaxTokensFromOldDatabases()
    {
        var config = new AIServiceConfig
        {
            Name = "MiniMax M3",
            ApiEndpoint = "https://integrate.api.nvidia.com/v1/chat/completions",
            ModelName = "minimaxai/minimax-m3",
            ApiKey = "bundled-key",
            MaxTokens = 32768
        };

        AIAssistantDefaultServicePolicy.ApplyKnownModelDefaults(config);

        Assert.Equal(8192, config.MaxTokens);
        Assert.Equal(1000000, config.ContextWindowTokens);
        Assert.True(config.SupportsVision);
    }

    private static void AssertService(
        AIServiceConfig service,
        string name,
        string model,
        int contextTokens,
        bool supportsVision,
        bool isDefault)
    {
        Assert.Equal(name, service.Name);
        Assert.Equal("https://integrate.api.nvidia.com/v1", service.ApiEndpoint);
        Assert.Equal(model, service.ModelName);
        Assert.Equal(contextTokens, service.ContextWindowTokens);
        Assert.Equal(supportsVision, service.SupportsVision);
        Assert.Equal(isDefault, service.IsDefault);
        Assert.Equal(4096, service.MaxTokens);
    }
}
