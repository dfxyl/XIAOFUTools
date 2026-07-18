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
        /// 发送消息并获取流式响应
        /// </summary>
        public async Task<string> SendMessageAsync(
            string userMessage,
            Action<string> onChunkReceived,
            CancellationToken cancellationToken = default,
            Action<string> onReasoningReceived = null,
            Action<ToolExecutionNotice> onToolExecution = null,
            Func<ToolApprovalRequest, CancellationToken, Task<ToolApprovalDecision>> onToolApproval = null,
            Action<string> onAssistantTurnStarted = null,
            Action<string> onAssistantTurnEnded = null,
            List<string> images = null,
            string selectedTool = null,
            string sessionId = null,
            string selectedModel = null,
            string requestMode = null)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                throw new ArgumentException("消息不能为空", nameof(userMessage));

            if (_dataStore == null)
            {
                throw new InvalidOperationException(
                    "数据库不可用，无法保存对话历史。请检查磁盘空间和写入权限。");
            }

            var serviceConfig = ResolveServiceConfig(selectedModel);
            if (serviceConfig == null)
            {
                throw new InvalidOperationException(
                    "AI服务未配置。请点击设置按钮添加AI模型配置（API地址、密钥等）。");
            }

            var effectiveMode = NormalizeMode(requestMode ?? _currentMode);
            var contextWindowTokens = ResolveContextWindow(serviceConfig);
            using var requestAiService = new OpenAICompatibleService(serviceConfig);

            try
            {
                var activeSessionId = string.IsNullOrWhiteSpace(sessionId) ? _currentSessionId : sessionId;
                System.Diagnostics.Debug.WriteLine($"[Agent] SendMessageAsync 被调用, 会话: {activeSessionId}, 模型: {serviceConfig.ModelName}, 模式: {effectiveMode}, 消息: {userMessage}, 图片数量: {images?.Count ?? 0}");

                // 命令前缀或显式指定工具时，强制首轮tool_choice
                var toolRequest = ResolveToolRequest(userMessage, selectedTool);
                string effectiveMessage = toolRequest?.Query ?? userMessage;
                
                // 先构建消息历史(不包含当前消息)
                var messages = BuildMessageHistory(activeSessionId, effectiveMessage, effectiveMode, contextWindowTokens, images);
                
                System.Diagnostics.Debug.WriteLine($"[Agent] 构建的消息历史数量: {messages.Count}");
                foreach (var msg in messages)
                {
                    var contentStr = msg.Content?.ToString() ?? "";
                    System.Diagnostics.Debug.WriteLine($"  - {msg.Role}: {(contentStr.Length > 50 ? contentStr.Substring(0, 50) + "..." : contentStr)}");
                }
                
                // 再保存用户消息到数据库(包含图片)
                var imagesJson = images != null && images.Count > 0 ? JsonConvert.SerializeObject(images) : null;
                _dataStore.SaveMessage(activeSessionId, "user", userMessage, 0, null, imagesJson);
                System.Diagnostics.Debug.WriteLine($"[Agent] 用户消息已保存到数据库");

                // 重置思考内容
                var currentThinking = "";

                var toolSettings = LoadToolExecutionSettings();
                var approvalState = new ToolApprovalState();
                var toolCallUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var toolCallSignatureUsage = new Dictionary<string, int>(StringComparer.Ordinal);

                AppendSensitiveModeInstruction(messages, toolSettings);

                if (!string.IsNullOrWhiteSpace(toolRequest?.ToolName) && !IsToolAvailableForCurrentContext(toolRequest.ToolName, toolSettings, effectiveMode))
                {
                    var modeMessage = toolSettings?.SensitiveMode == true
                        && string.Equals(toolRequest.ToolName, WebFetchToolName, StringComparison.OrdinalIgnoreCase)
                        ? "敏感模式已开启，联网抓取工具已禁用。请关闭敏感模式后再使用该工具。"
                        : $"工具 {toolRequest.ToolName} 在当前模式未启用，请到设置页调整工具开关后再试。";
                    EmitFinalResponseByChunks(modeMessage, onChunkReceived);
                    _dataStore.SaveMessage(activeSessionId, "assistant", modeMessage, 0, currentThinking);
                    return modeMessage;
                }

                var toolDefinitions = BuildToolDefinitions(toolRequest?.ToolName, toolSettings, effectiveMode);
                var firstToolChoice = GisToolDefinitionBuilder.BuildChoice(toolRequest?.ToolName);
                System.Diagnostics.Debug.WriteLine($"[Agent] 工具发布数量: {toolDefinitions.Count}, 模式: {effectiveMode}, 首选工具: {toolRequest?.ToolName ?? "(无)"}");
                var round = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var toolChoice = round == 0 ? firstToolChoice : "auto";
                    var turnId = Guid.NewGuid().ToString("N");
                    onAssistantTurnStarted?.Invoke(turnId);

                    AICompletionResult completion;
                    var turnEnded = false;
                    try
                    {
                        completion = await requestAiService.SendWithToolsStreamAsync(
                            messages,
                            toolDefinitions,
                            onChunkReceived,
                            reasoning =>
                            {
                                currentThinking += reasoning;
                                onReasoningReceived?.Invoke(reasoning);
                            },
                            toolChoice,
                            cancellationToken);
                    }
                    catch (HttpRequestException ex) when (LooksLikeToolsUnsupported(ex.Message))
                    {
                        System.Diagnostics.Debug.WriteLine($"模型不支持tools协议，回退普通流式: {ex.Message}");

                        if (toolRequest?.Parameters != null)
                        {
                            if (!TryConsumeToolCalls(toolCallUsage, new[] { toolRequest.ToolName }, out var directLimitMessage))
                            {
                                EmitFinalResponseByChunks(directLimitMessage, onChunkReceived);
                                _dataStore.SaveMessage(activeSessionId, "assistant", directLimitMessage, 0, currentThinking, null, turnId);
                                onAssistantTurnEnded?.Invoke(turnId);
                                turnEnded = true;
                                return directLimitMessage;
                            }

                            if (!TryConsumeToolParameterCalls(
                                toolCallSignatureUsage,
                                new[]
                                {
                                    new ToolCallRequest
                                    {
                                        ToolName = toolRequest.ToolName,
                                        Parameters = toolRequest.Parameters
                                    }
                                },
                                out var directSameParamLimitMessage))
                            {
                                EmitFinalResponseByChunks(directSameParamLimitMessage, onChunkReceived);
                                _dataStore.SaveMessage(activeSessionId, "assistant", directSameParamLimitMessage, 0, currentThinking, null, turnId);
                                onAssistantTurnEnded?.Invoke(turnId);
                                turnEnded = true;
                                return directSameParamLimitMessage;
                            }

                            var directApproval = await EnsureToolExecutionAllowedAsync(
                                new[] { toolRequest.ToolName },
                                toolSettings,
                                approvalState,
                                effectiveMode,
                                onToolApproval,
                                cancellationToken);

                            if (!directApproval.Allowed)
                            {
                                var blocked = directApproval.Message ?? "工具执行已取消。";
                                EmitFinalResponseByChunks(blocked, onChunkReceived);
                                _dataStore.SaveMessage(activeSessionId, "assistant", blocked, 0, currentThinking, null, turnId);
                                onAssistantTurnEnded?.Invoke(turnId);
                                turnEnded = true;
                                return blocked;
                            }

                            var directCallId = Guid.NewGuid().ToString("N");
                            var directToolCall = new ToolCallRequest
                            {
                                ToolName = toolRequest.ToolName,
                                Parameters = toolRequest.Parameters
                            };

                            onToolExecution?.Invoke(new ToolExecutionNotice
                            {
                                CallId = directCallId,
                                TurnId = turnId,
                                ToolName = toolRequest.ToolName,
                                Status = "running",
                                Message = "正在执行",
                                Preview = BuildToolRunningPreview(directToolCall.Parameters),
                                Parameters = JsonConvert.SerializeObject(directToolCall.Parameters ?? new JObject())
                            });

                            var directToolResult = await ExecuteToolAsync(activeSessionId, directToolCall, turnId, directCallId);
                            var directResponse = BuildDirectToolFallbackResponse(toolRequest.ToolName, directToolResult);
                            EmitFinalResponseByChunks(directResponse, onChunkReceived);

                            onToolExecution?.Invoke(new ToolExecutionNotice
                            {
                                CallId = directCallId,
                                TurnId = turnId,
                                ToolName = toolRequest.ToolName,
                                Status = directToolResult.Success ? "success" : "failed",
                                Message = directToolResult.Success ? "执行完成" : (directToolResult.Error ?? "执行失败"),
                                Preview = BuildToolPreview(toolRequest.ToolName, directToolCall.Parameters, directToolResult),
                                Parameters = JsonConvert.SerializeObject(directToolCall.Parameters ?? new JObject()),
                                Result = SerializeToolResultForNotice(directToolResult)
                            });

                            _dataStore.SaveMessage(activeSessionId, "assistant", directResponse, 0, currentThinking, null, turnId);
                            onAssistantTurnEnded?.Invoke(turnId);
                            turnEnded = true;
                            return directResponse;
                        }

                        var fallbackResponse = await requestAiService.SendMessageStreamAsync(
                            messages,
                            onChunkReceived,
                            cancellationToken,
                            reasoning =>
                            {
                                currentThinking += reasoning;
                                onReasoningReceived?.Invoke(reasoning);
                            });

                        _dataStore.SaveMessage(activeSessionId, "assistant", fallbackResponse, 0, currentThinking, null, turnId);
                        onAssistantTurnEnded?.Invoke(turnId);
                        turnEnded = true;
                        return fallbackResponse;
                    }
                    finally
                    {
                        if (!turnEnded)
                        {
                            onAssistantTurnEnded?.Invoke(turnId);
                        }
                    }

                    if (completion.ToolCalls == null || completion.ToolCalls.Count == 0)
                    {
                        var response = completion.Content ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(response) && !string.IsNullOrWhiteSpace(completion.ReasoningContent))
                        {
                            response = "我已完成思考，但未生成文本回答，请重试一次。";
                            onChunkReceived?.Invoke(response);
                        }
                        else if (string.IsNullOrWhiteSpace(response))
                        {
                            response = $"模型未返回可显示文本内容，finish_reason={completion.FinishReason ?? "unknown"}。";
                            onChunkReceived?.Invoke(response);
                        }

                        _dataStore.SaveMessage(activeSessionId, "assistant", response, 0, currentThinking, null, turnId);
                        return response;
                    }

                    var parsedToolRequests = completion.ToolCalls
                        .Select(toolCall => new ToolCallRequest
                        {
                            ToolName = toolCall.Name,
                            Parameters = ParseToolArguments(toolCall.Arguments)
                        })
                        .ToList();

                    if (!TryConsumeToolCalls(toolCallUsage, parsedToolRequests.Select(c => c.ToolName), out var limitMessage))
                    {
                        EmitFinalResponseByChunks(limitMessage, onChunkReceived);
                        _dataStore.SaveMessage(activeSessionId, "assistant", limitMessage, 0, currentThinking, null, turnId);
                        return limitMessage;
                    }

                    if (!TryConsumeToolParameterCalls(toolCallSignatureUsage, parsedToolRequests, out var sameParamLimitMessage))
                    {
                        EmitFinalResponseByChunks(sameParamLimitMessage, onChunkReceived);
                        _dataStore.SaveMessage(activeSessionId, "assistant", sameParamLimitMessage, 0, currentThinking, null, turnId);
                        return sameParamLimitMessage;
                    }

                    // 工具轮次也持久化一个assistant锚点，确保历史回放时工具卡片位置稳定。
                    _dataStore.SaveMessage(
                        activeSessionId,
                        "assistant",
                        completion.Content ?? string.Empty,
                        0,
                        null,
                        null,
                        turnId);

                    messages.Add(new ChatMessage("assistant", completion.Content)
                    {
                        ReasoningContent = completion.ReasoningContent,
                        ToolCalls = completion.ToolCalls
                    });

                    var toolApproval = await EnsureToolExecutionAllowedAsync(
                        completion.ToolCalls.Select(c => c.Name),
                        toolSettings,
                        approvalState,
                        effectiveMode,
                        onToolApproval,
                        cancellationToken);

                    if (!toolApproval.Allowed)
                    {
                        var deniedText = toolApproval.Message ?? "工具执行已取消。";
                        for (int i = 0; i < completion.ToolCalls.Count; i++)
                        {
                            var toolCall = completion.ToolCalls[i];
                            var deniedParameters = parsedToolRequests.Count > i ? parsedToolRequests[i].Parameters : new JObject();

                            onToolExecution?.Invoke(new ToolExecutionNotice
                            {
                                CallId = toolCall.Id,
                                TurnId = turnId,
                                ToolName = toolCall.Name,
                                Status = "failed",
                                Message = deniedText,
                                Preview = deniedText,
                                Parameters = JsonConvert.SerializeObject(deniedParameters ?? new JObject()),
                                Result = JsonConvert.SerializeObject(new
                                {
                                    success = false,
                                    message = deniedText,
                                    error = deniedText,
                                    data = (object)null
                                })
                            });

                            _dataStore.SaveToolCall(
                                activeSessionId,
                                toolCall.Name,
                                JsonConvert.SerializeObject(deniedParameters),
                                deniedText,
                                "denied",
                                turnId,
                                toolCall.Id);

                            var deniedToolResult = JsonConvert.SerializeObject(new
                            {
                                success = false,
                                message = deniedText,
                                error = deniedText,
                                data = (object)null
                            });

                            messages.Add(new ChatMessage("tool", deniedToolResult)
                            {
                                ToolCallId = toolCall.Id
                            });
                        }

                        round++;
                        continue;
                    }

                    var toolExecutionTasks = completion.ToolCalls
                        .Select((toolCall, index) => ExecuteToolCallAsync(
                            activeSessionId,
                            toolCall,
                            index,
                            onToolExecution,
                            cancellationToken,
                            parsedToolRequests[index].Parameters,
                            turnId))
                        .ToList();

                    var toolResponses = await Task.WhenAll(toolExecutionTasks);
                    foreach (var toolResponse in toolResponses.OrderBy(r => r.Index))
                    {
                        messages.Add(new ChatMessage("tool", BuildToolResultTextForModel(toolResponse.ToolResult, toolSettings?.SensitiveMode == true))
                        {
                            ToolCallId = toolResponse.ToolCallId
                        });
                    }

                    messages.Add(new ChatMessage("system",
                        "回答约束：必须严格依据刚刚返回的工具JSON作答；若 rows 为空、summaryGroupCount=0、intersectedPairs=0 或 totalArea=0，必须明确说明本次无结果，不得编造数值或结论。"));

                    round++;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"发送消息失败: {ex.Message}");
                throw;
            }
        }
    }
}
