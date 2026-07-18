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

    private async Task HandleRunPython(string sessionId, string code, string codeId)
    {
        Debug.WriteLine("[HandleRunPython] 开始执行Python代码, codeId=" + codeId);
        try
        {
            await SendMessageToWebView(new { type = "pythonRunning", sessionId = sessionId, codeId = codeId });
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                sessionId = _viewModel?.GetAgent()?.GetCurrentSessionId();
            }

            PythonRunResultDto result = await GetApplicationService().RunPythonAsync(code, codeId, sessionId);
            Debug.WriteLine($"[HandleRunPython] 执行完成, success={result.Success}, exitCode={result.ExitCode}");
            await SendMessageToWebView(new { type = "pythonResult", sessionId = sessionId, codeId = result.CodeId, executionId = result.ExecutionId, success = result.Success, output = result.Output, error = result.Error, exitCode = result.ExitCode, executionTime = result.ExecutionTime, code = result.Code });
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[HandleRunPython] 执行异常: " + ex.Message);
            await SendMessageToWebView(new { type = "pythonResult", sessionId = sessionId, codeId = codeId, success = false, error = "执行出错: " + ex.Message, executionTime = 0 });
        }
    }

    private void HandleOpenSettings()
    {
        try
        {
            System.Windows.Application current = System.Windows.Application.Current;
            if (current == null)
            {
                return;
            }

            ((DispatcherObject)current).Dispatcher.BeginInvoke((Delegate)(Action)async delegate
            {
                try
                {
                    SettingsWindow settingsWindow = new SettingsWindow();
                    ((ProWindow)settingsWindow).ShowDialog();
                    await RefreshModelsAfterSettings();
                }
                catch (Exception ex2)
                {
                    Exception ex3 = ex2;
                    Debug.WriteLine("[HandleOpenSettings] 显示窗口失败: " + ex3.Message);
                }
            }, Array.Empty<object>());
        }
        catch (Exception ex)
        {
            Debug.WriteLine("[HandleOpenSettings] 打开设置窗口失败: " + ex.Message);
        }
    }

    private static bool TryParseJsonToken(JToken token, out JToken parsed)
    {
        parsed = null;
        if (token == null || token.Type == JTokenType.Null)
        {
            return false;
        }

        if (token.Type == JTokenType.Object || token.Type == JTokenType.Array)
        {
            parsed = token;
            return true;
        }

        string text = token.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        if ((!text.StartsWith("{") || !text.EndsWith("}")) && (!text.StartsWith("[") || !text.EndsWith("]")))
        {
            return false;
        }

        try
        {
            parsed = JToken.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task RefreshModelsAfterSettings()
    {
        try
        {
            await SendModelsToWebView();
            await SendToolPolicyToWebView();
            GetApplicationService().ReloadAiService();
            Debug.WriteLine("[RefreshModelsAfterSettings] 模型列表和AI服务已刷新");
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            Debug.WriteLine("[RefreshModelsAfterSettings] 刷新失败: " + ex2.Message);
        }
    }
    }
}
