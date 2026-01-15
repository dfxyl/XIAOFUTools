using System.Windows.Controls;

namespace XIAOFUTools.Tools.Edit.Boundary.MapBoundaryPointLineGenerator
{
    /// <summary>
    /// 地图生成界址点线 View
    /// </summary>
    public partial class MapBoundaryPointLineGeneratorDockPaneView : UserControl
    {
        public MapBoundaryPointLineGeneratorDockPaneView()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            DataContext = new MapBoundaryPointLineGeneratorDockPaneViewModel();
        }
    }
}
