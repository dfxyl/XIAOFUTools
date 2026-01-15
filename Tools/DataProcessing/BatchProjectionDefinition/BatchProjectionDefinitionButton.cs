using ArcGIS.Desktop.Framework.Contracts;
using System;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.BatchProjectionDefinition
{
    /// <summary>
    /// 批量定义投影工具按钮
    /// </summary>
    internal class BatchProjectionDefinitionButton : Button
    {
        /// <summary>
        /// 按钮点击事件
        /// </summary>
        protected override void OnClick()
        {
            try
            {
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("批量定义投影工具"))
                {
                    return;
                }

                // 打开停靠窗格
                BatchProjectionDefinitionDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
