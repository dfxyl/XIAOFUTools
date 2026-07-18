using System;
using XIAOFUTools.Features.User.AIAssistant.Database;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Context
{
    internal static class AgentContextBudgetPolicy
    {
        internal const int DefaultContextWindowTokens = 12000;
        internal const int MinContextBudgetTokens = 4096;

        internal static int ResolveContextWindow(AIServiceConfig service)
        {
            var contextWindow = service?.ContextWindowTokens ?? 0;
            if (contextWindow <= 0 &&
                AIAssistantDefaultServicePolicy.TryGetKnownModel(service?.ModelName, out var model))
            {
                contextWindow = model.ContextWindowTokens;
            }

            return Math.Max(
                MinContextBudgetTokens,
                contextWindow > 0 ? contextWindow : DefaultContextWindowTokens);
        }

        internal static int GetInputBudget(int contextWindowTokens)
        {
            var effectiveWindow = Math.Max(MinContextBudgetTokens, contextWindowTokens);
            var reservedForReply = Math.Max(1024, Math.Min(32768, effectiveWindow / 6));
            return Math.Max(MinContextBudgetTokens, effectiveWindow - reservedForReply);
        }

        internal static int GetSummarizeThreshold(int contextWindowTokens)
        {
            return Math.Max(3000, (int)(GetInputBudget(contextWindowTokens) * 0.72));
        }
    }
}
