using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace XIAOFUTools.Tools.HistoricalImagery
{
    /// <summary>
    /// 历史影像停靠窗格视图
    /// </summary>
    public partial class HistoricalImageryDockPaneView : UserControl
    {
        public HistoricalImageryDockPaneView()
        {
            InitializeComponent();
            DataContext = new HistoricalImageryDockPaneViewModel();
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
