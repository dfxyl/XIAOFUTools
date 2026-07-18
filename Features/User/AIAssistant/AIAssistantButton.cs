using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.User.AIAssistant
{
    /// <summary>
    /// AI助手按钮
    /// </summary>
    internal class AIAssistantButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开AI助手停靠窗格
                AIAssistantDockPaneViewModel.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"打开AI助手窗格失败: {ex.Message}");
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"打开AI助手窗格时出错: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
