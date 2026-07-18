using System;
using System.Collections.Generic;
using XIAOFUTools.Features.User.AIAssistant.Database;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Infrastructure
{
    /// <summary>
    /// 基于 DatabaseManager 的默认 Agent 数据存储实现。
    /// </summary>
    internal sealed class DatabaseAgentDataStore : IAgentDataStore
    {
        private readonly DatabaseManager _databaseManager;

        public DatabaseAgentDataStore(DatabaseManager databaseManager)
        {
            _databaseManager = databaseManager ?? throw new ArgumentNullException(nameof(databaseManager));
        }

        public AIServiceConfig GetDefaultService()
        {
            return _databaseManager.GetDefaultService();
        }

        public List<AIServiceConfig> GetAllServices()
        {
            return _databaseManager.GetAllServices();
        }

        public void SaveMessage(string sessionId, string role, string content, int tokenCount = 0, string thinking = null, string images = null, string turnId = null)
        {
            _databaseManager.SaveMessage(sessionId, role, content, tokenCount, thinking, images, turnId);
        }

        public List<ConversationMessage> GetConversationHistory(string sessionId, int limit = 50)
        {
            return _databaseManager.GetConversationHistory(sessionId, limit);
        }

        public ContextSummary GetLatestContextSummary(string sessionId)
        {
            return _databaseManager.GetLatestContextSummary(sessionId);
        }

        public List<ConversationMessage> GetMessagesAfterId(string sessionId, int afterId, int limit = 50)
        {
            return _databaseManager.GetMessagesAfterId(sessionId, afterId, limit);
        }

        public void SaveContextSummary(string sessionId, string summary, int summarizedUpToId, int messageCount, int estimatedTokens)
        {
            _databaseManager.SaveContextSummary(sessionId, summary, summarizedUpToId, messageCount, estimatedTokens);
        }

        public ToolExecutionSettings GetToolExecutionSettings()
        {
            return _databaseManager.GetToolExecutionSettings();
        }

        public void SaveToolExecutionSettings(ToolExecutionSettings settings)
        {
            _databaseManager.SaveToolExecutionSettings(settings);
        }

        public void SaveToolCall(string sessionId, string toolName, string parameters, string result, string status, string turnId = null, string callId = null)
        {
            _databaseManager.SaveToolCall(sessionId, toolName, parameters, result, status, turnId, callId);
        }

        public void ClearConversationHistory(string sessionId)
        {
            _databaseManager.ClearConversationHistory(sessionId);
        }
    }
}
