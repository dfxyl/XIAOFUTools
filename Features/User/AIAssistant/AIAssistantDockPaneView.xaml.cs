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
using XIAOFUTools.Features.User.AIAssistant.Application;
using XIAOFUTools.Features.User.AIAssistant.Database;

namespace XIAOFUTools.Features.User.AIAssistant
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
        private readonly object _requestLock = new object();
        private readonly Dictionary<string, ActiveRequestInfo> _activeRequests = new Dictionary<string, ActiveRequestInfo>();
        private AIAssistantApplicationService _applicationService;
        private readonly object _toolApprovalLock = new object();
        private readonly Dictionary<string, PendingToolApprovalInfo> _pendingToolApprovals = new Dictionary<string, PendingToolApprovalInfo>();

        public AIAssistantDockPaneView()
        {
            InitializeComponent();
            
            // 延迟初始化，等待DataContext绑定
            this.Loaded += OnViewLoaded;
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
