using System.Windows.Controls;

namespace XIAOFUTools.Features.Editing.OverlapCheck
{
    /// <summary>
    /// 图形重叠检查工具停靠窗格视图
    /// </summary>
    public partial class OverlapCheckDockPaneView : UserControl
    {
        public OverlapCheckDockPaneView()
        {
            InitializeComponent();
            DataContext = new OverlapCheckDockPaneViewModel();
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 界面加载完成后的初始化操作
            if (DataContext is OverlapCheckDockPaneViewModel viewModel)
            {
                viewModel.RefreshLayers();
            }
        }
    }
}
