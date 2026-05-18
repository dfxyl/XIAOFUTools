using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.MapSheetsLargeAssign
{
    internal class LargeMapSheetAssignButton : Button
    {
        protected override void OnClick()
        {
            try
            {

                LargeMapSheetAssignDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
