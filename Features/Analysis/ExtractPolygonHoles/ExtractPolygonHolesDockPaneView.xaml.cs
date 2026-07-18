using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Features.Analysis.ExtractPolygonHoles
{
    public partial class ExtractPolygonHolesDockPaneView : UserControl
    {
        private readonly ExtractPolygonHolesViewModel _viewModel;

        public ExtractPolygonHolesDockPaneView()
        {
            InitializeComponent();
            _viewModel = new ExtractPolygonHolesViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.RefreshLayers();
        }
    }
}
