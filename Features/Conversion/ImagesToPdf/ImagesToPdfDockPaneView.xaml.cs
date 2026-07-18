using System.Windows.Controls;

namespace XIAOFUTools.Features.Conversion.ImagesToPdf
{
    /// <summary>
    /// 图片批量转PDF停靠窗格视图
    /// </summary>
    public partial class ImagesToPdfDockPaneView : UserControl
    {
        private ImagesToPdfDockPaneViewModel _viewModel;

        public ImagesToPdfDockPaneView()
        {
            InitializeComponent();

            // 创建并设置ViewModel
            _viewModel = new ImagesToPdfDockPaneViewModel();
            DataContext = _viewModel;
        }
    }
}
