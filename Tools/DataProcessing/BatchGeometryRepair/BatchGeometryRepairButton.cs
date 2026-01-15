using ArcGIS.Desktop.Framework.Contracts;
using System;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.BatchGeometryRepair
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
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("批量修复几何工具"))
                {
                    return;
                }

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
