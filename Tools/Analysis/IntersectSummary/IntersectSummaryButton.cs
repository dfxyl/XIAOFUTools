using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.IntersectSummary
{
    /// <summary>
    /// 交集汇总表按钮
    /// </summary>
    internal class IntersectSummaryButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开交集汇总表停靠窗格
                IntersectSummaryDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
