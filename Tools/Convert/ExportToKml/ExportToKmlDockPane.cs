using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.ExportToKml
{
    /// <summary>
    /// 要素图层分组导出KML/KMZ停靠窗格
    /// </summary>
    internal class ExportToKmlDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_ExportToKmlDockPane";

        protected ExportToKmlDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new ExportToKmlDockPaneView();
        }

        /// <summary>
        /// 显示停靠窗格
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
