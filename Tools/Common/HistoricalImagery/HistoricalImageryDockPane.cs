using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.HistoricalImagery
{
    /// <summary>
    /// 历史影像停靠窗格
    /// </summary>
    internal class HistoricalImageryDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_HistoricalImageryDockPane";

        protected HistoricalImageryDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new HistoricalImageryDockPaneView();
        }

        /// <summary>
        /// 显示停靠窗格
        /// </summary>
        internal static void Show()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }

        /// <summary>
        /// 关闭停靠窗格
        /// </summary>
        internal static void Close()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane != null && pane.IsVisible)
            {
                pane.Hide();
            }
        }

        /// <summary>
        /// 检查停靠窗格是否可见
        /// </summary>
        internal static new bool IsVisible()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            return pane?.IsVisible == true;
        }
    }
}
