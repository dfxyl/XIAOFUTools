using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace XIAOFUTools.Features.General.HistoricalImagery
{
    /// <summary>
    /// 历史影像停靠窗格视图
    /// </summary>
    public partial class HistoricalImageryDockPaneView : UserControl
    {
        private Point _dragStartPoint;

        public HistoricalImageryDockPaneView()
        {
            InitializeComponent();
            DataContext = new HistoricalImageryDockPaneViewModel();
        }

        private void HistoricalImageryTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (treeViewItem?.DataContext is not TreeNode node || DataContext is not HistoricalImageryDockPaneViewModel viewModel)
            {
                return;
            }

            treeViewItem.Focus();
            treeViewItem.IsSelected = true;
            viewModel.SelectedNode = node;
        }

        private void HistoricalImageryTree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var currentPosition = e.GetPosition(null);
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
        }

        /// <summary>
        /// 树形节点选中事件处理
        /// </summary>
        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TreeViewItem item && item.DataContext is TreeNode node)
            {
                if (DataContext is HistoricalImageryDockPaneViewModel viewModel)
                {
                    viewModel.SelectedNode = node;
                }
            }
        }

        /// <summary>
        /// 右键菜单"添加到地图"点击事件
        /// </summary>
        private void AddLayerMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && 
                menuItem.DataContext is TreeNode node &&
                DataContext is HistoricalImageryDockPaneViewModel viewModel)
            {
                // 设置选中的节点
                viewModel.SelectedNode = node;
                
                // 调用添加图层命令
                if (viewModel.AddLayerCommand?.CanExecute(null) == true)
                {
                    viewModel.AddLayerCommand.Execute(null);
                }
            }
        }

        private static T VisualUpwardSearch<T>(DependencyObject source) where T : DependencyObject
        {
            while (source != null && source is not T)
            {
                source = VisualTreeHelper.GetParent(source);
            }

            return source as T;
        }
    }

    /// <summary>
    /// 年份显示转换器
    /// </summary>
    public class YearDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int year)
            {
                return year == 0 ? "全部年份" : year.ToString();
            }
            return value?.ToString() ?? "全部年份";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// 反向布尔到可见性转换器
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
