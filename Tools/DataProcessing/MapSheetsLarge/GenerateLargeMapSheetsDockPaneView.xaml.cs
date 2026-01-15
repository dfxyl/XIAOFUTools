using System.Windows.Controls;

namespace XIAOFUTools.Tools.MapSheetsLarge
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
