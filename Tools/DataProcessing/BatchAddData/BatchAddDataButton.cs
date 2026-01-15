using ArcGIS.Desktop.Framework.Contracts;
using System;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.BatchAddData
{
    /// <summary>
    /// 批量添加数据工具按钮
    /// </summary>
    internal class BatchAddDataButton : Button
    {
        /// <summary>
        /// 按钮点击事件
        /// </summary>
        protected override void OnClick()
        {
            try
            {
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("批量添加数据工具"))
                {
                    return;
                }

                // 打开批量添加数据停靠窗格
                BatchAddDataDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
