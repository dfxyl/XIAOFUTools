using System.Windows.Controls;

namespace XIAOFUTools.Tools.MapSheetsSmallAssign
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
