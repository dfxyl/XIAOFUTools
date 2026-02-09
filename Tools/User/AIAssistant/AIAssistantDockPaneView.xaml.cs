using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Tools.User.AIAssistant.Application;
using XIAOFUTools.Tools.User.AIAssistant.Database;

namespace XIAOFUTools.Tools.User.AIAssistant
{
    /// <summary>
    /// AI助手停靠窗格视图
    /// </summary>
    public partial class AIAssistantDockPaneView : UserControl
    {
        private AIAssistantDockPaneViewModel _viewModel;
        private bool _isWebViewInitialized = false;
        private bool _isInitializing = false;
        private bool _isFirstNavigation = true;
        private System.Threading.CancellationTokenSource _currentRequestCancellation;
        private AIAssistantApplicationService _applicationService;
        private readonly object _toolApprovalLock = new object();
        private readonly Dictionary<string, TaskCompletionSource<string>> _pendingToolApprovals = new Dictionary<string, TaskCompletionSource<string>>();

        public AIAssistantDockPaneView()
        {
            InitializeComponent();
            
            // 延迟初始化，等待DataContext绑定
            this.Loaded += OnViewLoaded;
        }

        private AIAssistantApplicationService GetApplicationService()
        {
            if (_applicationService == null)
            {
                _applicationService = new AIAssistantApplicationService(() => _viewModel?.GetAgent());
            }

            return _applicationService;
        }

        private void OnViewLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 防止重复初始化
            if (_isWebViewInitialized || _isInitializing)
            {
                System.Diagnostics.Debug.WriteLine("WebView已初始化或正在初始化，跳过");
                return;
            }
            
            // 获取ViewModel
            _viewModel = this.DataContext as AIAssistantDockPaneViewModel;
            _applicationService = new AIAssistantApplicationService(() => _viewModel?.GetAgent());
            
            // 初始化WebView2
            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            // 防止重复初始化
            if (_isWebViewInitialized || _isInitializing)
            {
                System.Diagnostics.Debug.WriteLine("WebView已初始化或正在初始化，跳过");
                return;
            }
            
            _isInitializing = true;
            System.Diagnostics.Debug.WriteLine("[InitializeWebView] 开始初始化...");
            
            try
            {
                // 检查ViewModel是否存在
                if (_viewModel == null)
                {
                    throw new InvalidOperationException("ViewModel未初始化，无法加载AI助手界面");
                }

                // 先检查WebView2 Runtime是否可用
                string webView2Version = null;
                try
                {
                    webView2Version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                }
                catch (Exception)
                {
                    // WebView2 Runtime未安装
                }

                if (string.IsNullOrEmpty(webView2Version))
                {
                    _isInitializing = false;
                    loadingIndicator.Visibility = System.Windows.Visibility.Collapsed;
                    
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        "AI助手需要 Microsoft Edge WebView2 Runtime 才能运行。\n\n" +
                        "请从以下地址下载安装：\n" +
                        "https://developer.microsoft.com/en-us/microsoft-edge/webview2/\n\n" +
                        "安装完成后请重启ArcGIS Pro。",
                        "缺少WebView2运行时",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"WebView2 Runtime版本: {webView2Version}");

                // 创建WebView2环境，指定用户数据文件夹避免权限问题
                var userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XIAOFUTools", "WebView2");
                
                // 确保目录存在，失败则降级到临时目录
                try
                {
                    if (!System.IO.Directory.Exists(userDataFolder))
                        System.IO.Directory.CreateDirectory(userDataFolder);
                }
                catch (Exception)
                {
                    userDataFolder = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(), "XIAOFUTools", "WebView2");
                    System.IO.Directory.CreateDirectory(userDataFolder);
                    System.Diagnostics.Debug.WriteLine($"WebView2用户数据降级到临时目录: {userDataFolder}");
                }
                
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder, null);
                
                // 等待WebView2初始化
                await webView.EnsureCoreWebView2Async(env);
                
                // 设置WebView2选项
                webView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;
                webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;

                // 注册消息接收器
                webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                
                // 注册页面加载完成事件(只注册一次)
                webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

                System.Diagnostics.Debug.WriteLine("WebView初始化完成");
                
                // 检查AI服务状态
                await CheckAIServiceStatus();

                var htmlPath = _viewModel.GetHtmlPath();
                webView.CoreWebView2.Navigate($"file:///{htmlPath.Replace("\\", "/")}");

                _isWebViewInitialized = true;
                _isInitializing = false;
                System.Diagnostics.Debug.WriteLine("[InitializeWebView] 初始化完成");

            }
            catch (Exception ex)
            {
                _isInitializing = false;
                System.Diagnostics.Debug.WriteLine($"WebView2初始化失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈: {ex.StackTrace}");
                
                // 提供更友好的错误提示
                string errorMsg;
                if (ex.Message.Contains("0x80004005") || ex.Message.Contains("access"))
                {
                    errorMsg = "WebView2初始化失败：权限不足。\n请尝试以管理员身份运行ArcGIS Pro。";
                }
                else if (ex is System.IO.FileNotFoundException)
                {
                    errorMsg = $"AI助手界面文件未找到。\n请确保插件文件完整。\n\n详细信息: {ex.Message}";
                }
                else
                {
                    errorMsg = $"AI助手界面加载失败: {ex.Message}\n\n" +
                               "可能的解决方案：\n" +
                               "1. 确保已安装 WebView2 Runtime\n" +
                               "2. 尝试以管理员身份运行\n" +
                               "3. 检查杀毒软件是否拦截";
                }
                
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    errorMsg,
                    "AI助手初始化失败",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                
                // 隐藏加载指示器
                loadingIndicator.Visibility = System.Windows.Visibility.Collapsed;
            }
        }
        
        private async void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[OnNavigationCompleted] 页面加载完成, 是否首次: {_isFirstNavigation}");
                
                // 隐藏加载指示器
                loadingIndicator.Visibility = System.Windows.Visibility.Collapsed;
                
                // 只在首次加载时执行初始化逻辑
                if (_isFirstNavigation)
                {
                    _isFirstNavigation = false;
                    
                    // 发送模型列表到前端
                    await SendModelsToWebView();
                    await SendToolPolicyToWebView();
                    
                    // 发送当前会话信息到前端
                    var agent = _viewModel?.GetAgent();
                    if (agent != null)
                    {
                        await SendMessageToWebView(new 
                        { 
                            type = "sessionInfo", 
                            sessionId = agent.GetCurrentSessionId(),
                            isNewSession = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnNavigationCompleted异常: {ex.Message}");
            }
        }

        private async Task CheckAIServiceStatus()
        {
            try
            {
                if (_viewModel == null)
                {
                    System.Diagnostics.Debug.WriteLine("ViewModel未初始化，等待Loaded事件");
                    return;
                }
                
                var agent = _viewModel.GetAgent();
                if (agent == null)
                {
                    await SendErrorToWebView("⚠️ AI服务初始化失败，请检查数据库配置。请查看调试输出以获取详细信息。");
                    System.Diagnostics.Debug.WriteLine("AI Agent为null，请检查GISAgentCore初始化日志");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("AI服务状态正常");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查AI服务状态失败: {ex.Message}");
                await SendErrorToWebView($"检查AI服务状态失败: {ex.Message}");
            }
        }

        private async void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var messageJson = e.WebMessageAsJson;
                var message = JObject.Parse(messageJson);
                var type = message["type"]?.ToString();

                switch (type)
                {
                    case "sendMessage":
                        var userMessage = message["message"]?.ToString();
                        var selectedModel = message["model"]?.ToString();
                        var selectedTool = message["tool"]?.ToString();
                        var mode = message["mode"]?.ToString() ?? "chat";
                        var images = message["images"]?.ToObject<List<string>>();
                        var execReferences = message["execReferences"]?.ToObject<List<ExecReference>>();
                        await HandleSendMessage(userMessage, selectedModel, selectedTool, mode, images, execReferences);
                        break;

                    case "newSession":
                        HandleNewSession();
                        break;

                    case "getHistorySessions":
                        await HandleGetHistorySessions();
                        break;

                    case "loadSession":
                        var sessionId = message["sessionId"]?.ToString();
                        await HandleLoadSession(sessionId);
                        break;

                    case "deleteSession":
                        var deleteSessionId = message["sessionId"]?.ToString();
                        await HandleDeleteSession(deleteSessionId);
                        break;

                    case "modeChanged":
                        var newMode = message["mode"]?.ToString();
                        HandleModeChanged(newMode);
                        break;

                    case "stopMessage":
                        HandleStopMessage();
                        break;

                    case "runPython":
                        var pythonCode = message["code"]?.ToString();
                        var codeId = message["codeId"]?.ToString();
                        await HandleRunPython(pythonCode, codeId);
                        break;

                    case "openSettings":
                        HandleOpenSettings();
                        break;

                    case "toolApprovalResponse":
                        var requestId = message["requestId"]?.ToString();
                        var decision = message["decision"]?.ToString();
                        HandleToolApprovalResponse(requestId, decision);
                        break;

                    case "exportToolCall":
                        var payload = message["payload"] as JObject;
                        await HandleExportToolCall(payload);
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine($"未知消息类型: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理WebView消息失败: {ex.Message}");
                await SendErrorToWebView($"处理消息失败: {ex.Message}");
            }
        }

        private async Task HandleSendMessage(string userMessage, string selectedModel = null, string selectedTool = null, string mode = "chat", List<string> images = null, List<ExecReference> execReferences = null)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return;

            System.Diagnostics.Debug.WriteLine($"[Backend] HandleSendMessage 被调用, 消息: {userMessage}, 模式: {mode}, 工具: {selectedTool}, 图片数量: {images?.Count ?? 0}, 引用数量: {execReferences?.Count ?? 0}");

            try
            {
                // 创建新的CancellationTokenSource
                _currentRequestCancellation?.Cancel();
                _currentRequestCancellation?.Dispose();
                _currentRequestCancellation = new System.Threading.CancellationTokenSource();
                var cancellationToken = _currentRequestCancellation.Token;
                var streamId = Guid.NewGuid().ToString("N");

                var request = new SendMessageRequest
                {
                    UserMessage = userMessage,
                    SelectedModel = selectedModel,
                    SelectedTool = selectedTool,
                    Mode = mode,
                    Images = images,
                    ExecReferences = execReferences?.Select(r => new ExecReferenceDto
                    {
                        Id = r.Id,
                        Output = r.Output,
                        Error = r.Error,
                        Success = r.Success
                    }).ToList()
                };

                // 通知前端开始流式响应
                await SendMessageToWebView(new { type = "streamStart", streamId });

                // 在后台线程执行AI请求，避免阻塞UI（fire-and-forget）
                _ = Task.Run(async () =>
                {
                    string currentTurnId = null;
                    try
                    {
                        await GetApplicationService().SendMessageAsync(
                            request,
                            chunk =>
                            {
                                SendMessageToWebView(new { type = "streamChunk", streamId, turnId = currentTurnId, content = chunk }).GetAwaiter().GetResult();
                            },
                            reasoning =>
                            {
                                SendMessageToWebView(new { type = "streamThinking", streamId, turnId = currentTurnId, content = reasoning }).GetAwaiter().GetResult();
                            },
                            notice =>
                            {
                                SendMessageToWebView(new
                                {
                                    type = "toolCall",
                                    streamId,
                                    turnId = notice.TurnId ?? currentTurnId,
                                    callId = notice.CallId,
                                    toolName = notice.ToolName,
                                    status = notice.Status,
                                    message = notice.Message,
                                    preview = notice.Preview,
                                    parameters = notice.Parameters,
                                    result = notice.Result
                                }).GetAwaiter().GetResult();
                            },
                            (approvalRequest, ct) => RequestToolApprovalAsync(streamId, currentTurnId, approvalRequest, ct),
                            turnId =>
                            {
                                currentTurnId = turnId;
                                SendMessageToWebView(new { type = "assistantTurnStart", streamId, turnId }).GetAwaiter().GetResult();
                            },
                            turnId =>
                            {
                                SendMessageToWebView(new { type = "assistantTurnEnd", streamId, turnId }).GetAwaiter().GetResult();
                            },
                            cancellationToken);

                        await SendMessageToWebView(new { type = "streamEnd", streamId });
                    }
                    catch (System.OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine("AI请求已被取消");
                        await SendMessageToWebView(new { type = "streamEnd", streamId });
                        await SendErrorToWebView("已停止生成");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"AI请求异常: {ex.Message}");
                        await SendErrorToWebView($"AI请求失败: {ex.Message}");
                    }
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HandleSendMessage异常: {ex.Message}");
                await SendErrorToWebView($"发送失败: {ex.Message}");
            }
        }

        private void HandleNewSession()
        {
            try
            {
                var sessionId = GetApplicationService().CreateNewSession();
                
                // 通知前端当前会话信息
                _ = SendMessageToWebView(new 
                { 
                    type = "sessionInfo", 
                    sessionId = sessionId,
                    isNewSession = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建新会话失败: {ex.Message}");
            }
        }

        private async Task HandleGetHistorySessions()
        {
            try
            {
                var sessionData = GetApplicationService().GetHistorySessions(20).Select(s => new
                {
                    sessionId = s.SessionId,
                    title = s.Title,
                    createdAt = s.CreatedAt,
                    lastActivity = s.LastActivity
                }).ToList();
                
                await SendMessageToWebView(new { type = "historySessions", sessions = sessionData });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取历史会话失败: {ex.Message}");
                await SendErrorToWebView($"获取历史会话失败: {ex.Message}");
            }
        }

        private async Task HandleLoadSession(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return;

                var sessionData = GetApplicationService().LoadSession(sessionId);
                var execData = sessionData.PythonExecutions.Select(e => new
                {
                    id = e.Id,
                    codeId = e.CodeId,
                    code = e.Code,
                    output = e.Output,
                    error = e.Error,
                    success = e.Success,
                    executionTime = e.ExecutionTime,
                    timestamp = e.Timestamp
                }).ToList();

                var messageData = sessionData.Messages.Select(m => new
                {
                    role = m.Role,
                    content = m.Content,
                    thinking = m.Thinking,
                    images = m.Images,
                    turnId = m.TurnId,
                    timestamp = m.Timestamp
                }).ToList();

                var toolCallData = sessionData.ToolCalls.Select(t => new
                {
                    id = t.Id,
                    turnId = t.TurnId,
                    callId = t.CallId,
                    toolName = t.ToolName,
                    parameters = t.Parameters,
                    result = t.Result,
                    status = t.Status,
                    timestamp = t.Timestamp
                }).ToList();
                
                await SendMessageToWebView(new 
                { 
                    type = "sessionLoaded", 
                    sessionId = sessionData.SessionId,
                    messages = messageData,
                    pythonExecutions = execData,
                    toolCalls = toolCallData
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载会话失败: {ex.Message}");
                await SendErrorToWebView($"加载会话失败: {ex.Message}");
            }
        }

        private async Task HandleDeleteSession(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return;

                GetApplicationService().DeleteSession(sessionId);
                
                System.Diagnostics.Debug.WriteLine($"会话已删除: {sessionId}");
                
                // 通知前端删除成功
                await SendMessageToWebView(new 
                { 
                    type = "sessionDeleted",
                    deletedSessionId = sessionId
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"删除会话失败: {ex.Message}");
                await SendErrorToWebView($"删除会话失败: {ex.Message}");
            }
        }

        private void HandleModeChanged(string mode)
        {
            try
            {
                if (string.IsNullOrEmpty(mode))
                    return;

                GetApplicationService().SetMode(mode);
                System.Diagnostics.Debug.WriteLine($"模式已切换: {mode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"切换模式失败: {ex.Message}");
            }
        }

        private void HandleStopMessage()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("收到停止请求");
                _currentRequestCancellation?.Cancel();
                CancelPendingToolApprovals("cancel");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止请求失败: {ex.Message}");
            }
        }

        private async Task<ToolApprovalDecisionDto> RequestToolApprovalAsync(
            string streamId,
            string turnId,
            ToolApprovalRequestDto approvalRequest,
            System.Threading.CancellationToken cancellationToken)
        {
            if (approvalRequest == null)
            {
                return new ToolApprovalDecisionDto { Decision = "cancel" };
            }

            var requestId = Guid.NewGuid().ToString("N");
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (_toolApprovalLock)
            {
                _pendingToolApprovals[requestId] = tcs;
            }

            try
            {
                await SendMessageToWebView(new
                {
                    type = "toolApprovalRequest",
                    streamId,
                    turnId,
                    requestId,
                    toolNames = approvalRequest.ToolNames,
                    callCount = approvalRequest.CallCount,
                    mode = approvalRequest.Mode,
                    message = approvalRequest.Message
                });

                using (cancellationToken.Register(() => tcs.TrySetCanceled()))
                {
                    var decision = await tcs.Task;
                    return new ToolApprovalDecisionDto
                    {
                        Decision = string.IsNullOrWhiteSpace(decision) ? "cancel" : decision
                    };
                }
            }
            catch
            {
                return new ToolApprovalDecisionDto { Decision = "cancel" };
            }
            finally
            {
                lock (_toolApprovalLock)
                {
                    _pendingToolApprovals.Remove(requestId);
                }
            }
        }

        private void HandleToolApprovalResponse(string requestId, string decision)
        {
            if (string.IsNullOrWhiteSpace(requestId))
            {
                return;
            }

            TaskCompletionSource<string> waiter = null;
            lock (_toolApprovalLock)
            {
                if (_pendingToolApprovals.TryGetValue(requestId, out var pending))
                {
                    waiter = pending;
                }
            }

            waiter?.TrySetResult(string.IsNullOrWhiteSpace(decision) ? "cancel" : decision);
        }

        private void CancelPendingToolApprovals(string decision)
        {
            List<TaskCompletionSource<string>> pending;
            lock (_toolApprovalLock)
            {
                pending = _pendingToolApprovals.Values.ToList();
                _pendingToolApprovals.Clear();
            }

            foreach (var waiter in pending)
            {
                waiter.TrySetResult(decision);
            }
        }

        private async Task SendMessageToWebView(object message)
        {
            if (!_isWebViewInitialized)
                return;

            try
            {
                var json = JsonConvert.SerializeObject(message);
                var script = $"window.dispatchEvent(new MessageEvent('message', {{ data: {json} }}));";
                
                // 如果已经在UI线程，直接执行
                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    await webView.CoreWebView2.ExecuteScriptAsync(script);
                }
                else
                {
                    // 从后台线程切回UI线程并等待执行完成，避免丢消息
                    var op = System.Windows.Application.Current.Dispatcher.InvokeAsync(
                        () => webView.CoreWebView2.ExecuteScriptAsync(script),
                        System.Windows.Threading.DispatcherPriority.Normal);
                    await await op.Task;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"发送消息到WebView失败: {ex.Message}");
            }
        }

        private async Task SendErrorToWebView(string errorMessage)
        {
            await SendMessageToWebView(new { type = "error", message = errorMessage });
        }
        
        private async Task SendModelsToWebView()
        {
            try
            {
                var models = GetApplicationService().GetModels().Select(s => new
                {
                    value = s.Value,
                    label = s.Label,
                    isDefault = s.IsDefault,
                    supportsVision = s.SupportsVision
                }).ToList();

                if (models.Count == 0)
                    return;
                
                await SendMessageToWebView(new { type = "modelList", models = models });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"发送模型列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理Python代码执行请求
        /// </summary>
        private async Task HandleRunPython(string code, string codeId)
        {
            System.Diagnostics.Debug.WriteLine($"[HandleRunPython] 开始执行Python代码, codeId={codeId}");

            try
            {
                // 通知前端开始执行
                await SendMessageToWebView(new
                {
                    type = "pythonRunning",
                    codeId = codeId
                });

                var sessionId = _viewModel?.GetAgent()?.GetCurrentSessionId();
                var result = await GetApplicationService().RunPythonAsync(code, codeId, sessionId);
                System.Diagnostics.Debug.WriteLine($"[HandleRunPython] 执行完成, success={result.Success}, exitCode={result.ExitCode}");

                // 发送执行结果到前端
                await SendMessageToWebView(new
                {
                    type = "pythonResult",
                    codeId = result.CodeId,
                    executionId = result.ExecutionId,
                    success = result.Success,
                    output = result.Output,
                    error = result.Error,
                    exitCode = result.ExitCode,
                    executionTime = result.ExecutionTime,
                    code = result.Code
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HandleRunPython] 执行异常: {ex.Message}");
                
                await SendMessageToWebView(new
                {
                    type = "pythonResult",
                    codeId = codeId,
                    success = false,
                    error = $"执行出错: {ex.Message}",
                    executionTime = 0
                });
            }
        }

        /// <summary>
        /// 打开设置窗口
        /// </summary>
        private void HandleOpenSettings()
        {
            try
            {
                // 使用BeginInvoke避免死锁
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        var settingsWindow = new SettingsWindow();
                        settingsWindow.ShowDialog();
                        
                        // 设置窗口关闭后，刷新模型列表到前端
                        await RefreshModelsAfterSettings();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HandleOpenSettings] 显示窗口失败: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HandleOpenSettings] 打开设置窗口失败: {ex.Message}");
            }
        }

        private async Task HandleExportToolCall(JObject payload)
        {
            if (payload == null)
            {
                await SendMessageToWebView(new { type = "toolExportFailed", message = "导出失败：未接收到工具数据。" });
                return;
            }

            try
            {
                var toolName = payload["toolName"]?.ToString();
                if (string.IsNullOrWhiteSpace(toolName))
                {
                    toolName = "tool";
                }

                var defaultName = $"{SanitizeFileName(toolName)}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                    DefaultExt = ".xlsx",
                    AddExtension = true,
                    FileName = defaultName,
                    OverwritePrompt = true
                };

                var saveResult = saveDialog.ShowDialog();
                if (saveResult != true)
                {
                    return;
                }

                ExportToolCallToExcel(saveDialog.FileName, payload);

                await SendMessageToWebView(new
                {
                    type = "toolExported",
                    filePath = saveDialog.FileName,
                    fileName = Path.GetFileName(saveDialog.FileName)
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"导出工具结果失败: {ex.Message}");
                await SendMessageToWebView(new { type = "toolExportFailed", message = $"导出失败: {ex.Message}" });
            }
        }

        private static void ExportToolCallToExcel(string filePath, JObject payload)
        {
            object excelApp = null;
            object workbook = null;
            object summarySheet = null;
            object parameterSheet = null;
            object resultSheet = null;

            try
            {
                var excelType = Type.GetTypeFromProgID("Excel.Application");
                if (excelType == null)
                {
                    throw new InvalidOperationException("未检测到 Excel 组件，请先安装 Microsoft Excel。");
                }

                dynamic excel = Activator.CreateInstance(excelType);
                excel.Visible = false;
                excel.DisplayAlerts = false;
                excelApp = excel;

                dynamic wb = excel.Workbooks.Add();
                workbook = wb;

                dynamic ws1 = wb.Worksheets[1];
                ws1.Name = "概览";
                summarySheet = ws1;

                dynamic ws2 = wb.Worksheets.Add(After: ws1);
                ws2.Name = "执行参数";
                parameterSheet = ws2;

                dynamic ws3 = wb.Worksheets.Add(After: ws2);
                ws3.Name = "执行结果";
                resultSheet = ws3;

                BuildSummarySheet(summarySheet, payload);
                BuildParameterSheet(parameterSheet, payload);
                BuildResultSheet(resultSheet, payload);

                const int xlOpenXMLWorkbook = 51;
                wb.SaveAs(filePath, xlOpenXMLWorkbook);
            }
            finally
            {
                if (workbook != null)
                {
                    try
                    {
                        ((dynamic)workbook).Close(false);
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (excelApp != null)
                {
                    try
                    {
                        ((dynamic)excelApp).Quit();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                ReleaseComObject(resultSheet);
                ReleaseComObject(parameterSheet);
                ReleaseComObject(summarySheet);
                ReleaseComObject(workbook);
                ReleaseComObject(excelApp);
            }
        }

        private static void BuildSummarySheet(object sheetObject, JObject payload)
        {
            dynamic sheet = sheetObject;
            var rows = new List<(string Key, string Value)>
            {
                ("工具名称", payload["toolName"]?.ToString() ?? string.Empty),
                ("调用ID", payload["callId"]?.ToString() ?? string.Empty),
                ("状态", payload["status"]?.ToString() ?? string.Empty),
                ("执行信息", payload["message"]?.ToString() ?? string.Empty),
                ("导出时间", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
            };

            if (TryParseJsonToken(payload["result"], out var resultToken) && resultToken is JObject resultObj)
            {
                var data = resultObj["data"] as JObject;
                if (data != null)
                {
                    rows.Add(("地图", data["mapName"]?.ToString() ?? string.Empty));
                    rows.Add(("输入图层", data["inputLayer"]?.ToString() ?? data["targetLayer"]?.ToString() ?? string.Empty));
                    rows.Add(("输出", data["outputFeatureClass"]?.ToString() ?? string.Empty));
                }
            }

            sheet.Cells[1, 1] = "工具执行导出";
            var titleRange = sheet.Range["A1", "B1"];
            titleRange.Merge();
            titleRange.Font.Bold = true;
            titleRange.Font.Size = 14;

            sheet.Cells[3, 1] = "字段";
            sheet.Cells[3, 2] = "值";
            ApplyHeaderStyle(sheet.Range["A3", "B3"]);

            var rowIndex = 4;
            foreach (var row in rows.Where(r => !string.IsNullOrWhiteSpace(r.Value)))
            {
                sheet.Cells[rowIndex, 1] = row.Key;
                sheet.Cells[rowIndex, 2] = row.Value;
                rowIndex++;
            }

            sheet.Columns[1].ColumnWidth = 20;
            sheet.Columns[2].ColumnWidth = 90;
            sheet.Columns[2].WrapText = true;
        }

        private static void BuildParameterSheet(object sheetObject, JObject payload)
        {
            dynamic sheet = sheetObject;
            sheet.Cells[1, 1] = "参数路径";
            sheet.Cells[1, 2] = "参数值";
            ApplyHeaderStyle(sheet.Range["A1", "B1"]);

            if (!TryParseJsonToken(payload["parameters"], out var parameterToken))
            {
                sheet.Cells[2, 1] = "(无参数)";
                sheet.Columns[1].AutoFit();
                sheet.Columns[2].ColumnWidth = 80;
                sheet.Columns[2].WrapText = true;
                return;
            }

            var flattenRows = new List<(string Key, string Value)>();
            FlattenToken(parameterToken, string.Empty, flattenRows);
            if (flattenRows.Count == 0)
            {
                flattenRows.Add(("(空)", string.Empty));
            }

            var rowIndex = 2;
            foreach (var row in flattenRows)
            {
                sheet.Cells[rowIndex, 1] = row.Key;
                sheet.Cells[rowIndex, 2] = row.Value;
                rowIndex++;
            }

            sheet.Columns[1].ColumnWidth = 40;
            sheet.Columns[2].ColumnWidth = 80;
            sheet.Columns[2].WrapText = true;
        }

        private static void BuildResultSheet(object sheetObject, JObject payload)
        {
            dynamic sheet = sheetObject;
            sheet.Cells[1, 1] = "字段";
            sheet.Cells[1, 2] = "值";
            ApplyHeaderStyle(sheet.Range["A1", "B1"]);

            var resultRows = new List<(string Key, string Value)>();
            JArray tableRows = null;

            if (TryParseJsonToken(payload["result"], out var resultToken) && resultToken is JObject resultObj)
            {
                resultRows.Add(("success", resultObj["success"]?.ToString() ?? string.Empty));
                resultRows.Add(("message", resultObj["message"]?.ToString() ?? string.Empty));
                resultRows.Add(("error", resultObj["error"]?.ToString() ?? string.Empty));

                var data = resultObj["data"] as JObject;
                if (data != null)
                {
                    var stats = data["stats"] as JObject;
                    if (stats != null)
                    {
                        foreach (var property in stats.Properties())
                        {
                            resultRows.Add(($"stats.{property.Name}", property.Value?.ToString() ?? string.Empty));
                        }
                    }

                    if (data["warnings"] is JArray warnings && warnings.Count > 0)
                    {
                        for (var i = 0; i < warnings.Count; i++)
                        {
                            resultRows.Add(($"warnings[{i}]", warnings[i]?.ToString() ?? string.Empty));
                        }
                    }

                    tableRows = data["rows"] as JArray;
                }
            }
            else
            {
                resultRows.Add(("preview", payload["preview"]?.ToString() ?? string.Empty));
            }

            var rowIndex = 2;
            foreach (var row in resultRows.Where(r => !string.IsNullOrWhiteSpace(r.Value)))
            {
                sheet.Cells[rowIndex, 1] = row.Key;
                sheet.Cells[rowIndex, 2] = row.Value;
                rowIndex++;
            }

            if (tableRows != null && tableRows.Count > 0)
            {
                rowIndex += 2;
                sheet.Cells[rowIndex, 1] = "明细数据";
                sheet.Cells[rowIndex, 1].Font.Bold = true;
                rowIndex++;

                var headers = CollectTableHeaders(tableRows);
                for (var c = 0; c < headers.Count; c++)
                {
                    sheet.Cells[rowIndex, c + 1] = headers[c];
                }

                ApplyHeaderStyle(sheet.Range[sheet.Cells[rowIndex, 1], sheet.Cells[rowIndex, headers.Count]]);
                rowIndex++;

                foreach (var rowToken in tableRows.OfType<JObject>())
                {
                    for (var c = 0; c < headers.Count; c++)
                    {
                        var value = rowToken[headers[c]];
                        sheet.Cells[rowIndex, c + 1] = value?.ToString() ?? string.Empty;
                    }

                    rowIndex++;
                }
            }

            sheet.Columns[1].ColumnWidth = 30;
            sheet.Columns[2].ColumnWidth = 90;
            sheet.Columns[2].WrapText = true;
            sheet.UsedRange.Columns.AutoFit();
        }

        private static List<string> CollectTableHeaders(JArray rows)
        {
            var headers = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows.OfType<JObject>())
            {
                foreach (var property in row.Properties())
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
                    var obj = (JObject)token;
                    if (!obj.Properties().Any())
                    {
                        rows.Add((string.IsNullOrWhiteSpace(path) ? "(空对象)" : path, "{}"));
                        return;
                    }

                    foreach (var property in obj.Properties())
                    {
                        var nextPath = string.IsNullOrWhiteSpace(path) ? property.Name : $"{path}.{property.Name}";
                        FlattenToken(property.Value, nextPath, rows);
                    }

                    break;

                case JTokenType.Array:
                    var array = (JArray)token;
                    if (array.Count == 0)
                    {
                        rows.Add((string.IsNullOrWhiteSpace(path) ? "(空数组)" : path, "[]"));
                        return;
                    }

                    for (var i = 0; i < array.Count; i++)
                    {
                        var nextPath = string.IsNullOrWhiteSpace(path) ? $"[{i}]" : $"{path}[{i}]";
                        FlattenToken(array[i], nextPath, rows);
                    }

                    break;

                default:
                    rows.Add((string.IsNullOrWhiteSpace(path) ? "value" : path, token.ToString()));
                    break;
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

            var text = token.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();
            if (!(text.StartsWith("{") && text.EndsWith("}")) && !(text.StartsWith("[") && text.EndsWith("]")))
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

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "tool";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "tool" : sanitized;
        }

        private static void ReleaseComObject(object comObject)
        {
            if (comObject == null)
            {
                return;
            }

            try
            {
                Marshal.ReleaseComObject(comObject);
            }
            catch
            {
                // ignore
            }
        }

        private static void ApplyHeaderStyle(object rangeObject)
        {
            if (rangeObject == null)
            {
                return;
            }

            dynamic range = rangeObject;
            range.Font.Bold = true;
            range.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.FromArgb(229, 231, 235));
            const int xlContinuous = 1;
            range.Borders.LineStyle = xlContinuous;
        }

        /// <summary>
        /// 设置窗口关闭后刷新模型列表和AI服务
        /// </summary>
        private async Task RefreshModelsAfterSettings()
        {
            try
            {
                // 1. 重新发送模型列表到前端（立即更新下拉框）
                await SendModelsToWebView();
                await SendToolPolicyToWebView();
                
                // 2. 重新初始化AI服务（使新的默认模型生效）
                GetApplicationService().ReloadAiService();
                
                System.Diagnostics.Debug.WriteLine("[RefreshModelsAfterSettings] 模型列表和AI服务已刷新");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[RefreshModelsAfterSettings] 刷新失败: {ex.Message}");
            }
        }

        private async Task SendToolPolicyToWebView()
        {
            try
            {
                var settings = DatabaseManager.Instance.GetToolExecutionSettings();
                await SendMessageToWebView(new
                {
                    type = "toolPolicy",
                    sensitiveMode = settings?.SensitiveMode != false
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"发送工具策略失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Python执行结果引用
    /// </summary>
    public class ExecReference
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public bool Success { get; set; }
    }
}
