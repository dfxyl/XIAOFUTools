using System.Windows;
using System.Windows.Controls;

namespace XIAOFUTools.Features.DataManagement.BatchGeometryRepair
{
    /// <summary>
    /// BatchGeometryRepairDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class BatchGeometryRepairDockPaneView : UserControl
    {
        private BatchGeometryRepairViewModel _viewModel;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public BatchGeometryRepairDockPaneView()
        {
            InitializeComponent();
            _viewModel = new BatchGeometryRepairViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// 当控件加载时刷新图层列表
        /// </summary>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.RefreshLayers();
        }

        /// <summary>
        /// 图层选择状态改变时更新命令状态
        /// </summary>
        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // 命令状态会通过属性绑定自动更新
        }

        /// <summary>
        /// 图层取消选择状态改变时更新命令状态
        /// </summary>
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // 命令状态会通过属性绑定自动更新
        }
    }
}
