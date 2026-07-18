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
                            ContextWindowTokens = dialog.ContextWindowTokens,
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
        /// 清空模型配置
        /// </summary>
        private void BtnResetModels_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要清空所有模型配置吗？\n\n新版不再内置默认API和模型，清空后需要手动添加模型。",
                "确认清空",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    DatabaseManager.Instance.ResetToDefaultServices();
                    LoadModels();
                    ShowToast("已清空模型配置");
                }
                catch (Exception ex)
                {
                    ShowToast($"清空失败: {ex.Message}", false);
                }
            }
        }

    }
}
