using System.Windows.Controls;

namespace XIAOFUTools.Features.Conversion.WordToPdf
{
    public partial class WordToPdfDockPaneView : UserControl
    {
        public WordToPdfDockPaneView()
        {
            InitializeComponent();
            DataContext = new WordToPdfDockPaneViewModel();
        }
    }
}
