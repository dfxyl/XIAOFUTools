using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.Edit.Boundary.LayoutCoordinateTable
{
    internal class LayoutCoordinateTableButton : Button
    {
        protected override void OnClick()
        {
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("布局生成坐标表功能"))
                return;
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
