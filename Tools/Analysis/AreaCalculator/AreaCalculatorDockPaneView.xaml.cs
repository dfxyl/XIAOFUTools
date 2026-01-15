using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Tools.AreaCalculator
{
    /// <summary>
    /// AreaCalculatorDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class AreaCalculatorDockPaneView : UserControl
    {
        private AreaCalculatorDockPaneViewModel _viewModel;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public AreaCalculatorDockPaneView()
        {
            InitializeComponent();
            _viewModel = new AreaCalculatorDockPaneViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// 当控件加载时刷新图层列表
        /// </summary>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.RefreshLayers();
        }
    }
}
