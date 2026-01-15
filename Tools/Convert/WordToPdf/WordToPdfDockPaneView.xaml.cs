using System.Windows.Controls;

namespace XIAOFUTools.Tools.WordToPdf
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
