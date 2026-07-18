using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.BatchProjectionDefinition
{
    /// <summary>
    /// 批量定义投影停靠窗格
    /// </summary>
    internal class BatchProjectionDefinitionDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_BatchProjectionDefinitionDockPane";

        protected BatchProjectionDefinitionDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new BatchProjectionDefinitionDockPaneView();
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
