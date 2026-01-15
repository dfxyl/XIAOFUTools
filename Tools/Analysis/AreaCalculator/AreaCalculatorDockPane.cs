using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.AreaCalculator
{
    /// <summary>
    /// 计算面积停靠窗格
    /// </summary>
    internal class AreaCalculatorDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_AreaCalculatorDockPane";

        protected AreaCalculatorDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new AreaCalculatorDockPaneView();
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
