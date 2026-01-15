using System.Windows.Controls;

namespace XIAOFUTools.Tools.SpecialCoordinateTransform
{
    /// <summary>
    /// 特殊坐标转换DockPane视图
    /// </summary>
    public partial class SpecialCoordinateTransformDockPaneView : UserControl
    {
        private SpecialCoordinateTransformDockPaneViewModel _viewModel;

        public SpecialCoordinateTransformDockPaneView()
        {
            InitializeComponent();

            // 创建并设置视图模型
            _viewModel = new SpecialCoordinateTransformDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 确保视图模型已设置
            if (DataContext == null)
            {
                _viewModel = new SpecialCoordinateTransformDockPaneViewModel();
                DataContext = _viewModel;
            }

            // 刷新图层列表
            if (_viewModel != null)
            {
                _viewModel.RefreshLayers();
            }
        }
    }
}
