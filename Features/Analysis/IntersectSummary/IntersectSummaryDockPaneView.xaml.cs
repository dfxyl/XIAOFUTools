using System.Windows.Controls;

namespace XIAOFUTools.Features.Analysis.IntersectSummary
{
    /// <summary>
    /// 交集汇总表视图
    /// </summary>
    public partial class IntersectSummaryDockPaneView : UserControl
    {
        private IntersectSummaryDockPaneViewModel _viewModel;

        public IntersectSummaryDockPaneView()
        {
            InitializeComponent();
            _viewModel = new IntersectSummaryDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 刷新图层列表
            _viewModel?.RefreshLayers();
        }
    }
}
