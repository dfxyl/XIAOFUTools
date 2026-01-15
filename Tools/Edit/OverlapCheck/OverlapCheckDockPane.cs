using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.OverlapCheck
{
    /// <summary>
    /// 图形重叠检查工具停靠窗格
    /// </summary>
    internal class OverlapCheckDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_OverlapCheckDockPane";

        protected OverlapCheckDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new OverlapCheckDockPaneView();
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
