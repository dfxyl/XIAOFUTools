using ArcGIS.Desktop.Framework.Contracts;
using System;

namespace XIAOFUTools.Features.DataManagement.BatchProjectionDefinition
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
