using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable
{
    internal class LayoutCoordinateTableButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                LayoutCoordinateTableDockPane.Show();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"打开布局生成坐标表停靠窗格时发生错误：{ex.Message}", "错误");
            }
        }
    }
}
