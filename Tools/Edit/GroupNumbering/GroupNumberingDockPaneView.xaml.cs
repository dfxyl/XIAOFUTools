using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Controls;

namespace XIAOFUTools.Tools.GroupNumbering
{
    /// <summary>
    /// GroupNumberingDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class GroupNumberingDockPaneView : UserControl
    {
        private GroupNumberingViewModel _viewModel;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public GroupNumberingDockPaneView()
        {
            InitializeComponent();
            _viewModel = new GroupNumberingViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// 当控件加载时的处理（ViewModel在构造函数中已经加载图层）
        /// </summary>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // ViewModel在构造函数中已经调用了LoadLayersAsync()，无需额外操作
        }
    }
}
