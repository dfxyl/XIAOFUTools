using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.General.QuickAddData
{
    internal class QuickAddDataDockPane : DockPane
    {
        private const string DockPaneId = "XIAOFUTools_QuickAddDataDockPane";

        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new QuickAddDataDockPaneView();
        }

        internal static void Show()
        {
            FrameworkApplication.DockPaneManager.Find(DockPaneId)?.Activate();
        }
    }
}
