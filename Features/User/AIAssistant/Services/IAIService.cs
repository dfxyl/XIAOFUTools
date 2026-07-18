using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

#nullable disable

namespace XIAOFUTools.Features.User.AIAssistant.Services
{
    /// <summary>
    /// AI服务接口
    /// </summary>
    public interface IAIService : IDisposable
    {
        /// <summary>
        /// 发送消息并获取流式响应
        /// </summary>
        Task<string> SendMessageStreamAsync(
            List<ChatMessage> messages,
            Action<string> onChunkReceived,
            CancellationToken cancellationToken = default,
            Action<string> onReasoningReceived = null);

        /// <summary>
        /// 发送消息并获取完整响应
        /// </summary>
        Task<string> SendMessageAsync(
            List<ChatMessage> messages,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 使用OpenAI原生tools协议发送消息
        /// </summary>
        Task<AICompletionResult> SendWithToolsAsync(
            List<ChatMessage> messages,
            List<AIToolDefinition> tools,
            object toolChoice = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 使用OpenAI原生tools协议发送流式消息
        /// </summary>
        Task<AICompletionResult> SendWithToolsStreamAsync(
            List<ChatMessage> messages,
            List<AIToolDefinition> tools,
            Action<string> onChunkReceived,
            Action<string> onReasoningReceived = null,
            object toolChoice = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 验证API密钥是否有效
        /// </summary>
        Task<bool> ValidateApiKeyAsync();
    }

    /// <summary>
    /// 聊天消息
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; set; }
        public object Content { get; set; }
        public List<string> Images { get; set; }
        public string ToolCallId { get; set; }
        public List<AIToolCall> ToolCalls { get; set; }
        public string ReasoningContent { get; set; }
        
        public ChatMessage(string role, string content, List<string> images = null)
        {
            Role = role;
            Content = content;
            Images = images;
        }
        
        public ChatMessage(string role, object content)
        {
            Role = role;
            Content = content;
        }
    }

    /// <summary>
    /// API响应
    /// </summary>
    public class AIResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; }
        public string Error { get; set; }
        public int TokensUsed { get; set; }
    }

    /// <summary>
    /// OpenAI原生工具定义
    /// </summary>
    public class AIToolDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject ParametersSchema { get; set; }
    }

    /// <summary>
    /// OpenAI原生工具调用
    /// </summary>
    public class AIToolCall
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Arguments { get; set; }
    }

    /// <summary>
    /// OpenAI completion结果
    /// </summary>
    public class AICompletionResult
    {
        public string Content { get; set; }
        public string ReasoningContent { get; set; }
        public string FinishReason { get; set; }
        public List<AIToolCall> ToolCalls { get; set; } = new List<AIToolCall>();
    }
}
