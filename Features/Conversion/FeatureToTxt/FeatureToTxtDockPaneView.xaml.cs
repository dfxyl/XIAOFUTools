using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Windows;
using System;
using System.Windows.Input;
using System.Windows.Shapes;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    /// <summary>
    /// 要素类转TXT DockPane视图
    /// </summary>
    public partial class FeatureToTxtDockPaneView : UserControl
    {
        private FeatureToTxtDockPaneViewModel _viewModel;

        public FeatureToTxtDockPaneView()
        {
            try
            {
                InitializeComponent();

                // 创建并设置ViewModel
                _viewModel = new FeatureToTxtDockPaneViewModel();
                this.DataContext = _viewModel;
            }
            catch (Exception ex)
            {
                // 记录错误到系统
                System.Diagnostics.Debug.WriteLine($"FeatureToTxtDockPaneView初始化失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈: {ex.StackTrace}");
                
                // 尝试显示错误信息
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    $"要素图层转TXT工具初始化失败:\n\n{ex.Message}\n\n详细信息:\n{ex.StackTrace}", 
                    "初始化错误", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private OutputFieldItem _draggedItem;
        private Border _draggedBorder;
        private Border _dropTargetBorder;
        private Rectangle _insertionIndicator;
    }

    /// <summary>
    /// 字段名称到颜色的转换器
    /// </summary>
    public class FieldNameToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string fieldName)
            {
                switch (fieldName)
                {
                    case ",":
                        return new SolidColorBrush(Color.FromRgb(255, 255, 224)); // 浅黄色
                    case "@":
                        return new SolidColorBrush(Color.FromRgb(255, 182, 193)); // 浅粉色
                    case "点数":
                    case "图形类型":
                        return new SolidColorBrush(Color.FromRgb(230, 230, 250)); // 浅紫色
                    case "公顷4位":
                    case "公顷6位":
                        return new SolidColorBrush(Color.FromRgb(216, 238, 216)); // 浅绿色(面积相关)
                    default:
                        return new SolidColorBrush(Color.FromRgb(240, 248, 255)); // 浅蓝色
                }
            }
            return new SolidColorBrush(Color.FromRgb(240, 248, 255));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// 分隔线判断转换器
    /// </summary>
    public class SeparatorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str.StartsWith("---") && str.EndsWith("---");
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
