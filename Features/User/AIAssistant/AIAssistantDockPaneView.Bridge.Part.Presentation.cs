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

    private async void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        try
        {
            Debug.WriteLine($"[OnNavigationCompleted] 页面加载完成, 是否首次: {_isFirstNavigation}");
            loadingIndicator.Visibility = Visibility.Collapsed;
            if (_isFirstNavigation)
            {
                _isFirstNavigation = false;
                await SendModelsToWebView();
                await SendToolPolicyToWebView();
                GISAgentCore agent = _viewModel?.GetAgent();
                if (agent != null)
                {
                    await SendMessageToWebView(new { type = "sessionInfo", sessionId = agent.GetCurrentSessionId(), isNewSession = true });
                }
            }
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            Debug.WriteLine("OnNavigationCompleted异常: " + ex2.Message);
        }
    }

    private async void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            string messageJson = e.WebMessageAsJson;
            JObject message = JObject.Parse(messageJson);
            string type = message["type"]?.ToString();
            switch (type)
            {
                case "sendMessage":
                {
                    string userMessage = message["message"]?.ToString();
                    string selectedModel = message["model"]?.ToString();
                    string selectedTool = message["tool"]?.ToString();
                    string mode = message["mode"]?.ToString() ?? "chat";
                    List<string> images = message["images"]?.ToObject<List<string>>();
                    List<ExecReference> execReferences = message["execReferences"]?.ToObject<List<ExecReference>>();
                    string requestSessionId = message["sessionId"]?.ToString();
                    await HandleSendMessage(requestSessionId, userMessage, selectedModel, selectedTool, mode, images, execReferences);
                    break;
                }

                case "newSession":
                    HandleNewSession();
                    break;
                case "getHistorySessions":
                    await HandleGetHistorySessions();
                    break;
                case "loadSession":
                {
                    string sessionId = message["sessionId"]?.ToString();
                    await HandleLoadSession(sessionId);
                    break;
                }

                case "deleteSession":
                {
                    string deleteSessionId = message["sessionId"]?.ToString();
                    await HandleDeleteSession(deleteSessionId);
                    break;
                }

                case "modeChanged":
                {
                    string newMode = message["mode"]?.ToString();
                    HandleModeChanged(newMode);
                    break;
                }

                case "stopMessage":
                {
                    string stopStreamId = message["streamId"]?.ToString();
                    string stopSessionId = message["sessionId"]?.ToString();
                    HandleStopMessage(stopSessionId, stopStreamId);
                    break;
                }

                case "runPython":
                {
                    string pythonCode = message["code"]?.ToString();
                    string codeId = message["codeId"]?.ToString();
                    string pythonSessionId = message["sessionId"]?.ToString();
                    await HandleRunPython(pythonSessionId, pythonCode, codeId);
                    break;
                }

                case "openSettings":
                    HandleOpenSettings();
                    break;
                case "toolApprovalResponse":
                {
                    string requestId = message["requestId"]?.ToString();
                    string decision = message["decision"]?.ToString();
                    HandleToolApprovalResponse(requestId, decision);
                    break;
                }

                case "exportToolCall":
                {
                    JObject payload = message["payload"] as JObject;
                    await HandleExportToolCall(payload);
                    break;
                }

                default:
                    Debug.WriteLine("未知消息类型: " + type);
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("处理WebView消息失败: " + ex.Message);
            await SendErrorToWebView("处理消息失败: " + ex.Message);
        }
    }

    private void HandleNewSession()
    {
        try
        {
            string sessionId = GetApplicationService().CreateNewSession();
            _ = SendMessageToWebView(new { type = "sessionInfo", sessionId = sessionId, isNewSession = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("创建新会话失败: " + ex.Message);
        }
    }

    private async Task HandleDeleteSession(string sessionId)
    {
        try
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            GetApplicationService().DeleteSession(sessionId);
            CancelActiveRequestsForSession(sessionId);
            Debug.WriteLine("会话已删除: " + sessionId);
            await SendMessageToWebView(new { type = "sessionDeleted", deletedSessionId = sessionId });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("删除会话失败: " + ex.Message);
            await SendErrorToWebView("删除会话失败: " + ex.Message);
        }
    }

    private void CancelActiveRequestsForSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return;
        }

        List<CancellationTokenSource> targets;
        lock (_requestLock)
        {
            targets = (
                from r in _activeRequests.Values
                where string.Equals(r.SessionId, sessionId, StringComparison.Ordinal)select r.CancellationTokenSource).ToList();
        }

        foreach (CancellationTokenSource target in targets)
        {
            target.Cancel();
        }

        CancelPendingToolApprovals("cancel", sessionId);
    }

    private void HandleToolApprovalResponse(string requestId, string decision)
    {
        if (string.IsNullOrWhiteSpace(requestId))
        {
            return;
        }

        PendingToolApprovalInfo waiter = null;
        lock (_toolApprovalLock)
        {
            if (_pendingToolApprovals.TryGetValue(requestId, out var pending))
            {
                waiter = pending;
            }
        }

        waiter?.Completion.TrySetResult(string.IsNullOrWhiteSpace(decision) ? "cancel" : decision);
    }

    private void CancelPendingToolApprovals(string decision, string sessionId = null, string streamId = null)
    {
        List<PendingToolApprovalInfo> pending;
        lock (_toolApprovalLock)
        {
            List<string> matchedKeys = (
                from kvp in _pendingToolApprovals
                where (string.IsNullOrWhiteSpace(sessionId) || string.Equals(kvp.Value.SessionId, sessionId, StringComparison.Ordinal)) && (string.IsNullOrWhiteSpace(streamId) || string.Equals(kvp.Value.StreamId, streamId, StringComparison.Ordinal))select kvp.Key).ToList();
            pending = matchedKeys.Select((string key2) => _pendingToolApprovals[key2]).ToList();
            foreach (string key in matchedKeys)
            {
                _pendingToolApprovals.Remove(key);
            }
        }

        foreach (PendingToolApprovalInfo waiter in pending)
        {
            waiter.Completion.TrySetResult(decision);
        }
    }
    }
}
