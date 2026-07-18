using System.Windows.Controls;

namespace XIAOFUTools.Features.General.DownloadOnlineImagery
{
    /// <summary>
    /// 下载在线影像停靠窗格视图
    /// </summary>
    public partial class DownloadOnlineImageryDockPaneView : UserControl
    {
        private DownloadOnlineImageryViewModel _viewModel;

        public DownloadOnlineImageryDockPaneView()
        {
            InitializeComponent();

            // 创建并设置ViewModel
            _viewModel = new DownloadOnlineImageryViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// 用户控件加载事件
        /// </summary>
        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 在控件加载后可以执行一些初始化操作
            // 例如刷新图层列表等
        }
    }
}
