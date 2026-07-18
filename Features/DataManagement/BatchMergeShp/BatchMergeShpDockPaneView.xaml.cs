using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace XIAOFUTools.Features.DataManagement.BatchMergeShp
{
    /// <summary>
    /// 布尔反向转换器。
    /// </summary>
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

    /// <summary>
    /// BatchMergeShpDockPaneView.xaml 的交互逻辑。
    /// </summary>
    public partial class BatchMergeShpDockPaneView : UserControl
    {
        public BatchMergeShpDockPaneView()
        {
            InitializeComponent();
            DataContext = new BatchMergeShpViewModel();
        }
    }
}
