using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.MapSheetsSmallAssign
{
    internal class SmallMapSheetAssignDockPane : DockPane
    {
        private const string DockPaneId = "XIAOFUTools_SmallMapSheetAssignDockPane";

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new SmallMapSheetAssignDockPaneView();
        }

        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
            pane?.Activate();
        }
    }
}
