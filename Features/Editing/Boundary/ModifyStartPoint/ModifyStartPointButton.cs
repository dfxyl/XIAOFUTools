using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace XIAOFUTools.Features.Editing.Boundary.ModifyStartPoint
{
    internal class ModifyStartPointButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                ModifyStartPointDockPane.Show();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"打开修改起始点停靠窗格时发生错误：{ex.Message}", "错误");
            }
        }
    }
}
