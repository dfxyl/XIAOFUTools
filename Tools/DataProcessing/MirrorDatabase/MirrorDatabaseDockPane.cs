using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.DataProcessing.MirrorDatabase
{
    /// <summary>
    /// 镜像数据库停靠窗格
    /// </summary>
    internal class MirrorDatabaseDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_MirrorDatabaseDockPane";

        protected MirrorDatabaseDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new MirrorDatabaseDockPaneView();
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
