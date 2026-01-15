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
            if (panelPython == null || panelModel == null || panelAbout == null)
                return;
                
            if (sender is RadioButton rb)
            {
                panelPython.Visibility = Visibility.Collapsed;
                panelModel.Visibility = Visibility.Collapsed;
                panelAbout.Visibility = Visibility.Collapsed;
                
                if (rb == navPython) panelPython.Visibility = Visibility.Visible;
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
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"添加失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        DatabaseManager.Instance.DeleteService(id);
                        LoadModels();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("已重置为默认配置。", "重置成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"重置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                
                var modeText = PythonExecutionService.Instance.UseFixedLocation ? "不询问（固定位置）" : "每次询问";
                MessageBox.Show(
                    $"设置已保存！\n\nPython执行模式：{modeText}",
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
        /// 取消
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
