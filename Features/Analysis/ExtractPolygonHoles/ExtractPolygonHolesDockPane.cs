using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Analysis.ExtractPolygonHoles
{
    internal class ExtractPolygonHolesDockPane : DockPane
    {
        private const string DockPaneId = "XIAOFUTools_ExtractPolygonHolesDockPane";

        protected ExtractPolygonHolesDockPane()
        {
        }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new ExtractPolygonHolesDockPaneView();
        }

        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
            pane?.Activate();
        }
    }
}
