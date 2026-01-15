using System;
using System.Threading.Tasks;
using System.IO;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.User.AIAssistant.Agent;

namespace XIAOFUTools.Tools.User.AIAssistant
{
    /// <summary>
    /// AI助手停靠窗格ViewModel
    /// </summary>
    internal class AIAssistantDockPaneViewModel : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_AIAssistantDockPane";
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
                System.Diagnostics.Debug.WriteLine("GIS Agent初始化成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GIS Agent初始化失败: {ex.Message}");
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"AI助手初始化失败: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取HTML文件路径
        /// </summary>
        public string GetHtmlPath()
        {
            try
            {
                // 参考预设图层功能的路径获取方法
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                
                if (string.IsNullOrEmpty(assemblyLocation))
                {
                    throw new InvalidOperationException("无法获取程序集位置");
                }
                
                string assemblyDir = Path.GetDirectoryName(assemblyLocation);
                
                if (string.IsNullOrEmpty(assemblyDir))
                {
                    throw new InvalidOperationException("无法获取程序集目录");
                }
                
                // 使用相对路径构建HTML文件路径
                string relativePath = Path.Combine("Tools", "User", "AIAssistant", "UI", "chat.html");
                string htmlPath = Path.GetFullPath(Path.Combine(assemblyDir, relativePath));
                
                System.Diagnostics.Debug.WriteLine($"HTML路径: {htmlPath}");
                
                if (!File.Exists(htmlPath))
                {
                    System.Diagnostics.Debug.WriteLine($"HTML文件不存在: {htmlPath}");
                    System.Diagnostics.Debug.WriteLine($"程序集位置: {assemblyLocation}");
                    System.Diagnostics.Debug.WriteLine($"程序集目录: {assemblyDir}");
                    throw new FileNotFoundException($"AI助手界面文件未找到: {htmlPath}", htmlPath);
                }
                
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
