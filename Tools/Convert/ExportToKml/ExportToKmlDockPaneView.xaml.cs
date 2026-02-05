using System.Windows.Controls;

namespace XIAOFUTools.Tools.ExportToKml
{
    /// <summary>
    /// ExportToKmlDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class ExportToKmlDockPaneView : UserControl
    {
        public ExportToKmlDockPaneView()
        {
            InitializeComponent();
            DataContext = new ExportToKmlViewModel();
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is ExportToKmlViewModel viewModel)
            {
                viewModel.RefreshLayers();
            }
        }
    }
}
