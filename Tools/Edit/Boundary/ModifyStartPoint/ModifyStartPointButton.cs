using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.Edit.Boundary.ModifyStartPoint
{
    internal class ModifyStartPointButton : Button
    {
        protected override void OnClick()
        {
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("修改起始点功能"))
                return;
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
