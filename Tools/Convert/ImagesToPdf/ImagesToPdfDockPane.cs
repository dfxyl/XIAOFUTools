using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.ImagesToPdf
{
    /// <summary>
    /// 图片批量转PDF停靠窗格
    /// </summary>
    internal class ImagesToPdfDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_ImagesToPdfDockPane";

        protected ImagesToPdfDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new ImagesToPdfDockPaneView();
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
