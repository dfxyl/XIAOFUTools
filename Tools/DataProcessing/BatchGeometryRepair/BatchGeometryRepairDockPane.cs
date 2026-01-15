using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.BatchGeometryRepair
{
    /// <summary>
    /// 批量修复几何停靠窗格
    /// </summary>
    internal class BatchGeometryRepairDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_BatchGeometryRepairDockPane";

        protected BatchGeometryRepairDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new BatchGeometryRepairDockPaneView();
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
