using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

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
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("多图层压盖汇总工具"))
                {
                    return;
                }

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
