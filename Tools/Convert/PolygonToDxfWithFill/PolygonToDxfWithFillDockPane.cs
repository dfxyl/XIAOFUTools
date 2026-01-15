using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.PolygonToDxfWithFill
{
    /// <summary>
    /// 面要素图层转DXF[带填充] 停靠窗格
    /// </summary>
    internal class PolygonToDxfWithFillDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_PolygonToDxfWithFillDockPane";

        protected PolygonToDxfWithFillDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new PolygonToDxfWithFillDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
