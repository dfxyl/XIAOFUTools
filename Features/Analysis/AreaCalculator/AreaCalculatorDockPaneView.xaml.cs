using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Features.Analysis.AreaCalculator
{
    /// <summary>
    /// AreaCalculatorDockPaneView.xaml interaction logic.
    /// </summary>
    public partial class AreaCalculatorDockPaneView : UserControl
    {
        private readonly AreaCalculatorDockPaneViewModel _viewModel;

        public AreaCalculatorDockPaneView()
        {
            InitializeComponent();
            _viewModel = new AreaCalculatorDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.RefreshLayers();
        }

        internal void ApplyContextOptions(AreaCalculatorContextOptions contextOptions)
        {
            _viewModel?.ApplyContextOptions(contextOptions);
        }

        public void ApplyPreferredLayerName(string layerName)
        {
            _viewModel?.SetPreferredLayerName(layerName);
        }
    }
}
