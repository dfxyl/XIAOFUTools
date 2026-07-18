using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Analysis.ExtractPolygonHoles
{
    internal class ExtractPolygonHolesButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                ExtractPolygonHolesDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
