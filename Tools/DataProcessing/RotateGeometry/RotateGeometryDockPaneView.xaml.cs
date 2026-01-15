using System;
using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Tools.RotateGeometry
{
    /// <summary>
    /// RotateGeometryDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class RotateGeometryDockPaneView : UserControl
    {
        private RotateGeometryDockPaneViewModel _viewModel;

        public RotateGeometryDockPaneView()
        {
            InitializeComponent();
            _viewModel = new RotateGeometryDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.RefreshLayers();
        }
    }
}
