using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Tools.User.AIAssistant.Services;
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

        public AIAssistantDockPaneView()
        {
            InitializeComponent();
            
            // 延迟初始化，等待DataContext绑定
            this.Loaded += OnViewLoaded;
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

                // 创建WebView2环境，指定用户数据文件夹避免权限问题
                var userDataFolder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XIAOFUTools", "WebView2");
                
                // 确保目录存在
                if (!System.IO.Directory.Exists(userDataFolder))
                {
                    System.IO.Directory.CreateDirectory(userDataFolder);
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
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"界面加载失败: {ex.Message}\n\n详细信息:\n{ex.StackTrace}",
                    "错误",
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
                        var mode = message["mode"]?.ToString() ?? "chat";
                        var images = message["images"]?.ToObject<List<string>>();
                        var execReferences = message["execReferences"]?.ToObject<List<ExecReference>>();
                        await HandleSendMessage(userMessage, selectedModel, mode, images, execReferences);
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

        private async Task HandleSendMessage(string userMessage, string selectedModel = null, string mode = "chat", List<string> images = null, List<ExecReference> execReferences = null)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
                return;

            System.Diagnostics.Debug.WriteLine($"[Backend] HandleSendMessage 被调用, 消息: {userMessage}, 模式: {mode}, 图片数量: {images?.Count ?? 0}, 引用数量: {execReferences?.Count ?? 0}");

            // 如果有执行结果引用，将其添加到消息上下文
            if (execReferences != null && execReferences.Count > 0)
            {
                var contextBuilder = new System.Text.StringBuilder();
                contextBuilder.AppendLine("\n---\n**引用的Python执行结果:**");
                foreach (var exec in execReferences)
                {
                    contextBuilder.AppendLine($"\n[执行#{exec.Id}] {(exec.Success ? "✅成功" : "❌失败")}");
                    contextBuilder.AppendLine($"```python\n{exec.Code}\n```");
                    if (!string.IsNullOrWhiteSpace(exec.Output))
                        contextBuilder.AppendLine($"输出:\n```\n{exec.Output}\n```");
                    if (!string.IsNullOrWhiteSpace(exec.Error))
                        contextBuilder.AppendLine($"错误:\n```\n{exec.Error}\n```");
                }
                contextBuilder.AppendLine("---\n");
                userMessage = userMessage + contextBuilder.ToString();
            }

            try
            {
                // 检查ViewModel和Agent是否存在
                if (_viewModel == null)
                {
                    await SendErrorToWebView("AI助手未初始化");
                    return;
                }

                var agent = _viewModel.GetAgent();
                if (agent == null)
                {
                    await SendErrorToWebView("AI Agent未初始化");
                    return;
                }

                // 设置工作模式
                agent.SetMode(mode);

                // 如果指定了模型，尝试切换
                if (!string.IsNullOrEmpty(selectedModel))
                {
                    try
                    {
                        agent.SwitchModel(selectedModel);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"切换模型失败: {ex.Message}");
                        await SendErrorToWebView($"切换模型失败: {ex.Message}");
                        return;
                    }
                }

                // 创建新的CancellationTokenSource
                _currentRequestCancellation?.Cancel();
                _currentRequestCancellation?.Dispose();
                _currentRequestCancellation = new System.Threading.CancellationTokenSource();
                var cancellationToken = _currentRequestCancellation.Token;

                // 通知前端开始流式响应
                await SendMessageToWebView(new { type = "streamStart" });

                // 在后台线程执行AI请求，避免阻塞UI（fire-and-forget）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await agent.SendMessageAsync(
                            userMessage,
                            async (chunk) =>
                            {
                                await SendMessageToWebView(new { type = "streamChunk", content = chunk });
                            },
                            cancellationToken,
                            async (reasoning) =>
                            {
                                await SendMessageToWebView(new { type = "streamThinking", content = reasoning });
                            },
                            images);

                        await SendMessageToWebView(new { type = "streamEnd" });
                    }
                    catch (System.OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine("AI请求已被取消");
                        await SendMessageToWebView(new { type = "streamEnd" });
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
                if (_viewModel == null)
                    return;

                var agent = _viewModel.GetAgent();
                agent?.CreateNewSession();
                
                // 通知前端当前会话信息
                _ = SendMessageToWebView(new 
                { 
                    type = "sessionInfo", 
                    sessionId = agent?.GetCurrentSessionId(),
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
                var dbManager = Database.DatabaseManager.Instance;
                var sessions = dbManager.GetRecentSessions(20);
                
                var sessionData = sessions.Select(s => new
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

                if (_viewModel == null)
                {
                    await SendErrorToWebView("AI助手未初始化");
                    return;
                }

                var agent = _viewModel.GetAgent();
                if (agent == null)
                {
                    await SendErrorToWebView("AI Agent未初始化");
                    return;
                }

                // 加载会话历史
                var dbManager = Database.DatabaseManager.Instance;
                var messages = dbManager.GetConversationHistory(sessionId, 100);
                
                // 加载Python执行记录
                var pythonExecutions = dbManager.GetPythonExecutions(sessionId);
                var execData = pythonExecutions.Select(e => new
                {
                    id = e.Id,
                    codeId = e.CodeId,
                    code = e.Code,
                    output = e.Output,
                    error = e.Error,
                    success = e.Success,
                    executionTime = e.ExecutionTimeMs,
                    timestamp = e.Timestamp
                }).ToList();
                
                // 切换到指定会话
                agent.SwitchSession(sessionId);
                
                // 转换消息格式
                var messageData = messages.Select(m => new
                {
                    role = m.Role,
                    content = m.Content,
                    thinking = m.Thinking,
                    images = m.Images,  // 添加images字段
                    timestamp = m.Timestamp
                }).ToList();
                
                await SendMessageToWebView(new 
                { 
                    type = "sessionLoaded", 
                    sessionId = sessionId,
                    messages = messageData,
                    pythonExecutions = execData  // 添加Python执行记录
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

                var dbManager = Database.DatabaseManager.Instance;
                dbManager.DeleteSession(sessionId);
                
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

                if (_viewModel == null)
                    return;

                var agent = _viewModel.GetAgent();
                if (agent != null)
                {
                    agent.SetMode(mode);
                    System.Diagnostics.Debug.WriteLine($"模式已切换: {mode}");
                }
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止请求失败: {ex.Message}");
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
                    // 从后台线程调用，使用Invoke避免嵌套async
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        _ = webView.CoreWebView2.ExecuteScriptAsync(script);
                    }, System.Windows.Threading.DispatcherPriority.Normal);
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
                if (_viewModel == null || _viewModel.GetAgent() == null)
                    return;
                
                // 从数据库获取所有模型
                var dbManager = Database.DatabaseManager.Instance;
                var services = dbManager.GetAllServices();
                
                if (services == null || services.Count == 0)
                    return;
                
                // 转换为前端需要的格式
                var models = services.Select(s => new
                {
                    value = s.ModelName,
                    label = s.Name,
                    isDefault = s.IsDefault,
                    supportsVision = s.SupportsVision
                }).ToList();
                
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
            if (string.IsNullOrWhiteSpace(code))
            {
                await SendMessageToWebView(new
                {
                    type = "pythonResult",
                    codeId = codeId,
                    success = false,
                    error = "代码不能为空"
                });
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[HandleRunPython] 开始执行Python代码, codeId={codeId}");

            try
            {
                // 通知前端开始执行
                await SendMessageToWebView(new
                {
                    type = "pythonRunning",
                    codeId = codeId
                });

                // 使用Python执行服务执行代码
                var pythonService = PythonExecutionService.Instance;
                
                if (!pythonService.IsPythonAvailable)
                {
                    await SendMessageToWebView(new
                    {
                        type = "pythonResult",
                        codeId = codeId,
                        success = false,
                        error = "Python环境不可用。请确保ArcGIS Pro已正确安装。",
                        executionTime = 0
                    });
                    return;
                }

                var result = await pythonService.ExecuteCodeAsync(code);

                System.Diagnostics.Debug.WriteLine($"[HandleRunPython] 执行完成, success={result.Success}, exitCode={result.ExitCode}");

                // 保存执行记录到数据库
                var sessionId = _viewModel?.GetAgent()?.GetCurrentSessionId();
                int executionId = -1;
                if (!string.IsNullOrEmpty(sessionId))
                {
                    executionId = DatabaseManager.Instance.SavePythonExecution(
                        sessionId,
                        codeId,
                        code,
                        result.Output,
                        result.Error,
                        result.Success,
                        result.ExecutionTimeMs
                    );
                    System.Diagnostics.Debug.WriteLine($"[HandleRunPython] 执行记录已保存, executionId={executionId}");
                }

                // 发送执行结果到前端
                await SendMessageToWebView(new
                {
                    type = "pythonResult",
                    codeId = codeId,
                    executionId = executionId,
                    success = result.Success,
                    output = result.Output,
                    error = result.Error,
                    exitCode = result.ExitCode,
                    executionTime = result.ExecutionTimeMs,
                    code = code  // 返回代码用于@引用
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
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        var settingsWindow = new SettingsWindow();
                        settingsWindow.ShowDialog();
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
