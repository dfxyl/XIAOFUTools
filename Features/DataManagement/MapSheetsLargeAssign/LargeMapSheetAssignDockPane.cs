using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.MapSheetsLargeAssign
{
    internal class LargeMapSheetAssignDockPane : DockPane
    {
        private const string DockPaneId = "XIAOFUTools_LargeMapSheetAssignDockPane";

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new LargeMapSheetAssignDockPaneView();
        }

        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
            pane?.Activate();
        }
    }
}
