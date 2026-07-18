using System;
using System.Collections.Generic;
using System.Linq;

#nullable disable

namespace XIAOFUTools.Features.User.AIAssistant.Database
{
    /// <summary>
    /// AI助手默认服务策略。
    /// </summary>
    public static class AIAssistantDefaultServicePolicy
    {
        public const string NvidiaEndpoint = "https://integrate.api.nvidia.com/v1";
        public const int NvidiaDefaultMaxTokens = 4096;
        public const int NvidiaMaxRequestTokens = 8192;

        private const string NvidiaApiKey = "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";

        private static readonly LegacyBuiltInService[] LegacyBuiltInServices =
        {
            new LegacyBuiltInService("DeepSeek Chat", "https://api.deepseek.com/v1", "deepseek-chat"),
            new LegacyBuiltInService("DeepSeek Reasoner", "https://api.deepseek.com/v1", "deepseek-reasoner"),
            new LegacyBuiltInService("SiliconFlow GLM-4.5V", "https://api.siliconflow.cn/v1", "zai-org/GLM-4.5V"),
            new LegacyBuiltInService("SiliconFlow GLM-4.6V", "https://api.siliconflow.cn/v1", "zai-org/GLM-4.6V"),
            new LegacyBuiltInService("Moonshot Kimi K2", "https://api.moonshot.cn/v1", "kimi-k2-0905-preview"),
            new LegacyBuiltInService("Kimi K2.6", NvidiaEndpoint, "moonshotai/kimi-k2.6"),
            new LegacyBuiltInService("MiniMax M2.7", NvidiaEndpoint, "minimaxai/minimax-m2.7"),
            new LegacyBuiltInService("Qwen3.5 122B A10B", NvidiaEndpoint, "qwen/qwen3-5-122b-a10b"),
            new LegacyBuiltInService("GLM 5.1", NvidiaEndpoint, "z-ai/glm5.1"),
            new LegacyBuiltInService("GLM 5.1", NvidiaEndpoint, "z-ai/glm-5.1"),
            new LegacyBuiltInService("GLM 5.0", NvidiaEndpoint, "z-ai/glm5")
        };

        private static readonly IReadOnlyList<AIAssistantModelMetadata> KnownModels = new[]
        {
            new AIAssistantModelMetadata("MiniMax M3", "minimaxai/minimax-m3", 1000000, true),
            new AIAssistantModelMetadata("Qwen3.5 122B A10B", "qwen/qwen3.5-122b-a10b", 262144, true),
            new AIAssistantModelMetadata("Qwen3.5 397B A17B", "qwen/qwen3.5-397b-a17b", 262144, true),
            new AIAssistantModelMetadata("DeepSeek V4 Flash", "deepseek-ai/deepseek-v4-flash", 1000000, false),
            new AIAssistantModelMetadata("DeepSeek V4 Pro", "deepseek-ai/deepseek-v4-pro", 1000000, false),
            new AIAssistantModelMetadata("Gemma 4 31B IT", "google/gemma-4-31b-it", 256000, true),
            new AIAssistantModelMetadata("GLM 5.2", "z-ai/glm-5.2", 1000000, false)
        };

        public static IReadOnlyList<AIServiceConfig> InitialServices { get; } = KnownModels
            .Select((model, index) => CreateInitialService(model, index == 0))
            .ToList()
            .AsReadOnly();

        public static bool TryGetKnownModel(string modelName, out AIAssistantModelMetadata model)
        {
            foreach (var knownModel in KnownModels)
            {
                if (string.Equals(knownModel.ModelName, modelName, StringComparison.OrdinalIgnoreCase))
                {
                    model = knownModel;
                    return true;
                }
            }

            model = null;
            return false;
        }

        public static void ApplyKnownModelDefaults(AIServiceConfig config)
        {
            if (config == null || !TryGetKnownModel(config.ModelName, out var model))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(config.Name))
            {
                config.Name = model.DisplayName;
            }

            config.ContextWindowTokens = model.ContextWindowTokens;
            config.SupportsVision = model.SupportsVision;

            if (config.MaxTokens <= 0)
            {
                config.MaxTokens = model.DefaultMaxTokens;
            }
            else if (IsNvidiaBuiltInService(config) && config.MaxTokens > NvidiaMaxRequestTokens)
            {
                config.MaxTokens = NvidiaMaxRequestTokens;
            }
        }

        public static bool IsLegacyBuiltInService(AIServiceConfig config)
        {
            if (config == null)
            {
                return false;
            }

            foreach (var legacyService in LegacyBuiltInServices)
            {
                if (legacyService.Matches(config))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsNvidiaBuiltInService(AIServiceConfig config)
        {
            return config != null &&
                   string.Equals(NormalizeEndpoint(config.ApiEndpoint), NormalizeEndpoint(NvidiaEndpoint), StringComparison.OrdinalIgnoreCase) &&
                   TryGetKnownModel(config.ModelName, out _);
        }

        private static AIServiceConfig CreateInitialService(AIAssistantModelMetadata model, bool isDefault)
        {
            return new AIServiceConfig
            {
                Name = model.DisplayName,
                ApiEndpoint = NvidiaEndpoint,
                ModelName = model.ModelName,
                ApiKey = NvidiaApiKey,
                IsDefault = isDefault,
                MaxTokens = model.DefaultMaxTokens,
                ContextWindowTokens = model.ContextWindowTokens,
                Temperature = 0.7,
                SupportsVision = model.SupportsVision
            };
        }

        private readonly struct LegacyBuiltInService
        {
            private readonly string _apiEndpoint;
            private readonly string _modelName;
            private readonly string _name;

            public LegacyBuiltInService(string name, string apiEndpoint, string modelName)
            {
                _name = name;
                _apiEndpoint = NormalizeEndpoint(apiEndpoint);
                _modelName = modelName;
            }

            public bool Matches(AIServiceConfig config)
            {
                return string.Equals(config.Name, _name, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(NormalizeEndpoint(config.ApiEndpoint), _apiEndpoint, StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(config.ModelName, _modelName, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string NormalizeEndpoint(string endpoint)
        {
            var normalized = (endpoint ?? string.Empty).Trim().TrimEnd('/');
            const string chatCompletionsSuffix = "/chat/completions";
            if (normalized.EndsWith(chatCompletionsSuffix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - chatCompletionsSuffix.Length).TrimEnd('/');
            }

            return normalized;
        }
    }

    public sealed class AIAssistantModelMetadata
    {
        public AIAssistantModelMetadata(string displayName, string modelName, int contextWindowTokens, bool supportsVision)
        {
            DisplayName = displayName;
            ModelName = modelName;
            ContextWindowTokens = contextWindowTokens;
            SupportsVision = supportsVision;
            DefaultMaxTokens = AIAssistantDefaultServicePolicy.NvidiaDefaultMaxTokens;
        }

        public string DisplayName { get; }
        public string ModelName { get; }
        public int ContextWindowTokens { get; }
        public bool SupportsVision { get; }
        public int DefaultMaxTokens { get; }
    }
}
