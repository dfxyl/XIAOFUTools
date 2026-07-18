using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Cartography.LayoutTextReplace
{
    /// <summary>
    /// 布局元素查找替换停靠窗格类
    /// </summary>
    internal class LayoutTextReplaceDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_LayoutTextReplaceDockPane";

        protected LayoutTextReplaceDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new LayoutTextReplaceDockPaneView();
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
