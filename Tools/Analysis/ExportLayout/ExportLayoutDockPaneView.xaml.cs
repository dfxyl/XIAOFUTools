using System.Windows.Controls;

namespace XIAOFUTools.Tools.ExportLayout
{
    /// <summary>
    /// 导出布局停靠窗格视图
    /// </summary>
    public partial class ExportLayoutDockPaneView : UserControl
    {
        public ExportLayoutDockPaneView()
        {
            InitializeComponent();
            DataContext = new ExportLayoutViewModel();
        }
    }
}
