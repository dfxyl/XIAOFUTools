using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework.Controls;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Features.User.AIAssistant
{
    /// <summary>
    /// 模型配置视图模型
    /// </summary>
    public class ModelConfigViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ApiEndpoint { get; set; }
        public string ModelName { get; set; }
        public string ApiKey { get; set; }
        public bool IsDefault { get; set; }
        public int MaxTokens { get; set; }
        public int ContextWindowTokens { get; set; }
        public double Temperature { get; set; }
        public bool SupportsVision { get; set; }
        
        /// <summary>
        /// 是否为内置模型
        /// </summary>
        public bool IsBuiltIn => false;
        
        /// <summary>
        /// 显示的密钥（内置不显示，用户自定义显示脱敏）
        /// </summary>
        public string DisplayApiKey
        {
            get
            {
                if (string.IsNullOrEmpty(ApiKey) || ApiKey == "local-no-key")
                    return "（本地模型）";
                if (ApiKey.Length < 8)
                    return "****";
                return ApiKey.Substring(0, 4) + "****" + ApiKey.Substring(ApiKey.Length - 4);
            }
        }
    }

    /// <summary>
    /// AI助手设置窗口
    /// </summary>
    public partial class SettingsWindow : ProWindow
    {
        private List<ModelConfigViewModel> _models = new List<ModelConfigViewModel>();
        private readonly Dictionary<string, (CheckBox chatSwitch, CheckBox agentSwitch)> _toolSwitchMap =
            new Dictionary<string, (CheckBox chatSwitch, CheckBox agentSwitch)>(StringComparer.OrdinalIgnoreCase);
        
        public SettingsWindow()
        {
            InitializeComponent();
            
            // 窗口加载完成后再注册事件和加载设置
            Loaded += (s, e) =>
            {
                navPython.Checked += NavItem_Checked;
                LoadSettings();
            };
        }
    }
}
