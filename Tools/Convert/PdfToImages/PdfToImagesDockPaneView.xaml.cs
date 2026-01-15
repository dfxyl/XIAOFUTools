using System.Windows.Controls;

namespace XIAOFUTools.Tools.PdfToImages
{
    /// <summary>
    /// PdfToImagesDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class PdfToImagesDockPaneView : UserControl
    {
        private PdfToImagesDockPaneViewModel _viewModel;

        public PdfToImagesDockPaneView()
        {
            InitializeComponent();

            // 创建并设置ViewModel
            _viewModel = new PdfToImagesDockPaneViewModel();
            DataContext = _viewModel;
        }
    }
}
