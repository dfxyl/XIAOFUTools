using System.Windows.Controls;

namespace XIAOFUTools.Tools.NodeDistanceCheck
{
    /// <summary>
    /// 节点距离检查工具视图
    /// </summary>
    public partial class NodeDistanceCheckDockPaneView : UserControl
    {
        public NodeDistanceCheckDockPaneView()
        {
            InitializeComponent();
            DataContext = new NodeDistanceCheckDockPaneViewModel();
        }

        /// <summary>
        /// 用户控件加载事件
        /// </summary>
        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 刷新图层列表
            if (DataContext is NodeDistanceCheckDockPaneViewModel viewModel)
            {
                viewModel.RefreshLayers();
            }
        }
    }
}