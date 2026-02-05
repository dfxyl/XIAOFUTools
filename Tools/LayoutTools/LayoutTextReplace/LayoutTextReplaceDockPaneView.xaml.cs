using System.Windows.Controls;

namespace XIAOFUTools.Tools.LayoutTools.LayoutTextReplace
{
    /// <summary>
    /// 布局元素查找替换停靠窗格视图
    /// </summary>
    public partial class LayoutTextReplaceDockPaneView : UserControl
    {
        public LayoutTextReplaceDockPaneView()
        {
            InitializeComponent();
            DataContext = new LayoutTextReplaceViewModel();
        }
    }
}
