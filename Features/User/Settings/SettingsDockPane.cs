using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.User.Settings
{
    /// <summary>
    /// 设置停靠窗格
    /// </summary>
    internal class SettingsDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_SettingsDockPane";

        protected SettingsDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new SettingsDockPaneView();
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
