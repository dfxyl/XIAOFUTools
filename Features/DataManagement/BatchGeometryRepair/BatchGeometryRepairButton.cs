using ArcGIS.Desktop.Framework.Contracts;
using System;

namespace XIAOFUTools.Features.DataManagement.BatchGeometryRepair
{
    /// <summary>
    /// 批量修复几何按钮
    /// </summary>
    internal class BatchGeometryRepairButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开批量修复几何停靠窗格
                BatchGeometryRepairDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
