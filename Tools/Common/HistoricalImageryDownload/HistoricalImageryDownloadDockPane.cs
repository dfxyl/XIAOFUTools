using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.HistoricalImageryDownload
{
    internal class HistoricalImageryDownloadDockPane : DockPane
    {
        private const string DockPaneId = "XIAOFUTools_HistoricalImageryDownloadDockPane";

        protected HistoricalImageryDownloadDockPane()
        {
        }

        protected override System.Windows.Controls.Control OnCreateContent()
            => new HistoricalImageryDownloadDockPaneView();

        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
            pane?.Activate();
        }
    }
}
