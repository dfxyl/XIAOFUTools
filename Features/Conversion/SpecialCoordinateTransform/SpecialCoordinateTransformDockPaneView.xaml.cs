using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace XIAOFUTools.Features.Conversion.SpecialCoordinateTransform
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

    /// <summary>
    /// 特殊坐标转换 DockPane 视图
    /// </summary>
    public partial class SpecialCoordinateTransformDockPaneView : UserControl
    {
        private SpecialCoordinateTransformDockPaneViewModel _viewModel;

        public SpecialCoordinateTransformDockPaneView()
        {
            InitializeComponent();
            _viewModel = new SpecialCoordinateTransformDockPaneViewModel();
            DataContext = _viewModel;
        }

        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not SpecialCoordinateTransformDockPaneViewModel viewModel)
            {
                _viewModel = new SpecialCoordinateTransformDockPaneViewModel();
                DataContext = _viewModel;
                viewModel = _viewModel;
            }

            viewModel.RefreshLayers();
        }
    }
}
