using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.ExcelToPdf
{
    internal class ExcelToPdfDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_ExcelToPdfDockPane";

        protected ExcelToPdfDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new ExcelToPdfDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
