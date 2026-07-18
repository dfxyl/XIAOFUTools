using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Conversion.SpecialCoordinateTransform
{
    /// <summary>
    /// 特殊坐标转换停靠窗格
    /// </summary>
    internal class SpecialCoordinateTransformDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_SpecialCoordinateTransformDockPane";

        protected SpecialCoordinateTransformDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new SpecialCoordinateTransformDockPaneView();
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
