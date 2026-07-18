using ArcGIS.Desktop.Framework.Contracts;
using System;

namespace XIAOFUTools.Features.DataManagement.DatabaseBuilder
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
