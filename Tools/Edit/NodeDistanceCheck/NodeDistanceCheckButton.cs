using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.NodeDistanceCheck
{
    /// <summary>
    /// 节点距离检查工具按钮
    /// </summary>
    internal class NodeDistanceCheckButton : Button
    {
        protected override void OnClick()
        {
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("节点距离检查工具"))
                return;
            // 打开停靠窗格
            NodeDistanceCheckDockPane.Show();
        }
    }
}