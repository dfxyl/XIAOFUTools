using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.User.Custom.DevZoneCheck
{
    /// <summary>
    /// 开发区整合优化核查停靠窗格
    /// </summary>
    internal class DevZoneCheckDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_DevZoneCheckDockPane";

        protected DevZoneCheckDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new DevZoneCheckDockPaneView();
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
