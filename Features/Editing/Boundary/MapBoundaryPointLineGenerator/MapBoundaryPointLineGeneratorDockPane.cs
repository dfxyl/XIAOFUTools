using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Editing.Boundary.MapBoundaryPointLineGenerator
{
    /// <summary>
    /// 地图生成界址点线 DockPane
    /// </summary>
    internal class MapBoundaryPointLineGeneratorDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_MapBoundaryPointLineGeneratorDockPane";

        protected MapBoundaryPointLineGeneratorDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new MapBoundaryPointLineGeneratorDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }

    /// <summary>
    /// 显示 DockPane 的按钮
    /// </summary>
    internal class MapBoundaryPointLineGeneratorButton : Button
    {
        protected override void OnClick()
        {
            MapBoundaryPointLineGeneratorDockPane.Show();
        }
    }
}
