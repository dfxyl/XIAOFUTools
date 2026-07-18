using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    /// <summary>
    /// 要素类转TXT停靠窗格
    /// </summary>
    internal class FeatureToTxtDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_FeatureToTxtDockPane";

        protected FeatureToTxtDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new FeatureToTxtDockPaneView();
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
