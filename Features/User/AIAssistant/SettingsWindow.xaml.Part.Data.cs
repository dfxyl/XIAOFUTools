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
                    ContextWindowTokens = s.ContextWindowTokens,
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
    }
}
