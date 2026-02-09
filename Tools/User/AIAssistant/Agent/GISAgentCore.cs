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
using XIAOFUTools.Tools.User.AIAssistant.Agent.Infrastructure;
using XIAOFUTools.Tools.User.AIAssistant.Agent.Prompts;
using XIAOFUTools.Tools.User.AIAssistant.Agent.Tools;
using XIAOFUTools.Tools.User.AIAssistant.Agent.Tooling;
using XIAOFUTools.Tools.User.AIAssistant.Database;
using XIAOFUTools.Tools.User.AIAssistant.Services;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent
{
    /// <summary>
    /// GIS Agent核心 - 协调AI服务和GIS工具调用
    /// </summary>
    public class GISAgentCore
    {
        private IAgentDataStore _dataStore;
        private readonly Dictionary<string, IGISTool> _tools;
        private IAIService _aiService;
        private string _currentSessionId;
        private readonly string _chatSystemPrompt;
        private readonly string _agentSystemPrompt;
        private string _currentMode = "chat"; // "chat" or "agent"
        private string _currentThinking = ""; // 当前的思考内容
        private string _initError; // 初始化错误信息（用于延迟提示）
        private const string WebFetchToolName = "web_fetch";
        private const int SingleToolCallAlertThreshold = 30;
        private const int SingleToolCallHardLimit = 50;
        private const int SameParametersHardLimit = 20;
        private const string SensitiveModeToolResultPlaceholder = "敏感模式已开启：工具已在本地执行，详细结果不回传模型。";

        /// <summary>
        /// 是否已完全初始化（DB + AI服务均可用）
        /// </summary>
        public bool IsFullyInitialized => _dataStore != null && _aiService != null;

        /// <summary>
        /// 获取初始化错误信息
        /// </summary>
        public string InitializationError => _initError;

        public GISAgentCore()
            : this(null)
        {
        }

        internal GISAgentCore(IAgentDataStore dataStore)
        {
            _tools = new Dictionary<string, IGISTool>(StringComparer.OrdinalIgnoreCase);
            _currentSessionId = Guid.NewGuid().ToString();

            _chatSystemPrompt = SystemPromptProvider.GetChatPrompt();
            _agentSystemPrompt = SystemPromptProvider.GetAgentPrompt();

            System.Diagnostics.Debug.WriteLine("开始初始化GISAgentCore...");

            // 1. 初始化数据库（允许失败）
            try
            {
                _dataStore = dataStore ?? new DatabaseAgentDataStore(DatabaseManager.Instance);
                System.Diagnostics.Debug.WriteLine("Agent数据存储初始化成功");
            }
            catch (Exception ex)
            {
                _dataStore = null;
                _initError = $"数据库初始化失败: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"DatabaseManager初始化失败（将以降级模式运行）: {ex.Message}");
            }

            // 2. 初始化AI服务（允许失败，用户可稍后在设置中配置）
            try
            {
                InitializeAIService();
            }
            catch (Exception ex)
            {
                _aiService = null;
                var aiError = $"AI服务初始化失败: {ex.Message}";
                _initError = _initError != null ? $"{_initError}\n{aiError}" : aiError;
                System.Diagnostics.Debug.WriteLine($"AI服务初始化失败（用户可稍后配置）: {ex.Message}");
            }

            // 3. 注册内置工具（不应失败）
            RegisterBuiltInTools();
            
            System.Diagnostics.Debug.WriteLine($"GISAgentCore初始化完成, 完全初始化: {IsFullyInitialized}");
        }

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
            
            _aiService = new OpenAICompatibleService(defaultService);
            System.Diagnostics.Debug.WriteLine("AI服务初始化成功");
        }

        private void RegisterBuiltInTools()
        {
            // 默认启用只读内置工具。
            RegisterTool(new WebFetchTool());
            RegisterTool(new ProjectSnapshotTool());
            RegisterTool(new LayerListTool());
            RegisterTool(new LayerSchemaTool());
            RegisterTool(new SelectionSummaryTool());
            RegisterTool(new LayerQueryTool());
            RegisterTool(new FieldProfileTool());
            RegisterTool(new OverlayIntersectSummaryTool());
            RegisterTool(new LayerBufferTool());
            RegisterTool(new LayerClipTool());
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
        /// 获取所有可用工具
        /// </summary>
        public IReadOnlyDictionary<string, IGISTool> GetAvailableTools()
        {
            return _tools;
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
            string selectedTool = null)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                throw new ArgumentException("消息不能为空", nameof(userMessage));

            if (_aiService == null)
            {
                throw new InvalidOperationException(
                    "AI服务未配置。请点击设置按钮添加AI模型配置（API地址、密钥等）。");
            }

            if (_dataStore == null)
            {
                throw new InvalidOperationException(
                    "数据库不可用，无法保存对话历史。请检查磁盘空间和写入权限。");
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[Agent] SendMessageAsync 被调用, 消息: {userMessage}, 图片数量: {images?.Count ?? 0}");

                // 命令前缀或显式指定工具时，强制首轮tool_choice
                var toolRequest = ResolveToolRequest(userMessage, selectedTool);
                string effectiveMessage = toolRequest?.Query ?? userMessage;
                
                // 先构建消息历史(不包含当前消息)
                var messages = BuildMessageHistory(effectiveMessage, images);
                
                System.Diagnostics.Debug.WriteLine($"[Agent] 构建的消息历史数量: {messages.Count}");
                foreach (var msg in messages)
                {
                    var contentStr = msg.Content?.ToString() ?? "";
                    System.Diagnostics.Debug.WriteLine($"  - {msg.Role}: {(contentStr.Length > 50 ? contentStr.Substring(0, 50) + "..." : contentStr)}");
                }
                
                // 再保存用户消息到数据库(包含图片)
                var imagesJson = images != null && images.Count > 0 ? JsonConvert.SerializeObject(images) : null;
                _dataStore.SaveMessage(_currentSessionId, "user", userMessage, 0, null, imagesJson);
                System.Diagnostics.Debug.WriteLine($"[Agent] 用户消息已保存到数据库");

                // 重置思考内容
                _currentThinking = "";

                var toolSettings = LoadToolExecutionSettings();
                var approvalState = new ToolApprovalState();
                var toolCallUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var toolCallSignatureUsage = new Dictionary<string, int>(StringComparer.Ordinal);

                AppendSensitiveModeInstruction(messages, toolSettings);

                if (!string.IsNullOrWhiteSpace(toolRequest?.ToolName) && !IsToolAvailableForCurrentContext(toolRequest.ToolName, toolSettings))
                {
                    var modeMessage = toolSettings?.SensitiveMode == true
                        && string.Equals(toolRequest.ToolName, WebFetchToolName, StringComparison.OrdinalIgnoreCase)
                        ? "敏感模式已开启，联网抓取工具已禁用。请关闭敏感模式后再使用该工具。"
                        : $"工具 {toolRequest.ToolName} 在当前模式未启用，请到设置页调整工具开关后再试。";
                    EmitFinalResponseByChunks(modeMessage, onChunkReceived);
                    _dataStore.SaveMessage(_currentSessionId, "assistant", modeMessage, 0, _currentThinking);
                    return modeMessage;
                }

                var toolDefinitions = BuildToolDefinitions(toolRequest?.ToolName, toolSettings);
                var firstToolChoice = BuildToolChoice(toolRequest?.ToolName);
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
                        completion = await _aiService.SendWithToolsStreamAsync(
                            messages,
                            toolDefinitions,
                            onChunkReceived,
                            reasoning =>
                            {
                                _currentThinking += reasoning;
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
                                _dataStore.SaveMessage(_currentSessionId, "assistant", directLimitMessage, 0, _currentThinking, null, turnId);
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
                                _dataStore.SaveMessage(_currentSessionId, "assistant", directSameParamLimitMessage, 0, _currentThinking, null, turnId);
                                onAssistantTurnEnded?.Invoke(turnId);
                                turnEnded = true;
                                return directSameParamLimitMessage;
                            }

                            var directApproval = await EnsureToolExecutionAllowedAsync(
                                new[] { toolRequest.ToolName },
                                toolSettings,
                                approvalState,
                                onToolApproval,
                                cancellationToken);

                            if (!directApproval.Allowed)
                            {
                                var blocked = directApproval.Message ?? "工具执行已取消。";
                                EmitFinalResponseByChunks(blocked, onChunkReceived);
                                _dataStore.SaveMessage(_currentSessionId, "assistant", blocked, 0, _currentThinking, null, turnId);
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

                            var directToolResult = await ExecuteToolAsync(directToolCall, turnId, directCallId);
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

                            _dataStore.SaveMessage(_currentSessionId, "assistant", directResponse, 0, _currentThinking, null, turnId);
                            onAssistantTurnEnded?.Invoke(turnId);
                            turnEnded = true;
                            return directResponse;
                        }

                        var fallbackResponse = await _aiService.SendMessageStreamAsync(
                            messages,
                            onChunkReceived,
                            cancellationToken,
                            reasoning =>
                            {
                                _currentThinking += reasoning;
                                onReasoningReceived?.Invoke(reasoning);
                            });

                        _dataStore.SaveMessage(_currentSessionId, "assistant", fallbackResponse, 0, _currentThinking, null, turnId);
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

                        _dataStore.SaveMessage(_currentSessionId, "assistant", response, 0, _currentThinking, null, turnId);
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
                        _dataStore.SaveMessage(_currentSessionId, "assistant", limitMessage, 0, _currentThinking, null, turnId);
                        return limitMessage;
                    }

                    if (!TryConsumeToolParameterCalls(toolCallSignatureUsage, parsedToolRequests, out var sameParamLimitMessage))
                    {
                        EmitFinalResponseByChunks(sameParamLimitMessage, onChunkReceived);
                        _dataStore.SaveMessage(_currentSessionId, "assistant", sameParamLimitMessage, 0, _currentThinking, null, turnId);
                        return sameParamLimitMessage;
                    }

                    // 工具轮次也持久化一个assistant锚点，确保历史回放时工具卡片位置稳定。
                    _dataStore.SaveMessage(
                        _currentSessionId,
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
                                _currentSessionId,
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

        // ============ 上下文管理常量 ============
        /// <summary>上下文token上限（留出回复空间）</summary>
        private const int MAX_CONTEXT_TOKENS = 12000;
        /// <summary>触发总结的token阈值</summary>
        private const int SUMMARIZE_THRESHOLD = 8000;
        /// <summary>总结后保留的最近消息条数</summary>
        private const int KEEP_RECENT_MESSAGES = 4;
        /// <summary>是否正在执行总结</summary>
        private bool _isSummarizing = false;

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

        /// <summary>
        /// 构建消息历史（带自动总结压缩）
        /// </summary>
        private List<ChatMessage> BuildMessageHistory(string currentMessage, List<string> images = null)
        {
            // 根据当前模式选择提示词
            var systemPrompt = _currentMode == "agent" ? _agentSystemPrompt : _chatSystemPrompt;
            
            var messages = new List<ChatMessage>
            {
                new ChatMessage("system", systemPrompt)
            };

            int systemTokens = EstimateTokenCount(systemPrompt);
            int currentMsgTokens = EstimateTokenCount(currentMessage);
            int availableTokens = MAX_CONTEXT_TOKENS - systemTokens - currentMsgTokens - 100; // 100为安全余量

            // 尝试获取已有的上下文总结
            ContextSummary existingSummary = null;
            try
            {
                existingSummary = _dataStore.GetLatestContextSummary(_currentSessionId);
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
                history = _dataStore.GetMessagesAfterId(_currentSessionId, existingSummary.SummarizedUpToId, 50);
                
                var summaryMsg = $"[以下是之前对话的总结]\n{existingSummary.Summary}\n[总结结束，以下是最近的对话]";
                messages.Add(new ChatMessage("system", summaryMsg));
                availableTokens -= EstimateTokenCount(summaryMsg);
            }
            else
            {
                // 无总结：获取全部历史（限制50条）
                history = _dataStore.GetConversationHistory(_currentSessionId, 50);
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
            
            if (totalHistoryTokens > SUMMARIZE_THRESHOLD && !_isSummarizing && history.Count > KEEP_RECENT_MESSAGES + 2)
            {
                // 异步触发总结，不阻塞当前请求
                _ = SummarizeOldMessagesAsync(history);
            }

            System.Diagnostics.Debug.WriteLine($"[BuildMessageHistory] 系统:{systemTokens} 历史:{historyTokens}({selectedHistory.Count}条) 当前:{currentMsgTokens} 总计:{systemTokens + historyTokens + currentMsgTokens}");

            return messages;
        }

        private ToolExecutionRequest ResolveToolRequest(string userMessage, string selectedTool)
        {
            return ToolRequestResolver.Resolve(userMessage, selectedTool, WebFetchToolName);
        }

        private List<AIToolDefinition> BuildToolDefinitions(string preferredToolName, ToolExecutionSettings toolSettings)
        {
            var result = new List<AIToolDefinition>();
            if (_tools.Count == 0)
            {
                return result;
            }

            IEnumerable<IGISTool> targetTools;
            if (!string.IsNullOrWhiteSpace(preferredToolName) && _tools.TryGetValue(preferredToolName, out var preferredTool))
            {
                targetTools = IsToolAvailableForCurrentContext(preferredToolName, toolSettings)
                    ? new[] { preferredTool }
                    : Enumerable.Empty<IGISTool>();
            }
            else
            {
                targetTools = _tools.Values.Where(t => IsToolAvailableForCurrentContext(t.Name, toolSettings));
            }

            foreach (var tool in targetTools)
            {
                result.Add(new AIToolDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    ParametersSchema = tool.ParametersSchema
                });
            }

            return result;
        }

        private static object BuildToolChoice(string preferredToolName)
        {
            if (string.IsNullOrWhiteSpace(preferredToolName))
            {
                return "auto";
            }

            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = preferredToolName
                }
            };
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

        private ToolExecutionSettings LoadToolExecutionSettings()
        {
            try
            {
                return _dataStore?.GetToolExecutionSettings() ?? new ToolExecutionSettings();
            }
            catch
            {
                return new ToolExecutionSettings();
            }
        }

        private bool IsToolEnabled(string toolName, ToolExecutionSettings settings)
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

            return effectiveSettings.IsToolEnabled(toolName, _currentMode);
        }

        private bool IsToolAvailableForCurrentContext(string toolName, ToolExecutionSettings settings)
        {
            return IsToolAllowedInCurrentMode(toolName, settings) && IsToolEnabled(toolName, settings);
        }

        private bool IsToolAllowedInCurrentMode(string toolName, ToolExecutionSettings settings)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return false;
            }

            var effectiveSettings = settings ?? new ToolExecutionSettings();
            return effectiveSettings.IsToolEnabled(toolName, _currentMode);
        }

        private bool NeedPromptBeforeTool(string toolName, ToolExecutionSettings settings)
        {
            if (string.Equals(_currentMode, "agent", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return settings?.PromptBeforeToolInChat == true;
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

        private static string BuildToolParameterSignature(string toolName, JObject parameters)
        {
            var normalizedTool = (toolName ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedParameters = NormalizeTokenForSignature(parameters ?? new JObject());
            return normalizedTool + "|" + JsonConvert.SerializeObject(normalizedParameters);
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

        private static string BuildParametersPreview(JObject parameters)
        {
            var json = JsonConvert.SerializeObject(NormalizeTokenForSignature(parameters ?? new JObject()));
            if (json.Length <= 180)
            {
                return json;
            }

            return json.Substring(0, 180) + "...";
        }

        private async Task<ToolExecutionAllowance> EnsureToolExecutionAllowedAsync(
            IEnumerable<string> toolNames,
            ToolExecutionSettings settings,
            ToolApprovalState approvalState,
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
                .Where(name => !IsToolEnabled(name, settings))
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

            var needPrompt = normalizedTools.Any(name => NeedPromptBeforeTool(name, settings));
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
                Mode = _currentMode,
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

        private async Task<ToolCallExecutionResult> ExecuteToolCallAsync(
            AIToolCall toolCall,
            int index,
            Action<ToolExecutionNotice> onToolExecution,
            CancellationToken cancellationToken,
            JObject parsedParameters = null,
            string turnId = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onToolExecution?.Invoke(new ToolExecutionNotice
            {
                CallId = toolCall.Id,
                TurnId = turnId,
                ToolName = toolCall.Name,
                Status = "running",
                Message = "正在执行",
                Preview = BuildToolRunningPreview(parsedParameters ?? ParseToolArguments(toolCall.Arguments)),
                Parameters = JsonConvert.SerializeObject(parsedParameters ?? ParseToolArguments(toolCall.Arguments) ?? new JObject())
            });

            var toolRequestFromModel = new ToolCallRequest
            {
                ToolName = toolCall.Name,
                Parameters = parsedParameters ?? ParseToolArguments(toolCall.Arguments)
            };

            var toolResult = await ExecuteToolAsync(toolRequestFromModel, turnId, toolCall.Id);
            onToolExecution?.Invoke(new ToolExecutionNotice
            {
                CallId = toolCall.Id,
                TurnId = turnId,
                ToolName = toolCall.Name,
                Status = toolResult.Success ? "success" : "failed",
                Message = toolResult.Success ? "执行完成" : (toolResult.Error ?? "执行失败"),
                Preview = BuildToolPreview(toolCall.Name, toolRequestFromModel.Parameters, toolResult),
                Parameters = JsonConvert.SerializeObject(toolRequestFromModel.Parameters ?? new JObject()),
                Result = SerializeToolResultForNotice(toolResult)
            });

            var toolResultText = JsonConvert.SerializeObject(new
            {
                success = toolResult.Success,
                message = toolResult.Message,
                error = toolResult.Error,
                data = toolResult.Data
            });

            return new ToolCallExecutionResult
            {
                Index = index,
                ToolCallId = toolCall.Id,
                ToolResult = toolResult,
                ToolResultText = toolResultText
            };
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

        private sealed class ToolApprovalState
        {
            public bool AllowForCurrentTurn { get; set; }
        }

        private sealed class ToolExecutionAllowance
        {
            public bool Allowed { get; private set; }
            public string Message { get; private set; }

            public static ToolExecutionAllowance Allow()
            {
                return new ToolExecutionAllowance { Allowed = true };
            }

            public static ToolExecutionAllowance Deny(string message)
            {
                return new ToolExecutionAllowance
                {
                    Allowed = false,
                    Message = string.IsNullOrWhiteSpace(message) ? "工具执行已取消。" : message
                };
            }
        }

        private sealed class ToolCallExecutionResult
        {
            public int Index { get; set; }
            public string ToolCallId { get; set; }
            public ToolResult ToolResult { get; set; }
            public string ToolResultText { get; set; }
        }

        /// <summary>
        /// 异步总结旧消息（后台执行，不阻塞用户交互）
        /// </summary>
        private async Task SummarizeOldMessagesAsync(List<ConversationMessage> history)
        {
            if (_isSummarizing || _aiService == null) return;
            
            _isSummarizing = true;
            System.Diagnostics.Debug.WriteLine("[Summarize] 开始异步总结旧消息...");
            
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
                        _currentSessionId,
                        summary,
                        lastSummarizedMsg.Id,
                        messagesToSummarize.Count,
                        summaryTokens);
                    
                    System.Diagnostics.Debug.WriteLine($"[Summarize] 总结完成: {messagesToSummarize.Count}条消息 -> {summaryTokens} tokens");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Summarize] 总结失败: {ex.Message}");
            }
            finally
            {
                _isSummarizing = false;
            }
        }

        /// <summary>
        /// 执行工具调用
        /// </summary>
        private async Task<ToolResult> ExecuteToolAsync(ToolCallRequest toolCall, string turnId = null, string callId = null)
        {
            if (!_tools.ContainsKey(toolCall.ToolName))
            {
                var missingToolError = $"未找到工具: {toolCall.ToolName}";
                _dataStore.SaveToolCall(
                    _currentSessionId,
                    toolCall.ToolName,
                    JsonConvert.SerializeObject(toolCall.Parameters),
                    missingToolError,
                    "error",
                    turnId,
                    callId);
                return ToolResult.CreateError(missingToolError);
            }

            try
            {
                var tool = _tools[toolCall.ToolName];
                var result = await tool.ExecuteAsync(toolCall.Parameters);
                result = await EnsureFeatureOutputAddedToMapAsync(result);

                // 保存工具调用记录
                _dataStore.SaveToolCall(
                    _currentSessionId,
                    toolCall.ToolName,
                    JsonConvert.SerializeObject(toolCall.Parameters),
                    JsonConvert.SerializeObject(result),
                    result.Success ? "success" : "failed",
                    turnId,
                    callId);

                return result;
            }
            catch (Exception ex)
            {
                var error = $"工具执行失败: {ex.Message}";
                _dataStore.SaveToolCall(
                    _currentSessionId,
                    toolCall.ToolName,
                    JsonConvert.SerializeObject(toolCall.Parameters),
                    error,
                    "error",
                    turnId,
                    callId);
                
                return ToolResult.CreateError(error);
            }
        }

        private static async Task<ToolResult> EnsureFeatureOutputAddedToMapAsync(ToolResult result)
        {
            if (result == null || !result.Success || result.Data == null)
            {
                return result;
            }

            JObject data;
            try
            {
                data = result.Data as JObject ?? JObject.FromObject(result.Data);
            }
            catch
            {
                return result;
            }

            var outputFeatureClass = data["outputFeatureClass"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFeatureClass))
            {
                return result;
            }

            var addedToMap = data["addedToMap"]?.ToObject<bool?>() ?? false;
            if (!addedToMap)
            {
                var outputName = data["outputName"]?.ToString();
                if (string.IsNullOrWhiteSpace(outputName))
                {
                    outputName = Path.GetFileName(outputFeatureClass);
                }

                var mapName = data["mapName"]?.ToString();
                var ensureResult = await ToolSafetyHelpers.EnsureOutputLayerVisibleAsync(outputFeatureClass, outputName, mapName);
                data["addedToMap"] = ensureResult;

                if (!ensureResult)
                {
                    if (data["warnings"] is not JArray warnings)
                    {
                        warnings = new JArray();
                        data["warnings"] = warnings;
                    }

                    warnings.Add("输出已生成，但自动添加到地图失败，可在目录窗口手动添加。");
                }
            }
            else if (data["addedToMap"] == null)
            {
                data["addedToMap"] = true;
            }

            result.Data = data;
            return result;
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

        /// <summary>
        /// 重新加载AI服务（设置变更后调用，使新配置立即生效）
        /// </summary>
        public void ReloadAIService()
        {
            try
            {
                // 释放旧服务
                _aiService?.Dispose();
                _aiService = null;
                
                // 重新初始化
                InitializeAIService();
                
                System.Diagnostics.Debug.WriteLine($"[ReloadAIService] AI服务已重新加载, 可用: {_aiService != null}");
            }
            catch (Exception ex)
            {
                _aiService = null;
                System.Diagnostics.Debug.WriteLine($"[ReloadAIService] 重新加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前会话ID
        /// </summary>
        public string GetCurrentSessionId()
        {
            return _currentSessionId;
        }

        /// <summary>
        /// 切换会话
        /// </summary>
        public void SwitchSession(string sessionId)
        {
            _currentSessionId = sessionId;
        }

        /// <summary>
        /// 设置工作模式
        /// </summary>
        /// <param name="mode">"chat" 或 "agent"</param>
        public void SetMode(string mode)
        {
            if (mode == "chat" || mode == "agent")
            {
                _currentMode = mode;
                System.Diagnostics.Debug.WriteLine($"已切换到 {mode} 模式");
            }
        }

        /// <summary>
        /// 获取当前模式
        /// </summary>
        public string GetCurrentMode()
        {
            return _currentMode;
        }

        /// <summary>
        /// 清除当前会话历史
        /// </summary>
        public void ClearCurrentSession()
        {
            _dataStore?.ClearConversationHistory(_currentSessionId);
        }
    }

    /// <summary>
    /// 工具调用请求
    /// </summary>
    public class ToolCallRequest
    {
        public string ToolName { get; set; }
        public JObject Parameters { get; set; }
    }

    internal sealed class ToolExecutionRequest
    {
        public string ToolName { get; set; }
        public string Query { get; set; }
        public JObject Parameters { get; set; }
    }

    public sealed class ToolExecutionNotice
    {
        public string CallId { get; set; }
        public string TurnId { get; set; }
        public string ToolName { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string Preview { get; set; }
        public string Parameters { get; set; }
        public string Result { get; set; }
    }

    public sealed class ToolApprovalRequest
    {
        public List<string> ToolNames { get; set; } = new List<string>();
        public int CallCount { get; set; }
        public string Mode { get; set; }
        public string Message { get; set; }
    }

    public sealed class ToolApprovalDecision
    {
        /// <summary>
        /// allow_once | allow | cancel
        /// </summary>
        public string Decision { get; set; }
    }
}
