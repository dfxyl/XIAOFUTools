using System.Windows.Controls;

namespace XIAOFUTools.Features.DataManagement.MapSheetsLarge
{
    public partial class GenerateLargeMapSheetsDockPaneView : UserControl
    {
        public GenerateLargeMapSheetsDockPaneView()
        {
            InitializeComponent();
            this.DataContext = new GenerateLargeMapSheetsDockPaneViewModel();
        }
    }
}
