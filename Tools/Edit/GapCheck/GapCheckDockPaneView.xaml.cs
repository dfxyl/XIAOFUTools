using System.Windows.Controls;

namespace XIAOFUTools.Tools.GapCheck
{
    /// <summary>
    /// 缝隙检查工具停靠窗格视图
    /// </summary>
    public partial class GapCheckDockPaneView : UserControl
    {
        public GapCheckDockPaneView()
        {
            InitializeComponent();
            DataContext = new GapCheckDockPaneViewModel();
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 界面加载完成后的初始化操作
            if (DataContext is GapCheckDockPaneViewModel viewModel)
            {
                viewModel.Initialize();
            }
        }
    }
}