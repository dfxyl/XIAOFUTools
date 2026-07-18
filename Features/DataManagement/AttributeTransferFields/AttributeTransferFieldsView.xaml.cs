using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ArcGIS.Desktop.Framework.Controls;

namespace XIAOFUTools.Features.DataManagement.AttributeTransferFields
{
    /// <summary>
    /// 布尔值反转转换器
    /// </summary>
    public class BooleanInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return false;
        }
    }

    /// <summary>
    /// AttributeTransferFieldsView.xaml 的交互逻辑
    /// </summary>
    public partial class AttributeTransferFieldsView : UserControl
    {
        private static AttributeTransferFieldsView _view = null;
        private static ProWindow _window = null;

        public AttributeTransferFieldsView()
        {
            InitializeComponent(); 
            DataContext = new AttributeTransferFieldsViewModel();
        }

        public static void ShowDialog()
        {
            if (_window != null)
            {
                _window.Close();
                _window = null;
                _view = null;
            }

            _view = new AttributeTransferFieldsView();

            _window = new ProWindow
            {
                Content = _view,
                Title = "属性传递[字段]",
                Width = 900,
                Height = 640,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 720,
                MinHeight = 480
            };

            _window.ShowDialog();
        }

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
