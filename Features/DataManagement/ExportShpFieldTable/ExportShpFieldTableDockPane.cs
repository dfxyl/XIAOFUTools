using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.ExportShpFieldTable
{
    /// <summary>
    /// SHP输字段表停靠窗格
    /// </summary>
    internal class ExportShpFieldTableDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_ExportShpFieldTableDockPane";

        protected ExportShpFieldTableDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new ExportShpFieldTableDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
