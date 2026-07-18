using System.Windows.Controls;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    public partial class HistoricalImageryDownloadDockPaneView : UserControl
    {
        public HistoricalImageryDownloadDockPaneView()
        {
            InitializeComponent();
            DataContext = new HistoricalImageryDownloadViewModel();
        }

        private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is HistoricalImageryDownloadViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }
    }
}
