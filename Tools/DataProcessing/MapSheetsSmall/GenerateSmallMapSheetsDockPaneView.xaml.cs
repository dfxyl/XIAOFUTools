using System.Windows.Controls;

namespace XIAOFUTools.Tools.MapSheetsSmall
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
