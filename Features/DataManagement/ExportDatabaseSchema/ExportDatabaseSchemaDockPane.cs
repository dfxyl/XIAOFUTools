using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.ExportDatabaseSchema
{
    /// <summary>
    /// 输出数据库属性结构表停靠窗格
    /// </summary>
    internal class ExportDatabaseSchemaDockPane : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_ExportDatabaseSchemaDockPane";

        protected ExportDatabaseSchemaDockPane() { }

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new ExportDatabaseSchemaDockPaneView();
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
