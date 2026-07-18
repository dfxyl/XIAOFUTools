#define DEBUG
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using ArcGIS.Desktop.Framework.Controls;
using ArcGIS.Desktop.Framework.Dialogs;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Agent;
using XIAOFUTools.Features.User.AIAssistant.Application;
using XIAOFUTools.Features.User.AIAssistant.Database;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace XIAOFUTools.Features.User.AIAssistant
{
    public partial class AIAssistantDockPaneView
    {

    private async Task HandleSendMessage(string sessionId, string userMessage, string selectedModel = null, string selectedTool = null, string mode = "chat", List<string> images = null, List<ExecReference> execReferences = null)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = GetApplicationService().CreateNewSession();
            await SendMessageToWebView(new { type = "sessionInfo", sessionId = sessionId, isNewSession = false });
        }

        Debug.WriteLine($"[Backend] HandleSendMessage 被调用, 会话: {sessionId}, 消息: {userMessage}, 模式: {mode}, 工具: {selectedTool}, 图片数量: {images?.Count ?? 0}, 引用数量: {execReferences?.Count ?? 0}");
        try
        {
            string streamId = Guid.NewGuid().ToString("N");
            CancellationTokenSource requestCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = requestCancellation.Token;
            lock (_requestLock)
            {
                _activeRequests[streamId] = new ActiveRequestInfo(sessionId, requestCancellation);
            }

            SendMessageRequest request = new SendMessageRequest
            {
                SessionId = sessionId,
                UserMessage = userMessage,
                SelectedModel = selectedModel,
                SelectedTool = selectedTool,
                Mode = mode,
                Images = images,
                ExecReferences = execReferences?.Select((ExecReference r) => new ExecReferenceDto { Id = r.Id, Output = r.Output, Error = r.Error, Success = r.Success }).ToList()
            };
            await SendMessageToWebView(new { type = "streamStart", sessionId = sessionId, streamId = streamId });
            _ = Task.Run(async delegate
            {
                string currentTurnId = null;
                try
                {
                    await GetApplicationService().SendMessageAsync(request, delegate (string chunk)
                    {
                        SendMessageToWebView(new { type = "streamChunk", sessionId = sessionId, streamId = streamId, turnId = currentTurnId, content = chunk }).GetAwaiter().GetResult();
                    }, delegate (string reasoning)
                    {
                        SendMessageToWebView(new { type = "streamThinking", sessionId = sessionId, streamId = streamId, turnId = currentTurnId, content = reasoning }).GetAwaiter().GetResult();
                    }, delegate (ToolExecutionNoticeDto notice)
                    {
                        SendMessageToWebView(new { type = "toolCall", sessionId = sessionId, streamId = streamId, turnId = (notice.TurnId ?? currentTurnId), callId = notice.CallId, toolName = notice.ToolName, status = notice.Status, message = notice.Message, preview = notice.Preview, parameters = notice.Parameters, result = notice.Result }).GetAwaiter().GetResult();
                    }, (ToolApprovalRequestDto approvalRequest, CancellationToken ct) => RequestToolApprovalAsync(sessionId, streamId, currentTurnId, approvalRequest, ct), delegate (string turnId)
                    {
                        currentTurnId = turnId;
                        SendMessageToWebView(new { type = "assistantTurnStart", sessionId = sessionId, streamId = streamId, turnId = turnId }).GetAwaiter().GetResult();
                    }, delegate (string turnId)
                    {
                        SendMessageToWebView(new { type = "assistantTurnEnd", sessionId = sessionId, streamId = streamId, turnId = turnId }).GetAwaiter().GetResult();
                    }, cancellationToken);
                    await SendMessageToWebView(new { type = "streamEnd", sessionId = sessionId, streamId = streamId });
                }
                catch (OperationCanceledException)
                {
                    Debug.WriteLine("AI请求已被取消");
                    await SendErrorToWebView(sessionId, streamId, "已停止生成");
                    await SendMessageToWebView(new { type = "streamEnd", sessionId = sessionId, streamId = streamId });
                }
                catch (Exception ex3)
                {
                    Debug.WriteLine("AI请求异常: " + ex3.Message);
                    await SendErrorToWebView(sessionId, streamId, "AI请求失败: " + ex3.Message);
                }
                finally
                {
                    lock (_requestLock)
                    {
                        _activeRequests.Remove(streamId);
                    }

                    requestCancellation.Dispose();
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("HandleSendMessage异常: " + ex.Message);
            await SendErrorToWebView("发送失败: " + ex.Message);
        }
    }

    private void HandleModeChanged(string mode)
    {
        try
        {
            if (!string.IsNullOrEmpty(mode))
            {
                GetApplicationService().SetMode(mode);
                Debug.WriteLine("模式已切换: " + mode);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("切换模式失败: " + ex.Message);
        }
    }

    private void HandleStopMessage(string sessionId = null, string streamId = null)
    {
        try
        {
            Debug.WriteLine("收到停止请求, 会话: " + (sessionId ?? "(未指定)") + ", 流: " + (streamId ?? "(未指定)"));
            List<CancellationTokenSource> targets;
            lock (_requestLock)
            {
                targets = ((!string.IsNullOrWhiteSpace(streamId) && _activeRequests.TryGetValue(streamId, out var target)) ? ((string.IsNullOrWhiteSpace(sessionId) || string.Equals(target.SessionId, sessionId, StringComparison.Ordinal)) ? new List<CancellationTokenSource>
                {
                    target.CancellationTokenSource
                }

                : new List<CancellationTokenSource>()) : (string.IsNullOrWhiteSpace(sessionId) ? _activeRequests.Values.Select((ActiveRequestInfo r) => r.CancellationTokenSource).ToList() : (
                    from r in _activeRequests.Values
                    where string.Equals(r.SessionId, sessionId, StringComparison.Ordinal)select r.CancellationTokenSource).ToList()));
            }

            foreach (CancellationTokenSource target2 in targets)
            {
                target2.Cancel();
            }

            CancelPendingToolApprovals("cancel", sessionId, streamId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("停止请求失败: " + ex.Message);
        }
    }

    private async Task SendErrorToWebView(string errorMessage)
    {
        await SendMessageToWebView(new { type = "error", message = errorMessage });
    }

    private async Task SendErrorToWebView(string sessionId, string streamId, string errorMessage)
    {
        await SendMessageToWebView(new { type = "error", sessionId = sessionId, streamId = streamId, message = errorMessage });
    }

    private async Task SendModelsToWebView()
    {
        try
        {
            var models = (
                from s in GetApplicationService().GetModels()select new
                {
                    value = s.Value,
                    label = s.Label,
                    isDefault = s.IsDefault,
                    contextWindowTokens = s.ContextWindowTokens,
                    supportsVision = s.SupportsVision
                }

            ).ToList();
            await SendMessageToWebView(new { type = "modelList", models = models });
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            Debug.WriteLine("发送模型列表失败: " + ex2.Message);
        }
    }

    private async Task SendToolPolicyToWebView()
    {
        try
        {
            await SendMessageToWebView(new { type = "toolPolicy", sensitiveMode = (DatabaseManager.Instance.GetToolExecutionSettings()?.SensitiveMode ?? true) });
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            Debug.WriteLine("发送工具策略失败: " + ex2.Message);
        }
    }
    }
}
