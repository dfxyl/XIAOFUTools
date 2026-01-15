using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.GapCheck
{
    /// <summary>
    /// 缝隙检查工具停靠窗格
    /// </summary>
    internal class GapCheckDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_GapCheckDockPane";

        protected GapCheckDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new GapCheckDockPaneView();
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