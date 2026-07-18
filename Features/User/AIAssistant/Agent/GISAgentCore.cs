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
using XIAOFUTools.Features.User.AIAssistant.Agent.Context;
using XIAOFUTools.Features.User.AIAssistant.Agent.Prompts;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tools;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tooling;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Features.User.AIAssistant.Agent
{
    /// <summary>
    /// GIS Agent核心 - 协调AI服务和GIS工具调用
    /// </summary>
    public partial class GISAgentCore
    {
        private IAgentDataStore _dataStore;
        private readonly Dictionary<string, IGISTool> _tools;
        private IAIService _aiService;
        private string _currentSessionId;
        private readonly string _chatSystemPrompt;
        private readonly string _agentSystemPrompt;
        private string _currentMode = "chat"; // "chat" or "agent"
        private string _initError; // 初始化错误信息（用于延迟提示）
        private const string WebFetchToolName = "web_fetch";
        private const int SingleToolCallAlertThreshold = 30;
        private const int SingleToolCallHardLimit = 50;
        private const int SameParametersHardLimit = 20;
        private const string SensitiveModeToolResultPlaceholder = "敏感模式已开启：工具已在本地执行，详细结果不回传模型。";
        private int _currentContextWindowTokens = AgentContextBudgetPolicy.DefaultContextWindowTokens;

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

        // ============ 上下文管理 ============
        /// <summary>总结后保留的最近消息条数</summary>
        private const int KEEP_RECENT_MESSAGES = 4;
        /// <summary>正在执行总结的会话</summary>
        private readonly HashSet<string> _summarizingSessions = new HashSet<string>();
        private readonly object _summarizingLock = new object();
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
