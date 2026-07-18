using System.Windows.Controls;

namespace XIAOFUTools.Features.Editing.Boundary.MapBoundaryPointLineGenerator
{
    /// <summary>
    /// 地图生成界址点线 View
    /// </summary>
    public partial class MapBoundaryPointLineGeneratorDockPaneView : UserControl
    {
        private MapBoundaryPointLineGeneratorDockPaneViewModel _viewModel;

        public MapBoundaryPointLineGeneratorDockPaneView()
        {
            InitializeComponent();
            _viewModel = new MapBoundaryPointLineGeneratorDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 只刷新数据，不重新创建 ViewModel，避免累积事件订阅
            _viewModel?.RefreshLayers();
        }
    }
}
