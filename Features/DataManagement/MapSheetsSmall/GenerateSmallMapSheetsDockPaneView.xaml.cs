using System.Windows.Controls;

namespace XIAOFUTools.Features.DataManagement.MapSheetsSmall
{
    public partial class GenerateSmallMapSheetsDockPaneView : UserControl
    {
        public GenerateSmallMapSheetsDockPaneView()
        {
            InitializeComponent();
            this.DataContext = new GenerateSmallMapSheetsDockPaneViewModel();
        }
    }
}
