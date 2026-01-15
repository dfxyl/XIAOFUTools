using ArcGIS.Desktop.Framework.Contracts;
using System;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.DataProcessing.DatabaseBuilder
{
    /// <summary>
    /// 属性表建库工具按钮
    /// </summary>
    internal class DatabaseBuilderButton : Button
    {
        /// <summary>
        /// 按钮点击事件
        /// </summary>
        protected override void OnClick()
        {
            try
            {
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("属性表建库"))
                {
                    return;
                }

                // 打开属性表建库停靠窗格
                DatabaseBuilderDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
