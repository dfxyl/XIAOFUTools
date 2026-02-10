using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace XIAOFUTools.Tools.DataProcessing.ExportShpFieldTable
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
    /// ExportShpFieldTableDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class ExportShpFieldTableDockPaneView : UserControl
    {
        public ExportShpFieldTableDockPaneView()
        {
            InitializeComponent();
            DataContext = new ExportShpFieldTableViewModel();
        }
    }
}
