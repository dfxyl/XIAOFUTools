using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Editing.NodeDistanceCheck
{
    /// <summary>
    /// 节点距离检查工具停靠窗格
    /// </summary>
    internal class NodeDistanceCheckDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_NodeDistanceCheckDockPane";

        protected NodeDistanceCheckDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new NodeDistanceCheckDockPaneView();
        }

        /// <summary>
        /// 显示停靠窗格
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}