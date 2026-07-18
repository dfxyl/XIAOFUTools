using System;
using System.Collections.Generic;
using System.Globalization;
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

namespace XIAOFUTools.Features.DataManagement.FieldCopyTool
{
    /// <summary>
    /// 布尔值反转转换器
    /// </summary>
    public class BooleanInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }

    /// <summary>
    /// FieldCopyToolView.xaml 的交互逻辑
    /// </summary>
    public partial class FieldCopyToolView : UserControl
    {
        private static FieldCopyToolView _view = null;
        private static ProWindow _window = null;

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public FieldCopyToolView()
        {
            InitializeComponent();
            DataContext = new FieldCopyToolViewModel();
        }

        /// <summary>
        /// 显示窗口
        /// </summary>
        public static void ShowDialog()
        {
            if (_window != null)
            {
                _window.Close();
                _window = null;
                _view = null;
            }

            _view = new FieldCopyToolView();

            _window = new ProWindow
            {
                Content = _view,
                Title = "字段复制工具",
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 600,
                MinHeight = 400
            };

            _window.ShowDialog();
        }

        /// <summary>
        /// 关闭窗口
        /// </summary>
        public static void CloseDialog()
        {
            if (_window != null)
            {
                _window.Close();
                _window = null;
                _view = null;
            }
        }
    }
}
