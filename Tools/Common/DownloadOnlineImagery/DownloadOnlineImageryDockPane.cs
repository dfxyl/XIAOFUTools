using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.DownloadOnlineImagery
{
    /// <summary>
    /// 下载在线影像停靠窗格
    /// </summary>
    internal class DownloadOnlineImageryDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_DownloadOnlineImageryDockPane";

        protected DownloadOnlineImageryDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new DownloadOnlineImageryDockPaneView();
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
