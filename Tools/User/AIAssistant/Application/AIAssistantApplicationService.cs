using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Tools.User.AIAssistant.Agent;
using XIAOFUTools.Tools.User.AIAssistant.Database;
using XIAOFUTools.Tools.User.AIAssistant.Services;

namespace XIAOFUTools.Tools.User.AIAssistant.Application
{
    /// <summary>
    /// AI助手应用服务，封装会话、消息与执行编排。
    /// </summary>
    internal sealed class AIAssistantApplicationService
    {
        private readonly Func<GISAgentCore> _agentProvider;

        public AIAssistantApplicationService(Func<GISAgentCore> agentProvider)
        {
            _agentProvider = agentProvider ?? throw new ArgumentNullException(nameof(agentProvider));
        }

        public string CreateNewSession()
        {
            var agent = GetAgent();
            agent.CreateNewSession();
            return agent.GetCurrentSessionId();
        }

        public void SetMode(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                return;
            }

            GetAgent().SetMode(mode);
        }

        public List<HistorySessionDto> GetHistorySessions(int limit = 20)
        {
            var sessions = DatabaseManager.Instance.GetRecentSessions(limit);
            return sessions.Select(s => new HistorySessionDto
            {
                SessionId = s.SessionId,
                Title = s.Title,
                CreatedAt = s.CreatedAt,
                LastActivity = s.LastActivity
            }).ToList();
        }

        public SessionLoadDto LoadSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException("会话ID不能为空", nameof(sessionId));
            }

            var agent = GetAgent();
            var dbManager = DatabaseManager.Instance;

            var messages = dbManager.GetConversationHistory(sessionId, 0)
                .Select(m => new SessionMessageDto
                {
                    Role = m.Role,
                    Content = m.Content,
                    Thinking = m.Thinking,
                    Images = m.Images,
                    TurnId = m.TurnId,
                    Timestamp = m.Timestamp
                }).ToList();

            var executions = dbManager.GetPythonExecutions(sessionId)
                .Select(e => new PythonExecutionDto
                {
                    Id = e.Id,
                    CodeId = e.CodeId,
                    Code = e.Code,
                    Output = e.Output,
                    Error = e.Error,
                    Success = e.Success,
                    ExecutionTime = e.ExecutionTimeMs,
                    Timestamp = e.Timestamp
                }).ToList();

            var toolCalls = dbManager.GetToolCallHistory(sessionId, 0)
                .Select(t => new ToolCallHistoryDto
                {
                    Id = t.Id,
                    TurnId = t.TurnId,
                    CallId = t.CallId,
                    ToolName = t.ToolName,
                    Parameters = t.Parameters,
                    Result = t.Result,
                    Status = t.Status,
                    Timestamp = t.Timestamp
                }).ToList();

            agent.SwitchSession(sessionId);

            return new SessionLoadDto
            {
                SessionId = sessionId,
                Messages = messages,
                PythonExecutions = executions,
                ToolCalls = toolCalls
            };
        }

        public void DeleteSession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return;
            }

            DatabaseManager.Instance.DeleteSession(sessionId);
        }

        public List<ModelOptionDto> GetModels()
        {
            var services = DatabaseManager.Instance.GetAllServices();
            return services.Select(s => new ModelOptionDto
            {
                Value = s.ModelName,
                Label = s.Name,
                IsDefault = s.IsDefault,
                SupportsVision = s.SupportsVision
            }).ToList();
        }

        public void ReloadAiService()
        {
            GetAgent().ReloadAIService();
        }

        public async Task SendMessageAsync(
            SendMessageRequest request,
            Action<string> onChunk,
            Action<string> onReasoning,
            Action<ToolExecutionNoticeDto> onToolExecution,
            Func<ToolApprovalRequestDto, CancellationToken, Task<ToolApprovalDecisionDto>> onToolApproval,
            Action<string> onAssistantTurnStarted,
            Action<string> onAssistantTurnEnded,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.UserMessage))
            {
                throw new ArgumentException("消息不能为空", nameof(request.UserMessage));
            }

            var agent = GetAgent();
            agent.SetMode(string.IsNullOrWhiteSpace(request.Mode) ? "chat" : request.Mode);

            if (!string.IsNullOrWhiteSpace(request.SelectedModel))
            {
                agent.SwitchModel(request.SelectedModel);
            }

            var finalMessage = BuildMessageWithExecReferences(request.UserMessage, request.ExecReferences);

            await agent.SendMessageAsync(
                finalMessage,
                onChunk,
                cancellationToken,
                onReasoning,
                notice => onToolExecution?.Invoke(new ToolExecutionNoticeDto
                {
                    CallId = notice.CallId,
                    TurnId = notice.TurnId,
                    ToolName = notice.ToolName,
                    Status = notice.Status,
                    Message = notice.Message,
                    Preview = notice.Preview,
                    Parameters = notice.Parameters,
                    Result = notice.Result
                }),
                async (approvalRequest, ct) =>
                {
                    if (onToolApproval == null)
                    {
                        return new ToolApprovalDecision { Decision = "allow" };
                    }

                    var decisionDto = await onToolApproval(new ToolApprovalRequestDto
                    {
                        ToolNames = approvalRequest.ToolNames,
                        CallCount = approvalRequest.CallCount,
                        Mode = approvalRequest.Mode,
                        Message = approvalRequest.Message
                    }, ct);

                    return new ToolApprovalDecision
                    {
                        Decision = decisionDto?.Decision
                    };
                },
                onAssistantTurnStarted,
                onAssistantTurnEnded,
                request.Images,
                request.SelectedTool);
        }

        public async Task<PythonRunResultDto> RunPythonAsync(
            string code,
            string codeId,
            string sessionId)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return new PythonRunResultDto
                {
                    CodeId = codeId,
                    Success = false,
                    Error = "代码不能为空",
                    ExecutionTime = 0
                };
            }

            var pythonService = PythonExecutionService.Instance;
            if (!pythonService.IsPythonAvailable)
            {
                return new PythonRunResultDto
                {
                    CodeId = codeId,
                    Success = false,
                    Error = "Python环境不可用。请确保ArcGIS Pro已正确安装。",
                    ExecutionTime = 0
                };
            }

            var result = await pythonService.ExecuteCodeAsync(code);
            var executionId = -1;
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                executionId = DatabaseManager.Instance.SavePythonExecution(
                    sessionId,
                    codeId,
                    code,
                    result.Output,
                    result.Error,
                    result.Success,
                    result.ExecutionTimeMs);
            }

            return new PythonRunResultDto
            {
                CodeId = codeId,
                ExecutionId = executionId,
                Success = result.Success,
                Output = result.Output,
                Error = result.Error,
                ExitCode = result.ExitCode,
                ExecutionTime = result.ExecutionTimeMs,
                Code = code
            };
        }

        private GISAgentCore GetAgent()
        {
            var agent = _agentProvider();
            if (agent == null)
            {
                throw new InvalidOperationException("AI Agent未初始化");
            }

            return agent;
        }

        private static string BuildMessageWithExecReferences(string originalMessage, List<ExecReferenceDto> execReferences)
        {
            if (execReferences == null || execReferences.Count == 0)
            {
                return originalMessage;
            }

            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("\n---\n**引用的Python执行结果（仅结果/报错）:**");

            foreach (var exec in execReferences)
            {
                contextBuilder.AppendLine($"\n[执行#{exec.Id}] {(exec.Success ? "✅成功" : "❌失败")}");

                if (!string.IsNullOrWhiteSpace(exec.Output))
                {
                    var output = exec.Output.Length > 3000
                        ? exec.Output.Substring(0, 3000) + "\n...（输出已截断）"
                        : exec.Output;
                    contextBuilder.AppendLine($"输出:\n```\n{output}\n```");
                }

                if (!string.IsNullOrWhiteSpace(exec.Error))
                {
                    var error = exec.Error.Length > 3000
                        ? exec.Error.Substring(0, 3000) + "\n...（错误已截断）"
                        : exec.Error;
                    contextBuilder.AppendLine($"错误:\n```\n{error}\n```");
                }

                if (string.IsNullOrWhiteSpace(exec.Output) && string.IsNullOrWhiteSpace(exec.Error))
                {
                    contextBuilder.AppendLine("结果: 无输出");
                }
            }

            contextBuilder.AppendLine("---\n");
            return originalMessage + contextBuilder;
        }
    }

    internal sealed class SendMessageRequest
    {
        public string UserMessage { get; set; }
        public string SelectedModel { get; set; }
        public string SelectedTool { get; set; }
        public string Mode { get; set; }
        public List<string> Images { get; set; }
        public List<ExecReferenceDto> ExecReferences { get; set; }
    }

    internal sealed class ExecReferenceDto
    {
        public string Id { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool Success { get; set; }
    }

    internal sealed class HistorySessionDto
    {
        public string SessionId { get; set; }
        public string Title { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivity { get; set; }
    }

    internal sealed class SessionLoadDto
    {
        public string SessionId { get; set; }
        public List<SessionMessageDto> Messages { get; set; } = new List<SessionMessageDto>();
        public List<PythonExecutionDto> PythonExecutions { get; set; } = new List<PythonExecutionDto>();
        public List<ToolCallHistoryDto> ToolCalls { get; set; } = new List<ToolCallHistoryDto>();
    }

    internal sealed class SessionMessageDto
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public string Thinking { get; set; }
        public string Images { get; set; }
        public string TurnId { get; set; }
        public DateTime Timestamp { get; set; }
    }

    internal sealed class PythonExecutionDto
    {
        public int Id { get; set; }
        public string CodeId { get; set; }
        public string Code { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool Success { get; set; }
        public int ExecutionTime { get; set; }
        public DateTime Timestamp { get; set; }
    }

    internal sealed class ToolCallHistoryDto
    {
        public int Id { get; set; }
        public string TurnId { get; set; }
        public string CallId { get; set; }
        public string ToolName { get; set; }
        public string Parameters { get; set; }
        public string Result { get; set; }
        public string Status { get; set; }
        public DateTime Timestamp { get; set; }
    }

    internal sealed class ModelOptionDto
    {
        public string Value { get; set; }
        public string Label { get; set; }
        public bool IsDefault { get; set; }
        public bool SupportsVision { get; set; }
    }

    internal sealed class PythonRunResultDto
    {
        public string CodeId { get; set; }
        public int ExecutionId { get; set; }
        public bool Success { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public int ExitCode { get; set; }
        public long ExecutionTime { get; set; }
        public string Code { get; set; }
    }

    internal sealed class ToolExecutionNoticeDto
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

    internal sealed class ToolApprovalRequestDto
    {
        public List<string> ToolNames { get; set; } = new List<string>();
        public int CallCount { get; set; }
        public string Mode { get; set; }
        public string Message { get; set; }
    }

    internal sealed class ToolApprovalDecisionDto
    {
        public string Decision { get; set; }
    }
}
