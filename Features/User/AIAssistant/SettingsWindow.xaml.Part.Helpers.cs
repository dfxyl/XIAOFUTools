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

        private static string NormalizeToolGroup(string groupName)
        {
            return string.IsNullOrWhiteSpace(groupName) ? "其他" : groupName.Trim();
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
                        ContextWindowTokens = dialog.ContextWindowTokens,
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
    }
}
