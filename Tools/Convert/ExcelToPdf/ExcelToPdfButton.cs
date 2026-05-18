using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.ExcelToPdf
{
    internal class ExcelToPdfButton : Button
    {
        protected override void OnClick()
        {
            try
            {

                ExcelToPdfDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
