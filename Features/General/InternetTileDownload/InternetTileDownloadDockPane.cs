using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.General.InternetTileDownload
{
    internal class InternetTileDownloadDockPane : DockPane
    {
        private const string DockPaneId = "XIAOFUTools_InternetTileDownloadDockPane";

        protected InternetTileDownloadDockPane()
        {
        }

        protected override System.Windows.Controls.Control OnCreateContent()
            => new InternetTileDownloadDockPaneView();

        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
            pane?.Activate();
        }
    }
}
