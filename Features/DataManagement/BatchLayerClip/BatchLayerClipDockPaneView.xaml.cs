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

namespace XIAOFUTools.Features.DataManagement.BatchLayerClip
{
    /// <summary>
    /// 布尔值到文本转换器
    /// </summary>
    public class BooleanToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool boolValue = (bool)value;
            string parameterString = parameter as string;

            if (!string.IsNullOrEmpty(parameterString) && parameterString.Contains('|'))
            {
                string[] options = parameterString.Split('|');
                if (options.Length >= 2)
                {
                    return boolValue ? options[0] : options[1];
                }
            }

            return boolValue ? "是" : "否";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// BatchLayerClipDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class BatchLayerClipDockPaneView : UserControl
    {
        private BatchLayerClipViewModel _viewModel;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public BatchLayerClipDockPaneView()
        {
            InitializeComponent();
            _viewModel = new BatchLayerClipViewModel();
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
