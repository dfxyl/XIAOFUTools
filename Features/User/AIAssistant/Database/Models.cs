using System;
using System.Collections.Generic;
using System.Linq;

#nullable disable

namespace XIAOFUTools.Features.User.AIAssistant.Database
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
        public int ContextWindowTokens { get; set; }
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
        public int Id { get; set; }  // 数据库主键ID
        public string Role { get; set; }
        public string Content { get; set; }
        public string Thinking { get; set; }  // AI的思考过程(仅Agent模式)
        public string Images { get; set; }  // 图片JSON数组
        public string TurnId { get; set; }  // 助手轮次ID（用于精确回放工具调用）
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
        public string TurnId { get; set; }
        public string CallId { get; set; }
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

    /// <summary>
    /// 上下文总结记录（用于长对话压缩）
    /// </summary>
    public class ContextSummary
    {
        public int Id { get; set; }
        public string SessionId { get; set; }
        public string Summary { get; set; }
        public int SummarizedUpToId { get; set; }  // 总结覆盖到的最后一条消息ID
        public int MessageCount { get; set; }       // 被总结的消息数量
        public int EstimatedTokens { get; set; }    // 总结的估算token数
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 联网搜索配置
    /// </summary>
    public class SearchSettings
    {
        /// <summary>
        /// Tavily API Key（推荐，专为AI设计的搜索引擎，免费1000次/月）
        /// 申请地址: https://tavily.com
        /// </summary>
        public string TavilyApiKey { get; set; } = string.Empty;

        /// <summary>
        /// Brave Search API Key（免费2000次/月）
        /// 申请地址: https://brave.com/search/api/
        /// </summary>
        public string BraveApiKey { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用 SearXNG 免费搜索（无需 API Key）
        /// </summary>
        public bool EnableSearXNG { get; set; } = true;

        /// <summary>
        /// 自定义 SearXNG 实例地址（留空使用内置公共实例）
        /// </summary>
        public string SearXNGInstanceUrl { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用 Bing 页面抓取作为兜底（使用 cn.bing.com）
        /// </summary>
        public bool EnableBingScraper { get; set; } = true;
    }

    /// <summary>
    /// 工具元数据
    /// </summary>
    public class ToolDescriptor
    {
        public string ToolName { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string ToolGroup { get; set; } = "其他";
        public bool DefaultChatEnabled { get; set; } = true;
        public bool DefaultAgentEnabled { get; set; } = true;
        public bool RequiresDataAccess { get; set; } = false;
        public bool RequiresSafetyWarning { get; set; } = false;
    }

    /// <summary>
    /// AI助手内置工具目录
    /// </summary>
    public static class AIAssistantToolCatalog
    {
        public static readonly IReadOnlyList<ToolDescriptor> Tools = new List<ToolDescriptor>
        {
            new ToolDescriptor
            {
                ToolName = "web_fetch",
                DisplayName = "网页抓取",
                Description = "抓取网页正文并支持分段读取",
                ToolGroup = "基础与联网",
                DefaultChatEnabled = true,
                DefaultAgentEnabled = true
            },
            new ToolDescriptor
            {
                ToolName = "project_snapshot",
                DisplayName = "工程快照",
                Description = "读取当前工程、地图、范围和图层统计",
                ToolGroup = "工程与图层只读",
                DefaultChatEnabled = false,
                DefaultAgentEnabled = false
            },
            new ToolDescriptor
            {
                ToolName = "list_map_layers",
                DisplayName = "图层列表",
                Description = "按条件列出当前地图图层",
                ToolGroup = "工程与图层只读",
                DefaultChatEnabled = false,
                DefaultAgentEnabled = false
            },
            new ToolDescriptor
            {
                ToolName = "describe_layer_schema",
                DisplayName = "图层结构",
                Description = "读取字段、几何类型与OID信息",
                ToolGroup = "工程与图层只读",
                DefaultChatEnabled = false,
                DefaultAgentEnabled = false
            },
            new ToolDescriptor
            {
                ToolName = "selection_summary",
                DisplayName = "选择集摘要",
                Description = "统计当前选择集并返回样本",
                ToolGroup = "工程与图层只读",
                DefaultChatEnabled = false,
                DefaultAgentEnabled = false
            },
            new ToolDescriptor
            {
                ToolName = "layer_query",
                DisplayName = "记录查询",
                Description = "按条件读取图层记录样本（默认关闭）",
                ToolGroup = "数据读取与画像",
                DefaultChatEnabled = false,
                DefaultAgentEnabled = false,
                RequiresDataAccess = true
            },
            new ToolDescriptor
            {
                ToolName = "field_profile",
                DisplayName = "字段画像",
                Description = "统计空值率与分布样本（默认关闭）",
                ToolGroup = "数据读取与画像",
                DefaultChatEnabled = false,
                DefaultAgentEnabled = false,
                RequiresDataAccess = true
            },
            new ToolDescriptor
            {
                ToolName = "overlay_intersect_summary",
                DisplayName = "叠加汇总",
                Description = "按两个面图层求交并汇总面积（只读，默认关闭）",
                ToolGroup = "空间分析工具",
                DefaultChatEnabled = false,
                DefaultAgentEnabled = false,
                RequiresDataAccess = true
            },
            new ToolDescriptor
            {
                ToolName = "buffer_analysis",
                DisplayName = "缓冲区分析",
                Description = "创建缓冲区输出要素类（仅新增输出，不修改输入，默认关闭）",
                ToolGroup = "空间分析工具",
                DefaultChatEnabled = false,
                DefaultAgentEnabled = false,
                RequiresDataAccess = true,
                RequiresSafetyWarning = true
            },
            new ToolDescriptor
            {
                ToolName = "clip_analysis",
                DisplayName = "裁剪分析",
                Description = "按裁剪图层生成新要素类（仅新增输出，不修改输入，默认关闭）",
                ToolGroup = "空间分析工具",
                DefaultChatEnabled = false,
                DefaultAgentEnabled = false,
                RequiresDataAccess = true,
                RequiresSafetyWarning = true
            },
            new ToolDescriptor
            {
                ToolName = "create_python_toolbox",
                DisplayName = "生成 Python 工具箱",
                Description = "在当前工程目录生成 .pyt 工具箱并添加到工程",
                ToolGroup = "工程工具生成",
                DefaultChatEnabled = false,
                DefaultAgentEnabled = true,
                RequiresDataAccess = false,
                RequiresSafetyWarning = true
            }
        };

        public static ToolDescriptor Find(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return null;
            }

            return Tools.FirstOrDefault(t => string.Equals(t.ToolName, toolName, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// 工具模式开关
    /// </summary>
    public class ToolModeSetting
    {
        public string ToolName { get; set; }
        public bool ChatEnabled { get; set; }
        public bool AgentEnabled { get; set; }
    }

    /// <summary>
    /// 工具执行设置
    /// </summary>
    public class ToolExecutionSettings
    {
        /// <summary>
         /// 非 Agent 模式下是否在执行工具前提示
         /// false: 自动运行, true: 弹窗确认
         /// </summary>
        public bool PromptBeforeToolInChat { get; set; } = true;

        /// <summary>
        /// 敏感模式：工具明细结果不回传到外部模型，仅在本地工具块展示。
        /// </summary>
        public bool SensitiveMode { get; set; } = true;

        public Dictionary<string, ToolModeSetting> ToolModes { get; set; } =
            new Dictionary<string, ToolModeSetting>(StringComparer.OrdinalIgnoreCase);

        public void EnsureDefaults()
        {
            if (ToolModes == null)
            {
                ToolModes = new Dictionary<string, ToolModeSetting>(StringComparer.OrdinalIgnoreCase);
            }

            foreach (var descriptor in AIAssistantToolCatalog.Tools)
            {
                if (!ToolModes.TryGetValue(descriptor.ToolName, out var mode))
                {
                    ToolModes[descriptor.ToolName] = new ToolModeSetting
                    {
                        ToolName = descriptor.ToolName,
                        ChatEnabled = descriptor.DefaultChatEnabled,
                        AgentEnabled = descriptor.DefaultAgentEnabled
                    };
                    continue;
                }

                mode.ToolName = descriptor.ToolName;
            }
        }

        public bool IsToolEnabled(string toolName, string mode)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return false;
            }

            EnsureDefaults();

            if (!ToolModes.TryGetValue(toolName, out var toolMode))
            {
                return true;
            }

            if (string.Equals(mode, "agent", StringComparison.OrdinalIgnoreCase))
            {
                return toolMode.AgentEnabled;
            }

            return toolMode.ChatEnabled;
        }

        public void SetToolMode(string toolName, bool chatEnabled, bool agentEnabled)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return;
            }

            ToolModes[toolName] = new ToolModeSetting
            {
                ToolName = toolName,
                ChatEnabled = chatEnabled,
                AgentEnabled = agentEnabled
            };
        }
    }
}
