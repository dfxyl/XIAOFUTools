using System.Windows.Controls;

namespace XIAOFUTools.Features.Conversion.TxtToFeature
{
    /// <summary>
    /// TXT转SHP DockPane视图
    /// </summary>
    public partial class TxtToFeatureDockPaneView : UserControl
    {
        private TxtToFeatureDockPaneViewModel _viewModel;

        public TxtToFeatureDockPaneView()
        {
            InitializeComponent();

            // 创建并设置ViewModel
            _viewModel = new TxtToFeatureDockPaneViewModel();
            DataContext = _viewModel;
        }
    }
}
