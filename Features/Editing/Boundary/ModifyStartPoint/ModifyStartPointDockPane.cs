using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Editing.Boundary.ModifyStartPoint
{
    internal class ModifyStartPointDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_ModifyStartPointDockPane";

        protected ModifyStartPointDockPane() { }

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
        private string _heading = "修改起始点";
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
    internal class ModifyStartPointDockPane_ShowButton : Button
    {
        protected override void OnClick()
        {
            ModifyStartPointDockPane.Show();
        }
    }
}
