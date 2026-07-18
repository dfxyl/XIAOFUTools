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

        private void ConfigureContextWindow(AIServiceConfig service)
        {
            _currentContextWindowTokens = ResolveContextWindow(service);
            System.Diagnostics.Debug.WriteLine($"当前模型上下文窗口: {_currentContextWindowTokens} tokens");
        }


        private static AIServiceConfig CloneServiceConfig(AIServiceConfig service)
        {
            return new AIServiceConfig
            {
                Id = service.Id,
                Name = service.Name,
                ApiEndpoint = service.ApiEndpoint,
                ApiKey = service.ApiKey,
                ModelName = service.ModelName,
                IsDefault = service.IsDefault,
                MaxTokens = service.MaxTokens,
                ContextWindowTokens = service.ContextWindowTokens,
                Temperature = service.Temperature,
                SupportsVision = service.SupportsVision
            };
        }


        private bool IsSessionSummarizing(string sessionId)
        {
            lock (_summarizingLock)
            {
                return _summarizingSessions.Contains(sessionId);
            }
        }


        private bool IsToolAvailableForCurrentContext(string toolName, ToolExecutionSettings settings, string mode = null)
        {
            return IsToolAllowedInCurrentMode(toolName, settings, mode) && IsToolEnabled(toolName, settings, mode);
        }


        private bool TryConsumeToolCalls(
            Dictionary<string, int> usage,
            IEnumerable<string> toolNames,
            out string limitMessage)
        {
            limitMessage = null;
            if (usage == null)
            {
                throw new ArgumentNullException(nameof(usage));
            }

            var grouped = (toolNames ?? Enumerable.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(group => new { ToolName = group.Key, Count = group.Count() })
                .ToList();

            if (grouped.Count == 0)
            {
                return true;
            }

            foreach (var item in grouped)
            {
                usage.TryGetValue(item.ToolName, out var current);
                var next = current + item.Count;
                if (next > SingleToolCallHardLimit)
                {
                    limitMessage = $"工具 {item.ToolName} 在当前任务已调用 {current} 次，本轮再调用 {item.Count} 次将超过上限 {SingleToolCallHardLimit} 次。请开启新任务或调整问题。";
                    return false;
                }
            }

            foreach (var item in grouped)
            {
                usage.TryGetValue(item.ToolName, out var current);
                var next = current + item.Count;
                usage[item.ToolName] = next;
                if (current < SingleToolCallAlertThreshold && next >= SingleToolCallAlertThreshold)
                {
                    System.Diagnostics.Debug.WriteLine($"[ToolUsage] 工具 {item.ToolName} 已达到 {next} 次（提醒阈值 {SingleToolCallAlertThreshold}）");
                }
            }

            return true;
        }

        private bool TryConsumeToolParameterCalls(
            Dictionary<string, int> usage,
            IEnumerable<ToolCallRequest> toolCalls,
            out string limitMessage)
        {
            limitMessage = null;
            if (usage == null)
            {
                throw new ArgumentNullException(nameof(usage));
            }

            var grouped = (toolCalls ?? Enumerable.Empty<ToolCallRequest>())
                .Where(call => call != null && !string.IsNullOrWhiteSpace(call.ToolName))
                .Select(call => new
                {
                    ToolName = call.ToolName.Trim(),
                    Parameters = call.Parameters ?? new JObject(),
                    Signature = BuildToolParameterSignature(call.ToolName, call.Parameters)
                })
                .GroupBy(item => item.Signature, StringComparer.Ordinal)
                .Select(group => new
                {
                    Signature = group.Key,
                    ToolName = group.First().ToolName,
                    Parameters = group.First().Parameters,
                    Count = group.Count()
                })
                .ToList();

            if (grouped.Count == 0)
            {
                return true;
            }

            foreach (var item in grouped)
            {
                usage.TryGetValue(item.Signature, out var current);
                var next = current + item.Count;
                if (next > SameParametersHardLimit)
                {
                    var preview = BuildParametersPreview(item.Parameters);
                    limitMessage = $"工具 {item.ToolName} 的相同参数已调用 {current} 次，本轮再调用 {item.Count} 次将超过同参上限 {SameParametersHardLimit} 次。参数: {preview}";
                    return false;
                }
            }

            foreach (var item in grouped)
            {
                usage.TryGetValue(item.Signature, out var current);
                usage[item.Signature] = current + item.Count;
            }

            return true;
        }

        private async Task<ToolExecutionAllowance> EnsureToolExecutionAllowedAsync(
            IEnumerable<string> toolNames,
            ToolExecutionSettings settings,
            ToolApprovalState approvalState,
            string mode,
            Func<ToolApprovalRequest, CancellationToken, Task<ToolApprovalDecision>> onToolApproval,
            CancellationToken cancellationToken)
        {
            var normalizedTools = (toolNames ?? Enumerable.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToList();

            if (normalizedTools.Count == 0)
            {
                return ToolExecutionAllowance.Allow();
            }

            var disabledTools = normalizedTools
                .Where(name => !IsToolEnabled(name, settings, mode))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (disabledTools.Count > 0)
            {
                return ToolExecutionAllowance.Deny($"以下工具在当前模式未启用: {string.Join(", ", disabledTools)}。请在设置中调整开关后重试。");
            }

            if (approvalState?.AllowForCurrentTurn == true)
            {
                return ToolExecutionAllowance.Allow();
            }

            var needPrompt = normalizedTools.Any(name => NeedPromptBeforeTool(name, settings, mode));
            if (!needPrompt)
            {
                return ToolExecutionAllowance.Allow();
            }

            if (onToolApproval == null)
            {
                return ToolExecutionAllowance.Deny("当前配置要求人工确认工具调用，但前端确认通道不可用。");
            }

            var request = new ToolApprovalRequest
            {
                ToolNames = normalizedTools.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                CallCount = normalizedTools.Count,
                Mode = NormalizeMode(mode),
                Message = $"将执行 {normalizedTools.Count} 次工具调用"
            };

            var decision = await onToolApproval(request, cancellationToken);
            var normalizedDecision = (decision?.Decision ?? string.Empty).Trim().ToLowerInvariant();

            switch (normalizedDecision)
            {
                case "allow":
                    if (settings != null)
                    {
                        settings.PromptBeforeToolInChat = false;
                        _dataStore?.SaveToolExecutionSettings(settings);
                    }

                    if (approvalState != null)
                    {
                        approvalState.AllowForCurrentTurn = true;
                    }

                    return ToolExecutionAllowance.Allow();

                case "allow_once":
                    if (approvalState != null)
                    {
                        approvalState.AllowForCurrentTurn = true;
                    }

                    return ToolExecutionAllowance.Allow();

                default:
                    return ToolExecutionAllowance.Deny("用户取消了本次工具调用。");
            }
        }

        private static void AppendSensitiveModeInstruction(List<ChatMessage> messages, ToolExecutionSettings settings)
        {
            if (messages == null || settings?.SensitiveMode != true)
            {
                return;
            }

            messages.Add(new ChatMessage("system",
                "当前处于敏感模式：工具详细数据不会传给你，你只能基于用户文本与脱敏状态作答。严禁猜测或编造具体数值和地理结论；若缺少证据，必须明确说明不确定，并建议用户查看工具块本地结果。"));
        }

        private static void EmitFinalResponseByChunks(string response, Action<string> onChunkReceived)
        {
            if (string.IsNullOrEmpty(response) || onChunkReceived == null)
            {
                return;
            }

            const int chunkSize = 120;
            for (int i = 0; i < response.Length; i += chunkSize)
            {
                var length = Math.Min(chunkSize, response.Length - i);
                onChunkReceived(response.Substring(i, length));
            }
        }

        /// <summary>
        /// 切换会话
        /// </summary>
        public void SwitchSession(string sessionId)
        {
            _currentSessionId = sessionId;
        }

        /// <summary>
        /// 清除当前会话历史
        /// </summary>
        public void ClearCurrentSession()
        {
            _dataStore?.ClearConversationHistory(_currentSessionId);
        }
    }
}
