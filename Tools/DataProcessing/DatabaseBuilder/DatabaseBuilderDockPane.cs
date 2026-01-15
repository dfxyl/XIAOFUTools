using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.DataProcessing.DatabaseBuilder
{
    /// <summary>
    /// 属性表建库停靠窗格
    /// </summary>
    internal class DatabaseBuilderDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_DatabaseBuilderDockPane";

        protected DatabaseBuilderDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new DatabaseBuilderDockPaneView();
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
