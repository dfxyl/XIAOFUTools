using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.Output.MapSeriesExport
{
    /// <summary>
    /// 驱动制图停靠窗格类
    /// </summary>
    internal class MapSeriesExportDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_MapSeriesExportDockPane";

        protected MapSeriesExportDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new MapSeriesExportDockPaneView();
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
