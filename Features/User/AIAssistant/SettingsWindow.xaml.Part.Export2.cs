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
    }
}
