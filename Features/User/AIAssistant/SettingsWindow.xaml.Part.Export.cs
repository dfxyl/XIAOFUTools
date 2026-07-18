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
    public partial class SettingsWindow
    {

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
            infoPanel.Children.Add(new TextBlock { Text = $"上下文: {FormatTokenCount(model.ContextWindowTokens)}，输出上限: {model.MaxTokens}", Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9ca3af")), FontSize = 10, Margin = new Thickness(0, 2, 0, 0) });
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


        private static string FormatTokenCount(int tokenCount)
        {
            if (tokenCount <= 0)
            {
                return "未设置";
            }

            if (tokenCount >= 1000000)
            {
                return $"{tokenCount / 1000000.0:0.#}M";
            }

            if (tokenCount >= 1000)
            {
                return $"{tokenCount / 1000.0:0.#}K";
            }

            return tokenCount.ToString();
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
    }
}
