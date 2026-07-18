using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Conversion.PdfToImages
{
    /// <summary>
    /// PDF批量转图片停靠窗格
    /// </summary>
    internal class PdfToImagesDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_PdfToImagesDockPane";

        protected PdfToImagesDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new PdfToImagesDockPaneView();
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
