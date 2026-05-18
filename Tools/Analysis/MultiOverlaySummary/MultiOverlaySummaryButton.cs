using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.MultiOverlaySummary
{
    /// <summary>
    /// 多图层压盖汇总按钮
    /// </summary>
    internal class MultiOverlaySummaryButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开多图层压盖汇总停靠窗格
                MultiOverlaySummaryDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
