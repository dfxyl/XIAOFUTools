using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Tools.Edit.Boundary.BoundaryPointLineGenerator
{
    public partial class BoundaryPointLineGeneratorDockPaneView : UserControl
    {
        public BoundaryPointLineGeneratorDockPaneView()
        {
            InitializeComponent();
            // 直接在构造函数中绑定 ViewModel，避免被 DockPane 默认 DataContext 占用导致绑定失效
            DataContext = new BoundaryPointLineGeneratorDockPaneViewModel();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载时主动刷新一次图层，确保列表及时显示
            if (DataContext is BoundaryPointLineGeneratorDockPaneViewModel vm && vm.RefreshLayersCommand != null)
            {
                if (vm.RefreshLayersCommand.CanExecute(null))
                    vm.RefreshLayersCommand.Execute(null);
            }
        }
    }
}
