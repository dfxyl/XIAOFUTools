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

        private async Task<ToolCallExecutionResult> ExecuteToolCallAsync(
            string sessionId,
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

            var toolResult = await ExecuteToolAsync(sessionId, toolRequestFromModel, turnId, toolCall.Id);
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

        /// <summary>
        /// 执行工具调用
        /// </summary>
        private async Task<ToolResult> ExecuteToolAsync(string sessionId, ToolCallRequest toolCall, string turnId = null, string callId = null)
        {
            if (!_tools.ContainsKey(toolCall.ToolName))
            {
                var missingToolError = $"未找到工具: {toolCall.ToolName}";
                _dataStore.SaveToolCall(
                    sessionId,
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
                    sessionId,
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
                    sessionId,
                    toolCall.ToolName,
                    JsonConvert.SerializeObject(toolCall.Parameters),
                    error,
                    "error",
                    turnId,
                    callId);
                
                return ToolResult.CreateError(error);
            }
        }
    }
}
