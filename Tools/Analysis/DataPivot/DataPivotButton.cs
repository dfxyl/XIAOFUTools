using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.DataPivot
{
    internal class DataPivotButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                DataPivotDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
