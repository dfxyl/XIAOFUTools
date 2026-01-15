using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Tools.User.AIAssistant.Database;
using XIAOFUTools.Tools.User.AIAssistant.Services;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent
{
    /// <summary>
    /// GIS Agent核心 - 协调AI服务和GIS工具调用
    /// </summary>
    public class GISAgentCore
    {
        private readonly DatabaseManager _dbManager;
        private readonly Dictionary<string, IGISTool> _tools;
        private IAIService _aiService;
        private string _currentSessionId;
        private readonly string _chatSystemPrompt;
        private readonly string _agentSystemPrompt;
        private string _currentMode = "chat"; // "chat" or "agent"
        private string _currentThinking = ""; // 当前的思考内容

        public GISAgentCore()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始初始化GISAgentCore...");
                
                _dbManager = DatabaseManager.Instance;
                System.Diagnostics.Debug.WriteLine("DatabaseManager实例获取成功");
                
                _tools = new Dictionary<string, IGISTool>();
                _currentSessionId = Guid.NewGuid().ToString();
                
                // 初始化Chat模式提示词
                _chatSystemPrompt = @"你是一个专业的GIS助手，精通地理信息系统(GIS)相关的知识和技术。

你的主要职责是：
- 回答用户关于GIS的问题，包括概念、原理、方法等
- 提供专业的建议和指导
- 解释复杂的GIS技术问题
- 帮助用户理解空间数据分析、地图制图、坐标系统等知识

{PROJECT_CONTEXT}

请用清晰、专业、易于理解的语言回答问题。对于复杂的技术问题，请分步骤详细解释。如果用户的问题与当前工程相关，请结合工程上下文给出具体的建议。";
                
                // 初始化Agent模式提示词
                _agentSystemPrompt = @"你是一个智能GIS Agent，具备地理信息系统(GIS)的专业知识和工具调用能力。

你的能力包括：
1. **知识问答**：回答GIS相关的专业问题
2. **工程感知**：了解当前工程的地图、图层、坐标系等信息
3. **工具调用**：调用GIS工具执行具体操作，包括：
   - 空间数据分析和处理
   - 地图制图和可视化
   - 坐标系统转换
   - 空间查询和统计
   - 地理数据格式转换
   - 空间关系分析

{PROJECT_CONTEXT}

工作流程：
1. 理解用户的意图和需求
2. 结合当前工程上下文分析问题
3. 判断是否需要调用GIS工具
4. 如需调用，确定工具和参数
5. 执行工具并解释结果
6. 用专业、清晰的语言与用户交流

注意：当前工具调用功能正在开发中，请优先使用知识问答能力。";

                // 初始化AI服务
                InitializeAIService();
                
                // 注册内置工具
                RegisterBuiltInTools();
                
                System.Diagnostics.Debug.WriteLine("GISAgentCore初始化完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GISAgentCore初始化失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                throw new InvalidOperationException($"GIS Agent初始化失败: {ex.Message}", ex);
            }
        }

        private void InitializeAIService()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("开始初始化AI服务...");
                
                if (_dbManager == null)
                {
                    throw new InvalidOperationException("DatabaseManager未初始化");
                }
                
                var defaultService = _dbManager.GetDefaultService();
                
                if (defaultService == null)
                {
                    System.Diagnostics.Debug.WriteLine("未找到默认AI服务配置，尝试获取所有服务...");
                    var allServices = _dbManager.GetAllServices();
                    
                    if (allServices != null && allServices.Count > 0)
                    {
                        defaultService = allServices[0];
                        System.Diagnostics.Debug.WriteLine($"使用第一个可用服务: {defaultService.Name}");
                    }
                    else
                    {
                        throw new InvalidOperationException("数据库中没有可用的AI服务配置。请检查数据库初始化是否成功。");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"找到AI服务配置: {defaultService.Name}");
                System.Diagnostics.Debug.WriteLine($"API端点: {defaultService.ApiEndpoint}");
                System.Diagnostics.Debug.WriteLine($"模型: {defaultService.ModelName}");
                
                _aiService = new OpenAICompatibleService(defaultService);
                System.Diagnostics.Debug.WriteLine("AI服务初始化成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化AI服务失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                throw new InvalidOperationException($"AI服务初始化失败: {ex.Message}", ex);
            }
        }

        private void RegisterBuiltInTools()
        {
            // 注册内置GIS工具
            // 示例：RegisterTool(new GetMapLayersTool());
            // 示例：RegisterTool(new QueryFeaturesTool());
            // 示例：RegisterTool(new BufferAnalysisTool());
            // 工具实现留空，供后续扩展
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
                
                // 从数据库查找匹配的服务配置
                var allServices = _dbManager.GetAllServices();
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
            List<string> images = null)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                throw new ArgumentException("消息不能为空", nameof(userMessage));

            if (_aiService == null)
            {
                throw new InvalidOperationException("AI服务未初始化");
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[Agent] SendMessageAsync 被调用, 消息: {userMessage}, 图片数量: {images?.Count ?? 0}");
                
                // 先构建消息历史(不包含当前消息)
                var messages = BuildMessageHistory(userMessage, images);
                
                System.Diagnostics.Debug.WriteLine($"[Agent] 构建的消息历史数量: {messages.Count}");
                foreach (var msg in messages)
                {
                    var contentStr = msg.Content?.ToString() ?? "";
                    System.Diagnostics.Debug.WriteLine($"  - {msg.Role}: {(contentStr.Length > 50 ? contentStr.Substring(0, 50) + "..." : contentStr)}");
                }
                
                // 再保存用户消息到数据库(包含图片)
                var imagesJson = images != null && images.Count > 0 ? JsonConvert.SerializeObject(images) : null;
                _dbManager.SaveMessage(_currentSessionId, "user", userMessage, 0, null, imagesJson);
                System.Diagnostics.Debug.WriteLine($"[Agent] 用户消息已保存到数据库");

                // 重置思考内容
                _currentThinking = "";
                
                // 发送到AI服务
                var response = await _aiService.SendMessageStreamAsync(
                    messages,
                    onChunkReceived,
                    cancellationToken,
                    (reasoning) => {
                        _currentThinking += reasoning;
                        onReasoningReceived?.Invoke(reasoning);
                    });

                // 保存AI响应(包括思考内容)
                _dbManager.SaveMessage(_currentSessionId, "assistant", response, 0, _currentThinking);

                // 检查是否需要工具调用
                var toolCall = await DetectToolCallAsync(response);
                if (toolCall != null)
                {
                    var toolResult = await ExecuteToolAsync(toolCall);
                    
                    // 将工具结果反馈给AI
                    var toolResultMessage = $"工具执行结果: {JsonConvert.SerializeObject(toolResult)}";
                    _dbManager.SaveMessage(_currentSessionId, "system", toolResultMessage);
                    
                    // 继续对话获取AI对工具结果的解释
                    messages.Add(new ChatMessage("system", toolResultMessage));
                    var finalResponse = await _aiService.SendMessageStreamAsync(
                        messages,
                        onChunkReceived,
                        cancellationToken,
                        onReasoningReceived);
                    
                    _dbManager.SaveMessage(_currentSessionId, "assistant", finalResponse, 0, _currentThinking);
                    return finalResponse;
                }

                return response;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"发送消息失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 构建消息历史
        /// </summary>
        private List<ChatMessage> BuildMessageHistory(string currentMessage, List<string> images = null)
        {
            // 根据当前模式选择提示词
            var systemPrompt = _currentMode == "agent" ? _agentSystemPrompt : _chatSystemPrompt;
            
            // 注入工程上下文
            var projectContext = Tools.ProjectContextTool.GetSimplifiedProjectContext();
            systemPrompt = systemPrompt.Replace("{PROJECT_CONTEXT}", 
                $"\n\n**当前工程上下文**: {projectContext}\n");
            
            var messages = new List<ChatMessage>
            {
                new ChatMessage("system", systemPrompt)
            };

            // 获取最近的对话历史
            var history = _dbManager.GetConversationHistory(_currentSessionId, 10);
            foreach (var msg in history)
            {
                messages.Add(new ChatMessage(msg.Role, msg.Content));
            }

            // 添加当前消息(包含图片)
            messages.Add(new ChatMessage("user", currentMessage, images));

            return messages;
        }

        /// <summary>
        /// 检测是否需要工具调用(简化实现)
        /// </summary>
        private async Task<ToolCallRequest> DetectToolCallAsync(string response)
        {
            // TODO: 实现工具调用检测逻辑
            // 这里可以使用正则表达式或者让AI返回结构化的JSON
            // 暂时返回null,表示不需要工具调用
            await Task.CompletedTask;
            return null;
        }

        /// <summary>
        /// 执行工具调用
        /// </summary>
        private async Task<ToolResult> ExecuteToolAsync(ToolCallRequest toolCall)
        {
            if (!_tools.ContainsKey(toolCall.ToolName))
            {
                return ToolResult.CreateError($"未找到工具: {toolCall.ToolName}");
            }

            try
            {
                var tool = _tools[toolCall.ToolName];
                var result = await tool.ExecuteAsync(toolCall.Parameters);

                // 保存工具调用记录
                _dbManager.SaveToolCall(
                    _currentSessionId,
                    toolCall.ToolName,
                    JsonConvert.SerializeObject(toolCall.Parameters),
                    JsonConvert.SerializeObject(result),
                    result.Success ? "success" : "failed");

                return result;
            }
            catch (Exception ex)
            {
                var error = $"工具执行失败: {ex.Message}";
                _dbManager.SaveToolCall(
                    _currentSessionId,
                    toolCall.ToolName,
                    JsonConvert.SerializeObject(toolCall.Parameters),
                    error,
                    "error");
                
                return ToolResult.CreateError(error);
            }
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
            _dbManager.ClearConversationHistory(_currentSessionId);
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
}
