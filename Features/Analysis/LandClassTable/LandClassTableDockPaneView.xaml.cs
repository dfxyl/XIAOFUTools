using System.Windows.Controls;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    public partial class LandClassTableDockPaneView : UserControl
    {
        private readonly LandClassTableDockPaneViewModel _viewModel;

        public LandClassTableDockPaneView()
        {
            InitializeComponent();
            _viewModel = new LandClassTableDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel.RefreshLayers();
        }
    }
}
