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

        /// <summary>
        /// 构建消息历史（带自动总结压缩）
        /// </summary>
        private List<ChatMessage> BuildMessageHistory(string sessionId, string currentMessage, string mode, int contextWindowTokens, List<string> images = null)
        {
            // 根据当前模式选择提示词
            var systemPrompt = NormalizeMode(mode) == "agent" ? _agentSystemPrompt : _chatSystemPrompt;
            
            var messages = new List<ChatMessage>
            {
                new ChatMessage("system", systemPrompt)
            };

            int systemTokens = EstimateTokenCount(systemPrompt);
            int currentMsgTokens = EstimateTokenCount(currentMessage);
            int availableTokens = GetContextInputBudget(contextWindowTokens) - systemTokens - currentMsgTokens - 100; // 100为安全余量
            if (availableTokens < 1000)
            {
                availableTokens = 1000;
            }

            // 尝试获取已有的上下文总结
            ContextSummary existingSummary = null;
            try
            {
                existingSummary = _dataStore.GetLatestContextSummary(sessionId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取上下文总结失败: {ex.Message}");
            }

            // 获取对话历史
            List<ConversationMessage> history;
            if (existingSummary != null)
            {
                // 有总结：注入总结 + 总结之后的消息
                history = _dataStore.GetMessagesAfterId(sessionId, existingSummary.SummarizedUpToId, 50);
                
                var summaryMsg = $"[以下是之前对话的总结]\n{existingSummary.Summary}\n[总结结束，以下是最近的对话]";
                messages.Add(new ChatMessage("system", summaryMsg));
                availableTokens -= EstimateTokenCount(summaryMsg);
            }
            else
            {
                // 无总结：获取全部历史（限制50条）
                history = _dataStore.GetConversationHistory(sessionId, 50);
            }

            // 从最近的消息开始，向前填充直到token用尽
            var selectedHistory = new List<ConversationMessage>();
            int historyTokens = 0;
            
            for (int i = history.Count - 1; i >= 0; i--)
            {
                int msgTokens = EstimateTokenCount(history[i].Content) + 4;
                if (historyTokens + msgTokens > availableTokens)
                    break;
                historyTokens += msgTokens;
                selectedHistory.Insert(0, history[i]);
            }

            // 添加选中的历史消息
            foreach (var msg in selectedHistory)
            {
                if (string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(msg.Content))
                {
                    continue;
                }

                messages.Add(new ChatMessage(msg.Role, msg.Content));
            }

            // 添加当前消息(包含图片)
            messages.Add(new ChatMessage("user", currentMessage, images));

            // 检查是否需要触发异步总结（不阻塞当前请求）
            int totalHistoryTokens = 0;
            foreach (var msg in history)
            {
                totalHistoryTokens += EstimateTokenCount(msg.Content) + 4;
            }
            
            if (totalHistoryTokens > GetSummarizeThreshold(contextWindowTokens) && !IsSessionSummarizing(sessionId) && history.Count > KEEP_RECENT_MESSAGES + 2)
            {
                // 异步触发总结，不阻塞当前请求
                _ = SummarizeOldMessagesAsync(sessionId, history);
            }

            System.Diagnostics.Debug.WriteLine($"[BuildMessageHistory] 系统:{systemTokens} 历史:{historyTokens}({selectedHistory.Count}条) 当前:{currentMsgTokens} 总计:{systemTokens + historyTokens + currentMsgTokens}");

            return messages;
        }

        private List<AIToolDefinition> BuildToolDefinitions(string preferredToolName, ToolExecutionSettings toolSettings, string mode)
        {
            return GisToolDefinitionBuilder.Build(
                _tools,
                preferredToolName,
                mode,
                toolName => IsToolAvailableForCurrentContext(toolName, toolSettings, mode));
        }

        private static string BuildToolParameterSignature(string toolName, JObject parameters)
        {
            var normalizedTool = (toolName ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedParameters = NormalizeTokenForSignature(parameters ?? new JObject());
            return normalizedTool + "|" + JsonConvert.SerializeObject(normalizedParameters);
        }

        private static string BuildParametersPreview(JObject parameters)
        {
            var json = JsonConvert.SerializeObject(NormalizeTokenForSignature(parameters ?? new JObject()));
            if (json.Length <= 180)
            {
                return json;
            }

            return json.Substring(0, 180) + "...";
        }

        private static string BuildToolResultTextForModel(ToolResult toolResult, bool sensitiveMode)
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

            if (!sensitiveMode)
            {
                return JsonConvert.SerializeObject(new
                {
                    success = toolResult.Success,
                    message = toolResult.Message,
                    error = toolResult.Error,
                    data = toolResult.Data
                });
            }

            return JsonConvert.SerializeObject(new
            {
                success = toolResult.Success,
                message = toolResult.Success ? SensitiveModeToolResultPlaceholder : (toolResult.Error ?? "工具执行失败"),
                error = toolResult.Success ? (string)null : toolResult.Error,
                data = new
                {
                    redacted = true,
                    note = SensitiveModeToolResultPlaceholder
                }
            });
        }

        private static string BuildToolRunningPreview(JObject parameters)
        {
            var parameterText = SerializeForToolCard(parameters);
            if (string.IsNullOrWhiteSpace(parameterText))
            {
                return "参数: {}";
            }

            return "执行参数:\n" + parameterText;
        }

        private static string BuildToolPreview(string toolName, JObject parameters, ToolResult toolResult)
        {
            if (toolResult == null)
            {
                return string.Empty;
            }

            var blocks = new List<string>
            {
                "执行参数:\n" + SerializeForToolCard(parameters)
            };

            if (!toolResult.Success && !string.IsNullOrWhiteSpace(toolResult.Error))
            {
                blocks.Add("错误信息:\n" + toolResult.Error.Trim());
            }

            if (string.Equals(toolName, WebFetchToolName, StringComparison.OrdinalIgnoreCase) && toolResult.Data != null)
            {
                try
                {
                    var data = JObject.FromObject(toolResult.Data);
                    var url = data["url"]?.ToString();
                    var content = data["content"]?.ToString();
                    var links = data["links"] as JArray;
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        blocks.Add("执行结果:\n" + (toolResult.Message ?? string.Empty));
                        return string.Join("\n\n", blocks.Where(b => !string.IsNullOrWhiteSpace(b)));
                    }

                    if (content.Length > 1200)
                    {
                        content = content.Substring(0, 1200) + "\n...（已截断）";
                    }

                    if (string.IsNullOrWhiteSpace(url))
                    {
                        blocks.Add("执行结果:\n" + BuildFetchPreview(content, links));
                        return string.Join("\n\n", blocks.Where(b => !string.IsNullOrWhiteSpace(b)));
                    }

                    blocks.Add($"执行结果:\n来源: {url}\n{BuildFetchPreview(content, links)}");
                    return string.Join("\n\n", blocks.Where(b => !string.IsNullOrWhiteSpace(b)));
                }
                catch
                {
                    blocks.Add("执行结果:\n" + (toolResult.Message ?? string.Empty));
                    return string.Join("\n\n", blocks.Where(b => !string.IsNullOrWhiteSpace(b)));
                }
            }

            var payload = new JObject
            {
                ["success"] = toolResult.Success,
                ["message"] = toolResult.Message,
                ["error"] = toolResult.Error,
                ["data"] = toolResult.Data != null ? JToken.FromObject(toolResult.Data) : null
            };
            blocks.Add("执行结果:\n" + SerializeForToolCard(payload));
            return string.Join("\n\n", blocks.Where(b => !string.IsNullOrWhiteSpace(b)));
        }

        private static string BuildFetchPreview(string content, JArray links)
        {
            if (links == null || links.Count == 0)
            {
                return content;
            }

            var topLinks = links
                .OfType<JValue>()
                .Select(v => v.ToString())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Take(5)
                .ToList();

            if (topLinks.Count == 0)
            {
                return content;
            }

            var suffix = "\n\n可继续抓取链接:\n" + string.Join("\n", topLinks.Select((link, i) => $"{i + 1}. {link}"));
            return content + suffix;
        }

        private static string BuildDirectToolFallbackResponse(string toolName, ToolResult toolResult)
        {
            var toolDisplay = string.Equals(toolName, WebFetchToolName, StringComparison.OrdinalIgnoreCase)
                ? "网页抓取"
                : "工具执行";

            if (toolResult == null)
            {
                return $"{toolDisplay}不可用：工具返回为空。";
            }

            if (!toolResult.Success)
            {
                return $"{toolDisplay}失败：{toolResult.Error ?? "未知错误"}";
            }

            var preview = BuildToolMessagePreview(toolName, toolResult);
            if (string.IsNullOrWhiteSpace(preview))
            {
                return toolResult.Message ?? $"{toolDisplay}已完成。";
            }

            return $"{toolDisplay}已完成，以下是结果：\n{preview}";
        }

        private static string BuildToolMessagePreview(string toolName, ToolResult toolResult)
        {
            if (toolResult == null)
            {
                return string.Empty;
            }

            if (string.Equals(toolName, WebFetchToolName, StringComparison.OrdinalIgnoreCase))
            {
                var richPreview = BuildToolPreview(toolName, null, toolResult);
                if (!string.IsNullOrWhiteSpace(richPreview))
                {
                    return richPreview;
                }
            }

            return toolResult.Message ?? string.Empty;
        }

        /// <summary>
        /// 创建新会话
        /// </summary>
        public void CreateNewSession(string title = null)
        {
            // 只生成新的会话ID,不立即写入数据库
            // 会话将在第一条消息保存时自动创建
            _currentSessionId = Guid.NewGuid().ToString();
        }
    }
}
