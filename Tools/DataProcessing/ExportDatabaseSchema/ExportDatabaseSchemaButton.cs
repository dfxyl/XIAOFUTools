using ArcGIS.Desktop.Framework.Contracts;
using System;

namespace XIAOFUTools.Tools.DataProcessing.ExportDatabaseSchema
{
    /// <summary>
    /// 输出数据库属性结构表按钮
    /// </summary>
    internal class ExportDatabaseSchemaButton : Button
    {
        /// <summary>
        /// 按钮点击事件
        /// </summary>
        protected override void OnClick()
        {
            try
            {
                // 打开停靠窗格
                ExportDatabaseSchemaDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
