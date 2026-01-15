using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.MultiOverlaySummary
{
    /// <summary>
    /// 多图层压盖汇总停靠窗格
    /// </summary>
    internal class MultiOverlaySummaryDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_MultiOverlaySummaryDockPane";

        protected MultiOverlaySummaryDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new MultiOverlaySummaryDockPaneView();
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
