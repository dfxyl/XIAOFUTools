using ArcGIS.Desktop.Framework.Contracts;
using System;

namespace XIAOFUTools.Tools.DataProcessing.ExportShpFieldTable
{
    /// <summary>
    /// SHP输字段表按钮
    /// </summary>
    internal class ExportShpFieldTableButton : Button
    {
        protected override void OnClick()
        {
            try
            {

                ExportShpFieldTableDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
