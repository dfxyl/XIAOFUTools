using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.Authorization
{
    /// <summary>
    /// 授权管理按钮
    /// </summary>
    internal class AuthorizationButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开授权管理停靠窗格
                AuthorizationDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开授权管理窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
