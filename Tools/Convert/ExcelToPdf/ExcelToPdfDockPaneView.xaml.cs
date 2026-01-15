using System.Windows.Controls;

namespace XIAOFUTools.Tools.ExcelToPdf
{
    public partial class ExcelToPdfDockPaneView : UserControl
    {
        public ExcelToPdfDockPaneView()
        {
            InitializeComponent();
            DataContext = new ExcelToPdfDockPaneViewModel();
        }
    }
}
