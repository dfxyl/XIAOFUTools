using System.Windows.Controls;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    internal class LandClassTableDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_LandClassTableDockPane";

        protected LandClassTableDockPane()
        {
        }

        protected override Control OnCreateContent()
        {
            return new LandClassTableDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
