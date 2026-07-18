using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable
{
    internal class LayoutCoordinateTableDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_LayoutCoordinateTableDockPane";

        protected LayoutCoordinateTableDockPane() { }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            pane.Activate();
        }

        /// <summary>
        /// Text shown near the top of the DockPane.
        /// </summary>
        private string _heading = "布局生成坐标表[要素图层版]";
        public string Heading
        {
            get { return _heading; }
            set
            {
                SetProperty(ref _heading, value, () => Heading);
            }
        }
    }

    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class LayoutCoordinateTableDockPane_ShowButton : Button
    {
        protected override void OnClick()
        {
            LayoutCoordinateTableDockPane.Show();
        }
    }
}
