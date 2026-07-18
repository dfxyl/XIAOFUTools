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
    }
}
