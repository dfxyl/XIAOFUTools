using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Editing.Boundary.BoundaryPointLineGenerator
{
    /// <summary>
    /// 界址点线生成 DockPane
    /// </summary>
    internal class BoundaryPointLineGeneratorDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_BoundaryPointLineGeneratorDockPane";

        protected BoundaryPointLineGeneratorDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new BoundaryPointLineGeneratorDockPaneView();
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
    internal class BoundaryPointLineGeneratorButton : Button
    {
        protected override void OnClick()
        {
            BoundaryPointLineGeneratorDockPane.Show();
        }
    }
}
