using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.RotateGeometry
{
    internal class RotateGeometryButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 授权检查
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("旋转图形[线/面]"))
                    return;

                // 打开停靠窗格
                RotateGeometryDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
