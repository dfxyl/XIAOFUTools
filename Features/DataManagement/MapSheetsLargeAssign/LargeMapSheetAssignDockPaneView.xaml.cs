using System.Windows.Controls;

namespace XIAOFUTools.Features.DataManagement.MapSheetsLargeAssign
{
    public partial class LargeMapSheetAssignDockPaneView : UserControl
    {
        public LargeMapSheetAssignDockPaneView()
        {
            InitializeComponent();
            DataContext = new LargeMapSheetAssignViewModel();
        }
    }
}
