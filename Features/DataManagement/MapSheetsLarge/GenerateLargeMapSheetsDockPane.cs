using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.MapSheetsLarge
{
    internal class GenerateLargeMapSheetsDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_GenerateLargeMapSheetsDockPane";

        protected GenerateLargeMapSheetsDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new GenerateLargeMapSheetsDockPaneView();
        }

        public static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
