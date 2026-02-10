using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.DocumentBatchReplace
{
    internal class DocumentBatchReplaceDockPane : DockPane
    {
        private const string DockPaneId = "XIAOFUTools_DocumentBatchReplaceDockPane";

        protected DocumentBatchReplaceDockPane()
        {
        }

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new DocumentBatchReplaceDockPaneView();
        }

        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(DockPaneId);
            pane?.Activate();
        }
    }
}
