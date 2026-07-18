using XIAOFUTools.Features.User.AIAssistant.Agent.Context;
using XIAOFUTools.Features.User.AIAssistant.Database;

namespace XIAOFUTools.Tests.AIAssistant;

public sealed class AgentContextBudgetPolicyTests
{
    [Fact]
    public void ResolveContextWindow_UsesExplicitConfiguredValue()
    {
        var service = new AIServiceConfig
        {
            ModelName = "custom-model",
            ContextWindowTokens = 64000
        };

        Assert.Equal(64000, AgentContextBudgetPolicy.ResolveContextWindow(service));
    }

    [Fact]
    public void ResolveContextWindow_UsesKnownModelMetadataAndSafeFallback()
    {
        var known = new AIServiceConfig
        {
            ModelName = "qwen/qwen3.5-122b-a10b",
            ContextWindowTokens = 0
        };
        var unknown = new AIServiceConfig
        {
            ModelName = "unknown",
            ContextWindowTokens = 0
        };

        Assert.Equal(262144, AgentContextBudgetPolicy.ResolveContextWindow(known));
        Assert.Equal(12000, AgentContextBudgetPolicy.ResolveContextWindow(unknown));
    }

    [Theory]
    [InlineData(4096, 4096)]
    [InlineData(12000, 10000)]
    [InlineData(262144, 229376)]
    public void GetInputBudget_ReservesReplyCapacity(int contextWindow, int expected)
    {
        Assert.Equal(expected, AgentContextBudgetPolicy.GetInputBudget(contextWindow));
    }

    [Fact]
    public void GetSummarizeThreshold_UsesSeventyTwoPercentOfInputBudget()
    {
        Assert.Equal(7200, AgentContextBudgetPolicy.GetSummarizeThreshold(12000));
    }
}
