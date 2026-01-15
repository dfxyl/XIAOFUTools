using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.WordToPdf
{
    internal class WordToPdfDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_WordToPdfDockPane";

        protected WordToPdfDockPane() { }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new WordToPdfDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
