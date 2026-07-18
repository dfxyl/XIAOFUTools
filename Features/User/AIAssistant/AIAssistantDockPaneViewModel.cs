using System;
using System.Threading.Tasks;
using System.IO;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Features.User.AIAssistant.Agent;
using XIAOFUTools.Features.User.AIAssistant.Infrastructure;

namespace XIAOFUTools.Features.User.AIAssistant
{
    /// <summary>
    /// AI助手停靠窗格ViewModel
    /// </summary>
    internal class AIAssistantDockPaneViewModel : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_AIAssistantDockPane";
        private readonly AIAssistantUiPathResolver _uiPathResolver = new AIAssistantUiPathResolver();
        private GISAgentCore _agentCore;

        protected AIAssistantDockPaneViewModel()
        {
            InitializeAgent();
        }

        private void InitializeAgent()
        {
            try
            {
                _agentCore = new GISAgentCore();
                
                if (_agentCore.IsFullyInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("GIS Agent完全初始化成功");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"GIS Agent部分初始化: {_agentCore.InitializationError}");
                    // 不弹窗，让用户在使用时看到具体错误
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GIS Agent初始化失败: {ex.Message}");
                // 即使初始化失败也不弹窗阻塞，允许界面加载
                // 用户发送消息时会看到具体错误提示
                _agentCore = null;
            }
        }

        /// <summary>
        /// 获取HTML文件路径
        /// </summary>
        public string GetHtmlPath()
        {
            try
            {
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string htmlPath = _uiPathResolver.ResolveChatHtmlPath(assemblyLocation);
                
                System.Diagnostics.Debug.WriteLine($"HTML路径: {htmlPath}");
                
                return htmlPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取HTML路径失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取GIS Agent实例
        /// </summary>
        public GISAgentCore GetAgent()
        {
            return _agentCore;
        }

        /// <summary>
        /// 显示停靠窗格
        /// </summary>
        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }

        /// <summary>
        /// 窗格关闭时清理资源
        /// </summary>
        protected override void OnHidden()
        {
            // 可以在这里添加清理逻辑
            base.OnHidden();
        }
    }

    /// <summary>
    /// 按钮实现显示停靠窗格
    /// </summary>
    internal class AIAssistantDockPane_ShowButton : Button
    {
        protected override void OnClick()
        {
            AIAssistantDockPaneViewModel.Show();
        }
    }
}
