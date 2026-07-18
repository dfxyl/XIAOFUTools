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

    private async void InitializeWebView()
    {
        if (_isWebViewInitialized || _isInitializing)
        {
            Debug.WriteLine("WebView已初始化或正在初始化，跳过");
            return;
        }

        _isInitializing = true;
        Debug.WriteLine("[InitializeWebView] 开始初始化...");
        try
        {
            if (_viewModel == null)
            {
                throw new InvalidOperationException("ViewModel未初始化，无法加载AI助手界面");
            }

            string webView2Version = null;
            try
            {
                webView2Version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch (Exception)
            {
            }

            if (string.IsNullOrEmpty(webView2Version))
            {
                _isInitializing = false;
                loadingIndicator.Visibility = Visibility.Collapsed;
                MessageBox.Show("AI助手需要 Microsoft Edge WebView2 Runtime 才能运行。\n\n请从以下地址下载安装：\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/\n\n安装完成后请重启ArcGIS Pro。", "缺少WebView2运行时", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            Debug.WriteLine("WebView2 Runtime版本: " + webView2Version);
            string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XIAOFUTools", "WebView2");
            try
            {
                if (!Directory.Exists(userDataFolder))
                {
                    Directory.CreateDirectory(userDataFolder);
                }
            }
            catch (Exception)
            {
                userDataFolder = Path.Combine(Path.GetTempPath(), "XIAOFUTools", "WebView2");
                Directory.CreateDirectory(userDataFolder);
                Debug.WriteLine("WebView2用户数据降级到临时目录: " + userDataFolder);
            }

            CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await webView.EnsureCoreWebView2Async(env);
            webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
            webView.CoreWebView2.Settings.IsScriptEnabled = true;
            webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            Debug.WriteLine("WebView初始化完成");
            await CheckAIServiceStatus();
            string htmlPath = _viewModel.GetHtmlPath();
            webView.CoreWebView2.Navigate("file:///" + htmlPath.Replace("\\", "/"));
            _isWebViewInitialized = true;
            _isInitializing = false;
            Debug.WriteLine("[InitializeWebView] 初始化完成");
        }
        catch (Exception ex3)
        {
            _isInitializing = false;
            Debug.WriteLine("WebView2初始化失败: " + ex3.Message);
            Debug.WriteLine("堆栈: " + ex3.StackTrace);
            string errorMsg = ((ex3.Message.Contains("0x80004005") || ex3.Message.Contains("access")) ? "WebView2初始化失败：权限不足。\n请尝试以管理员身份运行ArcGIS Pro。" : ((!(ex3 is FileNotFoundException)) ? ("AI助手界面加载失败: " + ex3.Message + "\n\n可能的解决方案：\n1. 确保已安装 WebView2 Runtime\n2. 尝试以管理员身份运行\n3. 检查杀毒软件是否拦截") : ("AI助手界面文件未找到。\n请确保插件文件完整。\n\n详细信息: " + ex3.Message)));
            MessageBox.Show(errorMsg, "AI助手初始化失败", MessageBoxButton.OK, MessageBoxImage.Hand);
            loadingIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private async Task CheckAIServiceStatus()
    {
        try
        {
            if (_viewModel == null)
            {
                Debug.WriteLine("ViewModel未初始化，等待Loaded事件");
                return;
            }

            GISAgentCore agent = _viewModel.GetAgent();
            if (agent == null)
            {
                await SendErrorToWebView("⚠\ufe0f AI服务初始化失败，请检查数据库配置。请查看调试输出以获取详细信息。");
                Debug.WriteLine("AI Agent为null，请检查GISAgentCore初始化日志");
            }
            else
            {
                Debug.WriteLine("AI服务状态正常");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("检查AI服务状态失败: " + ex.Message);
            await SendErrorToWebView("检查AI服务状态失败: " + ex.Message);
        }
    }

    private async Task<ToolApprovalDecisionDto> RequestToolApprovalAsync(string sessionId, string streamId, string turnId, ToolApprovalRequestDto approvalRequest, CancellationToken cancellationToken)
    {
        if (approvalRequest == null)
        {
            return new ToolApprovalDecisionDto
            {
                Decision = "cancel"
            };
        }

        string requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<string> tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_toolApprovalLock)
        {
            _pendingToolApprovals[requestId] = new PendingToolApprovalInfo(sessionId, streamId, tcs);
        }

        try
        {
            await SendMessageToWebView(new { type = "toolApprovalRequest", sessionId = sessionId, streamId = streamId, turnId = turnId, requestId = requestId, toolNames = approvalRequest.ToolNames, callCount = approvalRequest.CallCount, mode = approvalRequest.Mode, message = approvalRequest.Message });
            using (cancellationToken.Register(delegate
            {
                tcs.TrySetCanceled();
            }))
            {
                string decision = await tcs.Task;
                return new ToolApprovalDecisionDto
                {
                    Decision = (string.IsNullOrWhiteSpace(decision) ? "cancel" : decision)
                };
            }
        }
        catch
        {
            return new ToolApprovalDecisionDto
            {
                Decision = "cancel"
            };
        }
        finally
        {
            lock (_toolApprovalLock)
            {
                _pendingToolApprovals.Remove(requestId);
            }
        }
    }

    private static List<string> CollectTableHeaders(JArray rows)
    {
        List<string> headers = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JObject row in rows.OfType<JObject>())
        {
            foreach (JProperty property in row.Properties())
            {
                if (seen.Add(property.Name))
                {
                    headers.Add(property.Name);
                }
            }
        }

        return headers;
    }

    private static void FlattenToken(JToken token, string path, List<(string Key, string Value)> rows)
    {
        if (token == null)
        {
            return;
        }

        switch (token.Type)
        {
            case JTokenType.Object:
            {
                JObject obj = (JObject)token;
                if (!obj.Properties().Any())
                {
                    rows.Add((string.IsNullOrWhiteSpace(path) ? "(空对象)" : path, "{}"));
                    break;
                }

                {
                    foreach (JProperty property in obj.Properties())
                    {
                        string nextPath2 = (string.IsNullOrWhiteSpace(path) ? property.Name : (path + "." + property.Name));
                        FlattenToken(property.Value, nextPath2, rows);
                    }

                    break;
                }
            }

            case JTokenType.Array:
            {
                JArray array = (JArray)token;
                if (array.Count == 0)
                {
                    rows.Add((string.IsNullOrWhiteSpace(path) ? "(空数组)" : path, "[]"));
                    break;
                }

                for (int i = 0; i < array.Count; i++)
                {
                    string nextPath = (string.IsNullOrWhiteSpace(path) ? $"[{i}]" : $"{path}[{i}]");
                    FlattenToken(array[i], nextPath, rows);
                }

                break;
            }

            default:
                rows.Add((string.IsNullOrWhiteSpace(path) ? "value" : path, token.ToString()));
                break;
        }
    }
    }
}
