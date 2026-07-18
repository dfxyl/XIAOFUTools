using System.Collections.Generic;

namespace XIAOFUTools.Features.User.AIAssistant.Database
{
    public partial class DatabaseManager
    {
        public List<PythonExecutionRecord> GetPythonExecutions(string sessionId)
            => _sessionRepository.GetPythonExecutions(sessionId);

        public PythonExecutionRecord GetLatestPythonExecution(string sessionId, string codeId)
            => _sessionRepository.GetLatestPythonExecution(sessionId, codeId);

        public ContextSummary GetLatestContextSummary(string sessionId)
            => _sessionRepository.GetLatestContextSummary(sessionId);

        public List<ConversationMessage> GetMessagesAfterId(string sessionId, int afterId, int limit = 50)
            => _sessionRepository.GetMessagesAfterId(sessionId, afterId, limit);

        public int GetMessageCount(string sessionId)
            => _sessionRepository.GetMessageCount(sessionId);

        public int GetLatestMessageId(string sessionId)
            => _sessionRepository.GetLatestMessageId(sessionId);

        public List<ConversationMessage> GetConversationHistory(string sessionId, int limit = 50)
            => _sessionRepository.GetConversationHistory(sessionId, limit);

        public List<Session> GetRecentSessions(int limit = 20)
            => _sessionRepository.GetRecentSessions(limit);

        public List<ToolCallRecord> GetToolCallHistory(string sessionId, int limit = 200)
            => _sessionRepository.GetToolCallHistory(sessionId, limit);

        public void SaveMessage(string sessionId, string role, string content, int tokenCount = 0, string thinking = null, string images = null, string turnId = null)
            => _sessionRepository.SaveMessage(sessionId, role, content, tokenCount, thinking, images, turnId);

        public void CreateSession(string sessionId, string title = null)
            => _sessionRepository.CreateSession(sessionId, title);

        public void SaveToolCall(string sessionId, string toolName, string parameters, string result, string status, string turnId = null, string callId = null)
            => _sessionRepository.SaveToolCall(sessionId, toolName, parameters, result, status, turnId, callId);

        public int SavePythonExecution(string sessionId, string codeId, string code, string output, string error, bool success, long executionTimeMs)
            => _sessionRepository.SavePythonExecution(sessionId, codeId, code, output, error, success, executionTimeMs);

        public void SaveContextSummary(string sessionId, string summary, int summarizedUpToId, int messageCount, int estimatedTokens)
            => _sessionRepository.SaveContextSummary(sessionId, summary, summarizedUpToId, messageCount, estimatedTokens);

        public void ClearConversationHistory(string sessionId)
            => _sessionRepository.ClearConversationHistory(sessionId);

        public void UpdateSessionActivity(string sessionId)
            => _sessionRepository.UpdateSessionActivity(sessionId);

        public void UpdateSessionTitle(string sessionId, string title)
            => _sessionRepository.UpdateSessionTitle(sessionId, title);

        public void DeleteSession(string sessionId)
            => _sessionRepository.DeleteSession(sessionId);
    }
}
