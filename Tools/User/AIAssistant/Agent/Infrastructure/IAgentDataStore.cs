using System.Collections.Generic;
using XIAOFUTools.Tools.User.AIAssistant.Database;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent.Infrastructure
{
    /// <summary>
    /// Agent 数据访问抽象，便于在测试中替换真实数据库。
    /// </summary>
    internal interface IAgentDataStore
    {
        AIServiceConfig GetDefaultService();

        List<AIServiceConfig> GetAllServices();

        void SaveMessage(string sessionId, string role, string content, int tokenCount = 0, string thinking = null, string images = null, string turnId = null);

        List<ConversationMessage> GetConversationHistory(string sessionId, int limit = 50);

        ContextSummary GetLatestContextSummary(string sessionId);

        List<ConversationMessage> GetMessagesAfterId(string sessionId, int afterId, int limit = 50);

        void SaveContextSummary(string sessionId, string summary, int summarizedUpToId, int messageCount, int estimatedTokens);

        ToolExecutionSettings GetToolExecutionSettings();

        void SaveToolExecutionSettings(ToolExecutionSettings settings);

        void SaveToolCall(string sessionId, string toolName, string parameters, string result, string status, string turnId = null, string callId = null);

        void ClearConversationHistory(string sessionId);
    }
}
