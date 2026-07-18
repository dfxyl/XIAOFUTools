using System.Windows.Controls;

namespace XIAOFUTools.Features.General.InternetTileDownload
{
    public partial class InternetTileDownloadDockPaneView : UserControl
    {
        public InternetTileDownloadDockPaneView()
        {
            InitializeComponent();
            DataContext = new InternetTileDownloadViewModel();
        }

        private async void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is InternetTileDownloadViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }
    }
}
