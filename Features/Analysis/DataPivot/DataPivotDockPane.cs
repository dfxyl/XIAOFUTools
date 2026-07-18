using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Analysis.DataPivot
{
    internal class DataPivotDockPane : DockPane
    {
        private const string DockPaneId = "XIAOFUTools_DataPivotDockPane";

        protected DataPivotDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new DataPivotDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
            pane?.Activate();
        }
    }
}
