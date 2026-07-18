using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.ShapefileBuilder
{
    /// <summary>
    /// 属性表建SHP停靠窗格
    /// </summary>
    internal class ShapefileBuilderDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_ShapefileBuilderDockPane";

        protected ShapefileBuilderDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new ShapefileBuilderDockPaneView();
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
