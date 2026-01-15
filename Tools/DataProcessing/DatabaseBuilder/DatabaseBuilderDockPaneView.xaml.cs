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

namespace XIAOFUTools.Tools.DataProcessing.DatabaseBuilder
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
    /// DatabaseBuilderDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class DatabaseBuilderDockPaneView : UserControl
    {
        /// <summary>
        /// 默认构造函数
        /// </summary>
        public DatabaseBuilderDockPaneView()
        {
            InitializeComponent();
            DataContext = new DatabaseBuilderViewModel();
        }
    }
}
