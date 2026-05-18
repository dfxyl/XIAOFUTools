using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.NodeDistanceCheck
{
    /// <summary>
    /// 节点距离检查工具按钮
    /// </summary>
    internal class NodeDistanceCheckButton : Button
    {
        protected override void OnClick()
        {
            // 打开停靠窗格
            NodeDistanceCheckDockPane.Show();
        }
    }
}