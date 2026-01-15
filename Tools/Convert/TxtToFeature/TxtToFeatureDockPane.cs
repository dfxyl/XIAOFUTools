using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.TxtToFeature
{
    /// <summary>
    /// TXT转SHP停靠窗格
    /// </summary>
    internal class TxtToFeatureDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_TxtToFeatureDockPane";

        protected TxtToFeatureDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new TxtToFeatureDockPaneView();
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
