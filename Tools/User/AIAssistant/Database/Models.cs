using System;

namespace XIAOFUTools.Tools.User.AIAssistant.Database
{
    /// <summary>
    /// AI服务配置
    /// </summary>
    public class AIServiceConfig
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ApiEndpoint { get; set; }
        public string ModelName { get; set; }
        public string ApiKey { get; set; }
        public bool IsDefault { get; set; }
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
        public bool SupportsVision { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// 对话消息
    /// </summary>
    public class ConversationMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public string Thinking { get; set; }  // AI的思考过程(仅Agent模式)
        public string Images { get; set; }  // 图片JSON数组
        public DateTime Timestamp { get; set; }
        public int TokenCount { get; set; }
    }

    /// <summary>
    /// 会话信息
    /// </summary>
    public class Session
    {
        public string SessionId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
    }

    /// <summary>
    /// 工具调用记录
    /// </summary>
    public class ToolCallRecord
    {
        public int Id { get; set; }
        public string SessionId { get; set; }
        public string ToolName { get; set; }
        public string Parameters { get; set; }
        public string Result { get; set; }
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Python执行记录
    /// </summary>
    public class PythonExecutionRecord
    {
        public int Id { get; set; }
        public string CodeId { get; set; }
        public string Code { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool Success { get; set; }
        public int ExecutionTimeMs { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
