using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace XIAOFUTools.Tools.DataProcessing.ExportDatabaseSchema
{
    /// <summary>
    /// ExportDatabaseSchemaDockPaneView.xaml 的交互逻辑
    /// </summary>
    public partial class ExportDatabaseSchemaDockPaneView : UserControl
    {
        public ExportDatabaseSchemaDockPaneView()
        {
            InitializeComponent();
            DataContext = new ExportDatabaseSchemaViewModel();
        }
    }

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
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return value;
        }
    }
}
