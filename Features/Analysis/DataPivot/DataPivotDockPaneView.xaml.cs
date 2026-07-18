using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Features.Analysis.DataPivot
{
    public partial class DataPivotDockPaneView : UserControl
    {
        private readonly DataPivotDockPaneViewModel _viewModel;

        public DataPivotDockPaneView()
        {
            InitializeComponent();
            _viewModel = new DataPivotDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.ResetOutputToProjectDefaultGdb();
            _viewModel?.RefreshDatasets();
        }
    }
}
