using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.MapSheetsSmall
{
    internal class GenerateSmallMapSheetsDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_GenerateSmallMapSheetsDockPane";

        protected GenerateSmallMapSheetsDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new GenerateSmallMapSheetsDockPaneView();
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        public static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            pane?.Activate();
        }
    }
}
