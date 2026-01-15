using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.GapCheck
{
    /// <summary>
    /// 缝隙检查工具按钮
    /// </summary>
    internal class GapCheckButton : Button
    {
        protected override void OnClick()
        {
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("缝隙检查工具"))
                return;
            // 打开停靠窗格
            GapCheckDockPane.Show();
        }
    }
}