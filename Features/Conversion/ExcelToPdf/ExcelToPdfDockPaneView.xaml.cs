using System.Windows.Controls;

namespace XIAOFUTools.Features.Conversion.ExcelToPdf
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
