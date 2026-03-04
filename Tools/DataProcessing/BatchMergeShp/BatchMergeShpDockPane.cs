using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.DataProcessing.BatchMergeShp
{
    /// <summary>
    /// 批量合并 SHP 停靠窗格。
    /// </summary>
    internal class BatchMergeShpDockPane : DockPane
    {
        private const string _dockPaneId = "XIAOFUTools_BatchMergeShpDockPane";

        protected BatchMergeShpDockPane()
        {
        }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new BatchMergeShpDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneId);
            pane?.Activate();
        }
    }
}
