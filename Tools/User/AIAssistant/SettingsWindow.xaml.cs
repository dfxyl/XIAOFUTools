using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ArcGIS.Desktop.Framework.Controls;
using XIAOFUTools.Tools.User.AIAssistant.Database;
using XIAOFUTools.Tools.User.AIAssistant.Services;

namespace XIAOFUTools.Tools.User.AIAssistant
{
    /// <summary>
    /// 模型配置视图模型
    /// </summary>
    public class ModelConfigViewModel
    {
        // 内置密钥列表（不显示这些密钥）
        private static readonly HashSet<string> BuiltInApiKeys = new HashSet<string>
        {
            "sk-3df1480042c041a8b034da2296b04fa2",
            "sk-vxzwulcezfneinkzkefolpbgikpdhxnrdpasnoygjdpcamyi",
            "sk-Khak6GZI5AWDbhZAFdJd6C9T6dgguhcX1WnbRATiZyzk7fCm"
        };
        
        public int Id { get; set; }
        public string Name { get; set; }
        public string ApiEndpoint { get; set; }
        public string ModelName { get; set; }
        public string ApiKey { get; set; }
        public bool IsDefault { get; set; }
        public int MaxTokens { get; set; }
        public double Temperature { get; set; }
        public bool SupportsVision { get; set; }
        
        /// <summary>
        /// 是否为内置模型
        /// </summary>
        public bool IsBuiltIn => BuiltInApiKeys.Contains(ApiKey);
        
        /// <summary>
        /// 显示的密钥（内置不显示，用户自定义显示脱敏）
        /// </summary>
        public string DisplayApiKey
        {
            get
            {
                if (IsBuiltIn)
                    return "（内置密钥）";
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

        private RadioButton ToolNavButton => FindName("navTool") as RadioButton;
        private ScrollViewer ToolPanel => FindName("panelTool") as ScrollViewer;
        private ComboBox ToolRunModeInChatComboBox => FindName("cmbToolRunModeInChat") as ComboBox;
        private CheckBox SensitiveModeCheckBox => FindName("chkSensitiveMode") as CheckBox;
        private StackPanel ToolListPanel => FindName("toolListPanel") as StackPanel;
        
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
        
        /// <summary>
        /// 导航切换
        /// </summary>
        private void NavItem_Checked(object sender, RoutedEventArgs e)
        {
            var toolPanel = ToolPanel;
            var toolNav = ToolNavButton;

            if (panelPython == null || toolPanel == null || panelModel == null || panelAbout == null)
                return;
                 
            if (sender is RadioButton rb)
            {
                panelPython.Visibility = Visibility.Collapsed;
                toolPanel.Visibility = Visibility.Collapsed;
                panelModel.Visibility = Visibility.Collapsed;
                panelAbout.Visibility = Visibility.Collapsed;
                 
                if (rb == navPython) panelPython.Visibility = Visibility.Visible;
                else if (toolNav != null && rb == toolNav) toolPanel.Visibility = Visibility.Visible;
                else if (rb == navModel)
                {
                    panelModel.Visibility = Visibility.Visible;
                    LoadModels();
                }
                else if (rb == navAbout) panelAbout.Visibility = Visibility.Visible;
            }
        }
        
        /// <summary>
        /// 加载所有设置
        /// </summary>
        private void LoadSettings()
        {
            LoadPythonSettings();
            LoadToolSettings();
        }
        
        /// <summary>
        /// 加载Python相关设置
        /// </summary>
        private void LoadPythonSettings()
        {
            try
            {
                cmbExecutionMode.SelectedIndex = PythonExecutionService.Instance.UseFixedLocation ? 1 : 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow] 加载Python设置失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 加载模型配置
        /// </summary>
        private void LoadModels()
        {
            try
            {
                var services = DatabaseManager.Instance.GetAllServices();
                _models = services.Select(s => new ModelConfigViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    ApiEndpoint = s.ApiEndpoint,
                    ModelName = s.ModelName,
                    ApiKey = s.ApiKey,
                    IsDefault = s.IsDefault,
                    MaxTokens = s.MaxTokens,
                    Temperature = s.Temperature,
                    SupportsVision = s.SupportsVision
                }).ToList();
                
                RenderModelList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow] 加载模型配置失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 渲染模型列表
        /// </summary>
        private void RenderModelList()
        {
            modelListPanel.Children.Clear();
            
            // 更新模型数量统计
            var defaultModel = _models.FirstOrDefault(m => m.IsDefault);
            txtModelCount.Text = $"共 {_models.Count} 个模型" + 
                (defaultModel != null ? $"，默认: {defaultModel.Name}" : "");
            
            foreach (var model in _models)
            {
                var card = CreateModelCard(model);
                modelListPanel.Children.Add(card);
            }
            
            if (_models.Count == 0)
            {
                var emptyText = new TextBlock
                {
                    Text = "暂无模型配置，点击「+ 添加模型」开始配置",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ca3af")),
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                modelListPanel.Children.Add(emptyText);
            }
        }

        /// <summary>
        /// 加载工具执行设置
        /// </summary>
        private void LoadToolSettings()
        {
            try
            {
                var settings = DatabaseManager.Instance.GetToolExecutionSettings();
                settings.EnsureDefaults();

                if (ToolRunModeInChatComboBox != null)
                {
                    ToolRunModeInChatComboBox.SelectedIndex = settings.PromptBeforeToolInChat ? 1 : 0;
                }

                if (SensitiveModeCheckBox != null)
                {
                    SensitiveModeCheckBox.IsChecked = settings.SensitiveMode;
                }

                RenderToolSwitches(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow] 加载工具设置失败: {ex.Message}");
            }
        }

        private void RenderToolSwitches(ToolExecutionSettings settings)
        {
            var toolListPanel = ToolListPanel;
            if (toolListPanel == null)
            {
                return;
            }

            toolListPanel.Children.Clear();
            _toolSwitchMap.Clear();

            var groupedTools = AIAssistantToolCatalog.Tools
                .GroupBy(t => NormalizeToolGroup(t.ToolGroup))
                .OrderBy(g => GetToolGroupOrder(g.Key))
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var group in groupedTools)
            {
                toolListPanel.Children.Add(CreateToolGroupHeader(group.Key));

                foreach (var tool in group.OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase))
                {
                    var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

                    var textPanel = new StackPanel();
                    textPanel.Children.Add(new TextBlock
                    {
                        Text = tool.DisplayName,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1f2937"))
                    });
                    textPanel.Children.Add(new TextBlock
                    {
                        Text = BuildToolDescription(tool),
                        FontSize = 11,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b7280")),
                        TextWrapping = TextWrapping.Wrap
                    });
                    Grid.SetColumn(textPanel, 0);
                    row.Children.Add(textPanel);

                    var chatSwitch = new CheckBox
                    {
                        Style = FindResource("SwitchCheckBox") as Style,
                        IsChecked = settings.IsToolEnabled(tool.ToolName, "chat"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = $"{tool.DisplayName} - Chat"
                    };
                    Grid.SetColumn(chatSwitch, 1);
                    row.Children.Add(chatSwitch);

                    var agentSwitch = new CheckBox
                    {
                        Style = FindResource("SwitchCheckBox") as Style,
                        IsChecked = settings.IsToolEnabled(tool.ToolName, "agent"),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        ToolTip = $"{tool.DisplayName} - Agent"
                    };
                    Grid.SetColumn(agentSwitch, 2);
                    row.Children.Add(agentSwitch);

                    AttachEnableWarning(tool, chatSwitch, "Chat");
                    AttachEnableWarning(tool, agentSwitch, "Agent");

                    _toolSwitchMap[tool.ToolName] = (chatSwitch, agentSwitch);
                    toolListPanel.Children.Add(row);
                }
            }
        }

        private Border CreateToolGroupHeader(string groupName)
        {
            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f3f4f6")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 12, 0, 6),
                Child = new TextBlock
                {
                    Text = groupName,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151"))
                }
            };
        }

        private static string NormalizeToolGroup(string groupName)
        {
            return string.IsNullOrWhiteSpace(groupName) ? "其他" : groupName.Trim();
        }

        private static int GetToolGroupOrder(string groupName)
        {
            return groupName switch
            {
                "基础与联网" => 1,
                "工程与图层只读" => 2,
                "数据读取与画像" => 3,
                "空间分析工具" => 4,
                _ => 99
            };
        }

        private static string BuildToolDescription(ToolDescriptor tool)
        {
            if (tool == null)
            {
                return string.Empty;
            }

            var description = tool.Description ?? string.Empty;
            if (tool.RequiresSafetyWarning)
            {
                return description + "（写入型：仅新增输出，不修改或删除输入）";
            }

            if (tool.RequiresDataAccess)
            {
                return description + "（涉及实际数据）";
            }

            return description;
        }

        private void AttachEnableWarning(ToolDescriptor tool, CheckBox toggle, string modeLabel)
        {
            if (tool == null || toggle == null || !tool.RequiresSafetyWarning)
            {
                return;
            }

            RoutedEventHandler handler = null;
            handler = (sender, args) =>
            {
                if (toggle.IsChecked != true)
                {
                    return;
                }

                var result = MessageBox.Show(
                    $"你正在启用【{tool.DisplayName}】({modeLabel})。\n\n" +
                    "该工具会创建新的输出数据，但不会修改或删除输入数据。\n" +
                    "请确认输出路径和数据范围后再运行。\n\n" +
                    "是否继续启用？",
                    "工具安全提醒",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    return;
                }

                toggle.Checked -= handler;
                toggle.IsChecked = false;
                toggle.Checked += handler;
            };

            toggle.Checked += handler;
        }
        
        /// <summary>
        /// 显示操作成功提示（自动消失）
        /// </summary>
        private void ShowToast(string message, bool isSuccess = true)
        {
            try
            {
                toastBorder.Background = isSuccess 
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dcfce7"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fee2e2"));
                toastText.Foreground = isSuccess
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16a34a"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dc2626"));
                toastText.Text = message;
                toastBorder.Visibility = Visibility.Visible;
                
                // 3秒后自动隐藏
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                timer.Tick += (s, e) =>
                {
                    toastBorder.Visibility = Visibility.Collapsed;
                    timer.Stop();
                };
                timer.Start();
            }
            catch { }
        }
        
        /// <summary>
        /// 创建模型卡片
        /// </summary>
        private Border CreateModelCard(ModelConfigViewModel model)
        {
            var card = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 8)
            };
            
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            // 左侧信息
            var infoPanel = new StackPanel();
            
            // 名称行
            var namePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            namePanel.Children.Add(new TextBlock { Text = model.Name, FontWeight = FontWeights.SemiBold, FontSize = 13 });
            
            if (model.IsDefault)
            {
                var defaultBadge = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dbeafe")),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(8, 0, 0, 0)
                };
                defaultBadge.Child = new TextBlock { Text = "默认", FontSize = 10, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1d4ed8")) };
                namePanel.Children.Add(defaultBadge);
            }
            
            if (model.SupportsVision)
            {
                var visionBadge = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dcfce7")),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(4, 0, 0, 0)
                };
                visionBadge.Child = new TextBlock { Text = "视觉", FontSize = 10, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16a34a")) };
                namePanel.Children.Add(visionBadge);
            }
            
            infoPanel.Children.Add(namePanel);
            infoPanel.Children.Add(new TextBlock { Text = model.ModelName, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6b7280")), FontSize = 11, Margin = new Thickness(0, 0, 0, 2) });
            infoPanel.Children.Add(new TextBlock { Text = model.ApiEndpoint, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ca3af")), FontSize = 10 });
            infoPanel.Children.Add(new TextBlock { Text = $"密钥: {model.DisplayApiKey}", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ca3af")), FontSize = 10, Margin = new Thickness(0, 2, 0, 0) });
            
            Grid.SetColumn(infoPanel, 0);
            grid.Children.Add(infoPanel);
            
            // 右侧按钮
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            if (!model.IsDefault)
            {
                var setDefaultBtn = CreateSmallButton("设为默认", model.Id, BtnSetDefault_Click);
                setDefaultBtn.Margin = new Thickness(0, 0, 4, 0);
                btnPanel.Children.Add(setDefaultBtn);
            }
            
            var editBtn = CreateSmallButton("编辑", model.Id, BtnEditModel_Click);
            editBtn.Margin = new Thickness(0, 0, 4, 0);
            btnPanel.Children.Add(editBtn);
            
            var deleteBtn = CreateDangerButton("删除", model.Id, BtnDeleteModel_Click);
            btnPanel.Children.Add(deleteBtn);
            
            Grid.SetColumn(btnPanel, 1);
            grid.Children.Add(btnPanel);
            
            card.Child = grid;
            return card;
        }
        
        private Button CreateSmallButton(string text, int id, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = text,
                Tag = id,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f3f4f6")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                Padding = new Thickness(8, 4, 8, 4),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 11
            };
            btn.Click += handler;
            return btn;
        }
        
        private Button CreateDangerButton(string text, int id, RoutedEventHandler handler)
        {
            var btn = new Button
            {
                Content = text,
                Tag = id,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#fee2e2")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dc2626")),
                Padding = new Thickness(8, 4, 8, 4),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 11
            };
            btn.Click += handler;
            return btn;
        }
        
        /// <summary>
        /// 添加模型
        /// </summary>
        private void BtnAddModel_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ModelEditDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var config = new AIServiceConfig
                    {
                        Name = dialog.ModelName,
                        ApiEndpoint = dialog.ApiEndpoint,
                        ModelName = dialog.ModelId,
                        ApiKey = dialog.ApiKey,
                        MaxTokens = dialog.MaxTokens,
                        Temperature = dialog.Temperature,
                        SupportsVision = dialog.SupportsVision,
                        IsDefault = false
                    };
                    
                    DatabaseManager.Instance.InsertService(config);
                    LoadModels();
                    ShowToast($"模型「{config.Name}」添加成功");
                }
                catch (Exception ex)
                {
                    ShowToast($"添加失败: {ex.Message}", false);
                }
            }
        }
        
        /// <summary>
        /// 编辑模型
        /// </summary>
        private void BtnEditModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var model = _models.FirstOrDefault(m => m.Id == id);
                if (model == null) return;
                
                var dialog = new ModelEditDialog(model);
                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        var config = new AIServiceConfig
                        {
                            Id = id,
                            Name = dialog.ModelName,
                            ApiEndpoint = dialog.ApiEndpoint,
                            ModelName = dialog.ModelId,
                            ApiKey = dialog.ApiKey,
                            MaxTokens = dialog.MaxTokens,
                            Temperature = dialog.Temperature,
                            SupportsVision = dialog.SupportsVision,
                            IsDefault = model.IsDefault
                        };
                        
                        DatabaseManager.Instance.UpdateService(config);
                        LoadModels();
                        ShowToast($"模型「{config.Name}」已更新");
                    }
                    catch (Exception ex)
                    {
                        ShowToast($"更新失败: {ex.Message}", false);
                    }
                }
            }
        }
        
        /// <summary>
        /// 删除模型
        /// </summary>
        private void BtnDeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                var model = _models.FirstOrDefault(m => m.Id == id);
                if (model == null) return;
                
                var result = MessageBox.Show(
                    $"确定要删除模型「{model.Name}」吗？",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                    
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var modelName = model.Name;
                        DatabaseManager.Instance.DeleteService(id);
                        LoadModels();
                        ShowToast($"模型「{modelName}」已删除");
                    }
                    catch (Exception ex)
                    {
                        ShowToast($"删除失败: {ex.Message}", false);
                    }
                }
            }
        }
        
        /// <summary>
        /// 设为默认
        /// </summary>
        private void BtnSetDefault_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int id)
            {
                try
                {
                    DatabaseManager.Instance.SetDefaultService(id);
                    LoadModels();
                    var model = _models.FirstOrDefault(m => m.Id == id);
                    ShowToast($"已将「{model?.Name}」设为默认模型");
                }
                catch (Exception ex)
                {
                    ShowToast($"设置失败: {ex.Message}", false);
                }
            }
        }
        
        /// <summary>
        /// 重置默认模型
        /// </summary>
        private void BtnResetModels_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要重置为默认模型配置吗？\n\n这将删除所有自定义配置并恢复初始模型。",
                "确认重置",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    DatabaseManager.Instance.ResetToDefaultServices();
                    LoadModels();
                    ShowToast("已重置为默认模型配置");
                }
                catch (Exception ex)
                {
                    ShowToast($"重置失败: {ex.Message}", false);
                }
            }
        }
        
        /// <summary>
        /// 保存设置
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SavePythonSettings();
                SaveToolSettings();
                 
                var modeText = PythonExecutionService.Instance.UseFixedLocation ? "不询问（固定位置）" : "每次询问";
                var chatPrompt = ToolRunModeInChatComboBox?.SelectedIndex == 1 ? "运行前提示" : "自动运行";
                var sensitiveModeText = SensitiveModeCheckBox?.IsChecked == true ? "开启" : "关闭";
                var enabledCount = _toolSwitchMap.Count(kv => kv.Value.chatSwitch?.IsChecked == true || kv.Value.agentSwitch?.IsChecked == true);
                MessageBox.Show(
                    $"设置已保存！\n\nPython执行模式：{modeText}\nChat工具策略：{chatPrompt}\n敏感模式：{sensitiveModeText}\n启用工具数：{enabledCount}/{_toolSwitchMap.Count}",
                    "保存成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 保存Python相关设置
        /// </summary>
        private void SavePythonSettings()
        {
            PythonExecutionService.Instance.UseFixedLocation = cmbExecutionMode.SelectedIndex == 1;
            PythonExecutionService.Instance.SaveSettings();
        }

        /// <summary>
        /// 保存工具执行设置
        /// </summary>
        private void SaveToolSettings()
        {
            var settings = new ToolExecutionSettings
            {
                PromptBeforeToolInChat = ToolRunModeInChatComboBox?.SelectedIndex == 1,
                SensitiveMode = SensitiveModeCheckBox?.IsChecked == true
            };

            foreach (var tool in AIAssistantToolCatalog.Tools)
            {
                if (_toolSwitchMap.TryGetValue(tool.ToolName, out var switches))
                {
                    settings.SetToolMode(
                        tool.ToolName,
                        switches.chatSwitch?.IsChecked == true,
                        switches.agentSwitch?.IsChecked == true);
                }
                else
                {
                    settings.SetToolMode(tool.ToolName, tool.DefaultChatEnabled, tool.DefaultAgentEnabled);
                }
            }

            DatabaseManager.Instance.SaveToolExecutionSettings(settings);
        }
        
        /// <summary>
        /// 取消
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
