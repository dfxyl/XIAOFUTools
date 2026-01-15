using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace XIAOFUTools.Tools.Authorization
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
    /// 授权管理停靠窗格视图
    /// </summary>
    public partial class AuthorizationDockPaneView : UserControl
    {
        public AuthorizationDockPaneView()
        {
            InitializeComponent();
            // DataContext将由DockPane设置，不在这里创建新实例
        }
    }
}
