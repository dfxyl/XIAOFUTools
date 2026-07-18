using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.RotateGeometry
{
    internal class RotateGeometryButton : Button
    {
        protected override void OnClick()
        {
            try
            {
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
