using System.Windows.Controls;

namespace XIAOFUTools.Features.Custom.DevZoneCheck
{
    /// <summary>
    /// 开发区整合优化核查DockPane视图
    /// </summary>
    public partial class DevZoneCheckDockPaneView : UserControl
    {
        private DevZoneCheckDockPaneViewModel _viewModel;

        public DevZoneCheckDockPaneView()
        {
            InitializeComponent();
            _viewModel = new DevZoneCheckDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _viewModel?.RefreshLayers();
        }
    }
}
