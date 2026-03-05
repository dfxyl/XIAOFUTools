using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal class MdbBatchToGdbDockPane : DockPane
    {
        private const string DockPaneId = "XIAOFUTools_MdbBatchToGdbDockPane";

        protected MdbBatchToGdbDockPane()
        {
        }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new MdbBatchToGdbDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
            pane?.Activate();
        }
    }
}
