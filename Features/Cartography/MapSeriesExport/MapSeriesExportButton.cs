using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using System;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    /// <summary>
    /// 驱动制图按钮类
    /// </summary>
    internal class MapSeriesExportButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开驱动制图停靠窗格
                MapSeriesExportDockPane.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
