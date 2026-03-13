using System.Windows.Controls;

namespace XIAOFUTools.Tools.MapSheetsLargeAssign
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
