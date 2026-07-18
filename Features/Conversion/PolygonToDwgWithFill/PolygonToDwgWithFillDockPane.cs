using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill
{
    /// <summary>
    /// 面要素图层转DWG[带色块填充] 停靠窗格
    /// </summary>
    internal class PolygonToDwgWithFillDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_PolygonToDwgWithFillDockPane";

        protected PolygonToDwgWithFillDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new PolygonToDwgWithFillDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
