using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Agent.Infrastructure;
using XIAOFUTools.Features.User.AIAssistant.Agent.Prompts;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tools;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tooling;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Features.User.AIAssistant.Agent
{
    public partial class GISAgentCore
    {

        private void InitializeAIService()
        {
            System.Diagnostics.Debug.WriteLine("开始初始化AI服务...");
            
            if (_dataStore == null)
            {
                System.Diagnostics.Debug.WriteLine("数据库不可用，跳过AI服务初始化");
                return;
            }
            
            var defaultService = _dataStore.GetDefaultService();
            
            if (defaultService == null)
            {
                System.Diagnostics.Debug.WriteLine("未找到默认AI服务配置，尝试获取所有服务...");
                var allServices = _dataStore.GetAllServices();
                
                if (allServices != null && allServices.Count > 0)
                {
                    defaultService = allServices[0];
                    System.Diagnostics.Debug.WriteLine($"使用第一个可用服务: {defaultService.Name}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("数据库中没有AI服务配置，用户需要在设置中添加");
                    return;
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"找到AI服务配置: {defaultService.Name}");
            System.Diagnostics.Debug.WriteLine($"API端点: {defaultService.ApiEndpoint}");
            System.Diagnostics.Debug.WriteLine($"模型: {defaultService.ModelName}");
            
            ConfigureContextWindow(defaultService);
            _aiService = new OpenAICompatibleService(defaultService);
            System.Diagnostics.Debug.WriteLine("AI服务初始化成功");
        }


        private static string NormalizeMode(string mode)
        {
            return string.Equals(mode, "agent", StringComparison.OrdinalIgnoreCase) ? "agent" : "chat";
        }


        private void RegisterBuiltInTools()
        {
            foreach (var tool in BuiltInGisToolCatalog.CreateTools())
            {
                RegisterTool(tool);
            }
        }


        /// <summary>
        /// 注册GIS工具
        /// </summary>
        public void RegisterTool(IGISTool tool)
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));

            _tools[tool.Name] = tool;
            System.Diagnostics.Debug.WriteLine($"已注册GIS工具: {tool.Name}");
        }

        /// <summary>
        /// 切换AI模型
        /// </summary>
        public void SwitchModel(string modelName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"尝试切换到模型: {modelName}");
                
                if (_dataStore == null)
                {
                    throw new InvalidOperationException("数据库不可用，无法切换模型");
                }
                
                // 从数据库查找匹配的服务配置
                var allServices = _dataStore.GetAllServices();
                var targetService = allServices?.FirstOrDefault(s => 
                    s.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains(modelName, StringComparison.OrdinalIgnoreCase));
                
                if (targetService == null)
                {
                    System.Diagnostics.Debug.WriteLine($"未找到模型配置: {modelName}");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"找到模型配置: {targetService.Name} ({targetService.ModelName})");
                
                // 释放旧的AI服务
                _aiService?.Dispose();
                
                // 创建新的AI服务
                ConfigureContextWindow(targetService);
                _aiService = new OpenAICompatibleService(targetService);
                System.Diagnostics.Debug.WriteLine($"已切换到模型: {targetService.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"切换模型失败: {ex.Message}");
                throw new InvalidOperationException($"切换模型失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 估算文本的token数量（中文约1.5token/字，英文约0.75token/词）
        /// </summary>
        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            
            int chineseChars = 0;
            int otherChars = 0;
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) // CJK统一汉字
                    chineseChars++;
                else
                    otherChars++;
            }
            // 中文约1.5 token/字，英文约0.25 token/字符（含空格分词）
            return (int)(chineseChars * 1.5 + otherChars * 0.25) + 1;
        }

        /// <summary>
        /// 估算消息列表的总token数
        /// </summary>
        private int EstimateTotalTokens(List<ChatMessage> messages)
        {
            int total = 0;
            foreach (var msg in messages)
            {
                total += EstimateTokenCount(msg.Content?.ToString() ?? "");
                total += 4; // 每条消息的role/格式开销
            }
            return total;
        }

        private static JObject ParseToolArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments))
            {
                return new JObject();
            }

            try
            {
                return JObject.Parse(arguments);
            }
            catch
            {
                return new JObject
                {
                    ["raw"] = arguments
                };
            }
        }

        private bool IsToolEnabled(string toolName, ToolExecutionSettings settings, string mode = null)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return false;
            }

            var effectiveSettings = settings ?? new ToolExecutionSettings();
            if (effectiveSettings.SensitiveMode && string.Equals(toolName, WebFetchToolName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return effectiveSettings.IsToolEnabled(toolName, NormalizeMode(mode ?? _currentMode));
        }


        private bool IsToolAllowedInCurrentMode(string toolName, ToolExecutionSettings settings, string mode = null)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return false;
            }

            var effectiveSettings = settings ?? new ToolExecutionSettings();
            return effectiveSettings.IsToolEnabled(toolName, NormalizeMode(mode ?? _currentMode));
        }


        private bool NeedPromptBeforeTool(string toolName, ToolExecutionSettings settings, string mode = null)
        {
            if (string.Equals(NormalizeMode(mode ?? _currentMode), "agent", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return settings?.PromptBeforeToolInChat == true;
        }

        private static JToken NormalizeTokenForSignature(JToken token)
        {
            if (token == null)
            {
                return JValue.CreateNull();
            }

            if (token.Type == JTokenType.Object)
            {
                var source = (JObject)token;
                var normalized = new JObject();
                foreach (var property in source.Properties().OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
                {
                    normalized[property.Name] = NormalizeTokenForSignature(property.Value);
                }

                return normalized;
            }

            if (token.Type == JTokenType.Array)
            {
                var normalizedArray = new JArray();
                foreach (var child in token.Children())
                {
                    normalizedArray.Add(NormalizeTokenForSignature(child));
                }

                return normalizedArray;
            }

            return token.DeepClone();
        }

        private static string SerializeToolResultForNotice(ToolResult toolResult)
        {
            if (toolResult == null)
            {
                return JsonConvert.SerializeObject(new
                {
                    success = false,
                    message = "工具执行返回空结果",
                    error = "工具执行返回空结果",
                    data = (object)null
                });
            }

            return JsonConvert.SerializeObject(new
            {
                success = toolResult.Success,
                message = toolResult.Message,
                error = toolResult.Error,
                data = toolResult.Data
            });
        }

        private static bool LooksLikeToolsUnsupported(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return false;
            }

            var text = errorMessage.ToLowerInvariant();
            return text.Contains("tool")
                   && (text.Contains("not support")
                       || text.Contains("unsupported")
                       || text.Contains("invalid")
                       || text.Contains("unknown"));
        }

        private static string SerializeForToolCard(object value)
        {
            if (value == null)
            {
                return "{}";
            }

            try
            {
                var text = value is string str
                    ? str
                    : JsonConvert.SerializeObject(value, Formatting.Indented);

                if (string.IsNullOrWhiteSpace(text))
                {
                    return "{}";
                }

                if (text.Length > 4000)
                {
                    return text.Substring(0, 4000) + "\n...（已截断）";
                }

                return text;
            }
            catch
            {
                return value.ToString() ?? "{}";
            }
        }

        /// <summary>
        /// 异步总结旧消息（后台执行，不阻塞用户交互）
        /// </summary>
        private async Task SummarizeOldMessagesAsync(string sessionId, List<ConversationMessage> history)
        {
            if (_aiService == null || string.IsNullOrWhiteSpace(sessionId)) return;

            lock (_summarizingLock)
            {
                if (_summarizingSessions.Contains(sessionId))
                {
                    return;
                }

                _summarizingSessions.Add(sessionId);
            }

            System.Diagnostics.Debug.WriteLine($"[Summarize] 开始异步总结旧消息, 会话: {sessionId}");
            
            try
            {
                // 保留最近的N条消息，总结之前的所有消息
                int cutoff = history.Count - KEEP_RECENT_MESSAGES;
                if (cutoff <= 1) return;

                var messagesToSummarize = history.Take(cutoff).ToList();
                
                // 构建总结请求
                var summaryPrompt = new List<ChatMessage>
                {
                    new ChatMessage("system", 
                        "你是一个对话总结助手。请将以下对话历史压缩为一段简洁的总结，" +
                        "保留关键信息、用户意图、重要结论和上下文。" +
                        "总结应该让后续对话能够无缝继续。" +
                        "请直接输出总结内容，不要加任何前缀。控制在300字以内。")
                };

                var conversationText = new System.Text.StringBuilder();
                foreach (var msg in messagesToSummarize)
                {
                    var roleLabel = msg.Role == "user" ? "用户" : (msg.Role == "assistant" ? "助手" : "系统");
                    var content = msg.Content;
                    // 截断过长的单条消息
                    if (content.Length > 500)
                        content = content.Substring(0, 500) + "...（已截断）";
                    conversationText.AppendLine($"{roleLabel}: {content}");
                }
                
                summaryPrompt.Add(new ChatMessage("user", 
                    $"请总结以下对话：\n\n{conversationText}"));

                // 调用AI生成总结（非流式）
                var summary = await _aiService.SendMessageAsync(summaryPrompt);
                
                if (!string.IsNullOrWhiteSpace(summary))
                {
                    // 获取被总结的最后一条消息的ID
                    var lastSummarizedMsg = messagesToSummarize.Last();
                    int summaryTokens = EstimateTokenCount(summary);
                    
                    _dataStore.SaveContextSummary(
                        sessionId,
                        summary,
                        lastSummarizedMsg.Id,
                        messagesToSummarize.Count,
                        summaryTokens);
                    
                    System.Diagnostics.Debug.WriteLine($"[Summarize] 总结完成: {messagesToSummarize.Count}条消息 -> {summaryTokens} tokens, 会话: {sessionId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Summarize] 总结失败: {ex.Message}");
            }
            finally
            {
                lock (_summarizingLock)
                {
                    _summarizingSessions.Remove(sessionId);
                }
            }
        }

    }
}
