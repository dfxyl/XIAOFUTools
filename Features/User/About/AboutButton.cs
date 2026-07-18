using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.User.About
{
    /// <summary>
    /// 关于按钮
    /// </summary>
    internal class AboutButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开关于对话框
                AboutDialog dialog = new AboutDialog();
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开关于对话框时出错: {ex.Message}", "错误");
            }
        }
    }
}
