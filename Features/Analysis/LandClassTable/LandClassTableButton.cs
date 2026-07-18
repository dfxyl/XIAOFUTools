using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    internal class LandClassTableButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                LandClassTableDockPane.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
