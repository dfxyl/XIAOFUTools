using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using System;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.ExportLayout
{
    /// <summary>
    /// 导出布局按钮类
    /// </summary>
    internal class ExportLayoutButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("导出布局工具"))
                {
                    return;
                }

                // 打开导出布局停靠窗格
                ExportLayoutDockPane.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
