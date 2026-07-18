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
using ArcGIS.Desktop.Framework.Controls;

namespace XIAOFUTools.Features.DataManagement.RangeClipTool
{
    /// <summary>
    /// RangeClipToolDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class RangeClipToolDockPaneView : UserControl
    {
        private RangeClipToolViewModel _viewModel;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public RangeClipToolDockPaneView()
        {
            InitializeComponent();
            _viewModel = new RangeClipToolViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// 当控件加载时刷新图层列表
        /// </summary>
        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel?.RefreshLayers();
        }
    }
}
