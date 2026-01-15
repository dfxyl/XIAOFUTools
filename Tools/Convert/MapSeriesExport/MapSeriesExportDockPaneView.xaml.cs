using System.Windows.Controls;
using System.Windows.Input;

namespace XIAOFUTools.Tools.Output.MapSeriesExport
{
    /// <summary>
    /// 驱动制图停靠窗格视图
    /// </summary>
    public partial class MapSeriesExportDockPaneView : UserControl
    {
        public MapSeriesExportDockPaneView()
        {
            InitializeComponent();
            DataContext = new MapSeriesExportViewModel();
        }

        /// <summary>
        /// 列表项双击事件 - 切换到该页面
        /// </summary>
        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item && item.DataContext is MapSeriesPageItem page)
            {
                var vm = DataContext as MapSeriesExportViewModel;
                vm?.NavigateToPageCommand?.Execute(page);
            }
        }
    }
}
