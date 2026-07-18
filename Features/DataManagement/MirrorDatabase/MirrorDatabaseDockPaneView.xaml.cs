using System.Windows.Controls;

namespace XIAOFUTools.Features.DataManagement.MirrorDatabase
{
    /// <summary>
    /// MirrorDatabaseDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class MirrorDatabaseDockPaneView : UserControl
    {
        public MirrorDatabaseDockPaneView()
        {
            InitializeComponent();
            DataContext = new MirrorDatabaseViewModel();
        }
    }
}
