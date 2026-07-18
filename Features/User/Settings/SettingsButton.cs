using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.User.Settings
{
    /// <summary>
    /// 设置按钮
    /// </summary>
    internal class SettingsButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开设置停靠窗格
                SettingsDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开设置窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
