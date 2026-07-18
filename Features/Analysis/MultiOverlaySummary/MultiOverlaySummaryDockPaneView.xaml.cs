using System.Windows.Controls;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary
{
    /// <summary>
    /// 多图层压盖汇总视图
    /// </summary>
    public partial class MultiOverlaySummaryDockPaneView : UserControl
    {
        private MultiOverlaySummaryDockPaneViewModel _viewModel;

        public MultiOverlaySummaryDockPaneView()
        {
            InitializeComponent();
            _viewModel = new MultiOverlaySummaryDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel?.RefreshLayers();
        }
    }
}
