using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.IntersectSummary
{
    /// <summary>
    /// 交集汇总表停靠窗格
    /// </summary>
    internal class IntersectSummaryDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_IntersectSummaryDockPane";

        protected IntersectSummaryDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new IntersectSummaryDockPaneView();
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
