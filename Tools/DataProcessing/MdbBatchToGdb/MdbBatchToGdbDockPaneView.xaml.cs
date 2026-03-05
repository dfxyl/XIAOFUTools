using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    public class BooleanInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool booleanValue ? !booleanValue : true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool booleanValue ? !booleanValue : false;
        }
    }

    public partial class MdbBatchToGdbDockPaneView : UserControl
    {
        public MdbBatchToGdbDockPaneView()
        {
            InitializeComponent();
            DataContext = new MdbBatchToGdbViewModel();
        }
    }
}
