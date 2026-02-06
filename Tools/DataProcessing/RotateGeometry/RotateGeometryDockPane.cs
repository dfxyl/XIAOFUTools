using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.RotateGeometry
{
    /// <summary>
    /// 旋转图形[线/面] 停靠窗格
    /// </summary>
    internal class RotateGeometryDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_RotateGeometryDockPane";

        protected RotateGeometryDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new RotateGeometryDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }

        internal static new bool IsVisible()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            return pane?.IsVisible == true;
        }

        internal static void Close()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane != null && pane.IsVisible)
                pane.Hide();
        }
    }
}
