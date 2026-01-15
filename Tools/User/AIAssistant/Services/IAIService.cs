using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Tools.User.AIAssistant.Services
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
}
