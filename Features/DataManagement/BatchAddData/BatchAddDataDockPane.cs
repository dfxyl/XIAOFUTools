using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.BatchAddData
{
    /// <summary>
    /// 批量添加数据停靠窗格
    /// </summary>
    internal class BatchAddDataDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_BatchAddDataDockPane";

        protected BatchAddDataDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new BatchAddDataDockPaneView();
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
