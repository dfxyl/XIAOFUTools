using System;
using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Tools.ProtocolLineExtract
{
    /// <summary>
    /// ProtocolLineExtractDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class ProtocolLineExtractDockPaneView : UserControl
    {
        private ProtocolLineExtractViewModel _viewModel;

        public ProtocolLineExtractDockPaneView()
        {
            InitializeComponent();
            _viewModel = new ProtocolLineExtractViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.RefreshLayers();
        }
    }
}
