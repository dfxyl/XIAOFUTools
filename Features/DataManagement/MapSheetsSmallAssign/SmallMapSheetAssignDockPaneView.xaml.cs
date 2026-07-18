using System.Windows.Controls;

namespace XIAOFUTools.Features.DataManagement.MapSheetsSmallAssign
{
    public partial class SmallMapSheetAssignDockPaneView : UserControl
    {
        public SmallMapSheetAssignDockPaneView()
        {
            InitializeComponent();
            DataContext = new SmallMapSheetAssignViewModel();
        }
    }
}
