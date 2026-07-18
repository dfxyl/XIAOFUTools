using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Cartography.ExportLayout
{
    /// <summary>
    /// 导出布局停靠窗格类
    /// </summary>
    internal class ExportLayoutDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_ExportLayoutDockPane";

        protected ExportLayoutDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new ExportLayoutDockPaneView();
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
