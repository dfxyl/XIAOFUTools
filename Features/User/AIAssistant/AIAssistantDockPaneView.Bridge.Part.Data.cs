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

    private AIAssistantApplicationService GetApplicationService()
    {
        if (_applicationService == null)
        {
            _applicationService = new AIAssistantApplicationService(() => _viewModel?.GetAgent());
        }

        return _applicationService;
    }

    private void OnViewLoaded(object sender, RoutedEventArgs e)
    {
        if (_isWebViewInitialized || _isInitializing)
        {
            Debug.WriteLine("WebView已初始化或正在初始化，跳过");
            return;
        }

        _viewModel = base.DataContext as AIAssistantDockPaneViewModel;
        _applicationService = new AIAssistantApplicationService(() => _viewModel?.GetAgent());
        InitializeWebView();
    }

    private async Task HandleGetHistorySessions()
    {
        try
        {
            var sessionData = (
                from s in GetApplicationService().GetHistorySessions()select new
                {
                    sessionId = s.SessionId,
                    title = s.Title,
                    createdAt = s.CreatedAt,
                    lastActivity = s.LastActivity
                }

            ).ToList();
            await SendMessageToWebView(new { type = "historySessions", sessions = sessionData });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("获取历史会话失败: " + ex.Message);
            await SendErrorToWebView("获取历史会话失败: " + ex.Message);
        }
    }

    private async Task HandleLoadSession(string sessionId)
    {
        try
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            SessionLoadDto sessionData = GetApplicationService().LoadSession(sessionId);
            var execData = sessionData.PythonExecutions.Select((PythonExecutionDto e) => new { id = e.Id, codeId = e.CodeId, code = e.Code, output = e.Output, error = e.Error, success = e.Success, executionTime = e.ExecutionTime, timestamp = e.Timestamp }).ToList();
            var messageData = sessionData.Messages.Select((SessionMessageDto m) => new { role = m.Role, content = m.Content, thinking = m.Thinking, images = m.Images, turnId = m.TurnId, timestamp = m.Timestamp }).ToList();
            var toolCallData = sessionData.ToolCalls.Select((ToolCallHistoryDto t) => new { id = t.Id, turnId = t.TurnId, callId = t.CallId, toolName = t.ToolName, parameters = t.Parameters, result = t.Result, status = t.Status, timestamp = t.Timestamp }).ToList();
            await SendMessageToWebView(new { type = "sessionLoaded", sessionId = sessionData.SessionId, messages = messageData, pythonExecutions = execData, toolCalls = toolCallData });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("加载会话失败: " + ex.Message);
            await SendErrorToWebView("加载会话失败: " + ex.Message);
        }
    }

    private async Task SendMessageToWebView(object message)
    {
        if (!_isWebViewInitialized)
        {
            return;
        }

        try
        {
            string json = JsonConvert.SerializeObject(message);
            string script = "\n(function() {\n    const payload = " + json + ";\n    if (typeof window.__xiaofuHandleHostMessage === 'function') {\n        window.__xiaofuHandleHostMessage(payload);\n        return true;\n    }\n\n    window.dispatchEvent(new MessageEvent('message', { data: payload }));\n    return false;\n})();";
            if (((DispatcherObject)System.Windows.Application.Current).Dispatcher.CheckAccess())
            {
                Debug.WriteLine(string.Concat(str3: await webView.CoreWebView2.ExecuteScriptAsync(script), str0: "[WebViewBridge] 已发送 ", str1: GetMessageType(message), str2: ", direct="));
                return;
            }

            DispatcherOperation<Task<string>> op = ((DispatcherObject)System.Windows.Application.Current).Dispatcher.InvokeAsync<Task<string>>((Func<Task<string>>)(() => webView.CoreWebView2.ExecuteScriptAsync(script)), (DispatcherPriority)9);
            Debug.WriteLine(string.Concat(str3: await (await op.Task), str0: "[WebViewBridge] 已发送 ", str1: GetMessageType(message), str2: ", direct="));
        }
        catch (Exception value)
        {
            Debug.WriteLine($"[WebViewBridge] 发送 {GetMessageType(message)} 到WebView失败: {value}");
        }
    }

    private static string GetMessageType(object message)
    {
        if (message == null)
        {
            return "null";
        }

        return message.GetType().GetProperty("type")?.GetValue(message)?.ToString() ?? message.GetType().Name;
    }
    }
}
