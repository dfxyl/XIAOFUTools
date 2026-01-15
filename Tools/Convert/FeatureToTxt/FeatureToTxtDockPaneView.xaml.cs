using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Windows;
using System;
using System.Windows.Input;
using System.Windows.Shapes;

namespace XIAOFUTools.Tools.FeatureToTxt
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

        /// <summary>
        /// 用户控件加载事件
        /// </summary>
        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 在控件加载后刷新图层列表
            _viewModel?.RefreshLayers();
        }

        private OutputFieldItem _draggedItem;
        private Border _draggedBorder;
        private Border _dropTargetBorder;
        private Rectangle _insertionIndicator;

        /// <summary>
        /// 拖拽开始事件
        /// </summary>
        private void ListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && e.OriginalSource is DependencyObject element)
            {
                // 向上查找直到找到ListBoxItem
                var parent = element;
                while (parent != null && !(parent is ListBoxItem))
                {
                    parent = VisualTreeHelper.GetParent(parent) ?? LogicalTreeHelper.GetParent(parent);
                }

                var item = (parent as ListBoxItem)?.DataContext as OutputFieldItem;
                if (item != null) // 允许拖拽所有字段
                {
                    _draggedItem = item;

                    // 清除之前的拖拽指示
                    ClearDropTargetIndicator();

                    // 查找Border元素用于视觉效果
                    var border = FindVisualChild<Border>(parent as ListBoxItem);
                    if (border != null)
                    {
                        _draggedBorder = border;
                        border.Opacity = 0.7;
                        border.BorderThickness = new Thickness(2);
                        border.BorderBrush = new SolidColorBrush(Colors.Blue);
                    }

                    // 设置拖拽光标
                    listBox.Cursor = Cursors.IBeam;

                    DragDrop.DoDragDrop(listBox, item, DragDropEffects.Move);

                    // 恢复视觉效果和光标
                    if (border != null)
                    {
                        border.Opacity = 1.0;
                        border.BorderThickness = new Thickness(1);
                        border.BorderBrush = new SolidColorBrush(Color.FromRgb(176, 196, 222));
                    }

                    // 清除拖拽指示
                    ClearDropTargetIndicator();
                    listBox.Cursor = Cursors.Arrow;
                }
            }
        }

        /// <summary>
        /// 拖拽放置事件
        /// </summary>
        private void ListBox_Drop(object sender, DragEventArgs e)
        {
            if (sender is ListBox listBox && DataContext is FeatureToTxtDockPaneViewModel viewModel)
            {
                var draggedItem = e.Data.GetData(typeof(OutputFieldItem)) as OutputFieldItem;

                // 获取目标项
                OutputFieldItem targetItem = null;
                if (e.OriginalSource is DependencyObject element)
                {
                    // 向上查找直到找到ListBoxItem
                    var parent = element;
                    while (parent != null && !(parent is ListBoxItem))
                    {
                        parent = VisualTreeHelper.GetParent(parent) ?? LogicalTreeHelper.GetParent(parent);
                    }
                    targetItem = (parent as ListBoxItem)?.DataContext as OutputFieldItem;
                }

                if (draggedItem != null && targetItem != null && draggedItem != targetItem)
                {
                    var draggedIndex = viewModel.OutputFields.IndexOf(draggedItem);
                    var targetIndex = viewModel.OutputFields.IndexOf(targetItem);

                    if (draggedIndex >= 0 && targetIndex >= 0)
                    {
                        viewModel.OutputFields.Move(draggedIndex, targetIndex);
                        viewModel.StatusMessage = $"已移动字段 '{draggedItem.FieldName}' 到新位置";
                    }
                }

                // 清除拖拽指示和恢复光标
                ClearDropTargetIndicator();
                listBox.Cursor = Cursors.Arrow;
            }
        }

        /// <summary>
        /// 拖拽进入事件
        /// </summary>
        private void ListBox_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                listBox.Cursor = Cursors.IBeam;
                e.Effects = DragDropEffects.Move;
            }
        }

        /// <summary>
        /// 拖拽离开事件
        /// </summary>
        private void ListBox_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                listBox.Cursor = Cursors.Arrow;

                // 清除拖拽目标指示
                ClearDropTargetIndicator();
            }
        }

        /// <summary>
        /// 拖拽悬停事件 - 显示放置位置指示
        /// </summary>
        private void ListBox_DragOver(object sender, DragEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                e.Effects = DragDropEffects.Move;

                // 清除之前的指示
                ClearDropTargetIndicator();

                // 获取鼠标位置下的目标项
                if (e.OriginalSource is DependencyObject element)
                {
                    // 向上查找直到找到ListBoxItem
                    var parent = element;
                    while (parent != null && !(parent is ListBoxItem))
                    {
                        parent = VisualTreeHelper.GetParent(parent) ?? LogicalTreeHelper.GetParent(parent);
                    }

                    if (parent is ListBoxItem targetListBoxItem)
                    {
                        // 查找Border元素用于视觉指示
                        var border = FindVisualChild<Border>(targetListBoxItem);
                        if (border != null)
                        {
                            _dropTargetBorder = border;

                            // 获取鼠标相对于目标项的位置
                            var mousePos = e.GetPosition(targetListBoxItem);
                            var itemWidth = targetListBoxItem.ActualWidth;

                            // 判断是插入到左边还是右边
                            bool insertBefore = mousePos.X < itemWidth / 2;

                            // 设置拖拽目标指示样式 - 更明显的插入线效果
                            if (insertBefore)
                            {
                                border.BorderBrush = new SolidColorBrush(Colors.Blue);
                                border.BorderThickness = new Thickness(4, 1, 1, 1); // 左边粗线
                            }
                            else
                            {
                                border.BorderBrush = new SolidColorBrush(Colors.Blue);
                                border.BorderThickness = new Thickness(1, 1, 4, 1); // 右边粗线
                            }

                            border.Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 255)); // 半透明蓝色
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 清除拖拽目标指示
        /// </summary>
        private void ClearDropTargetIndicator()
        {
            if (_dropTargetBorder != null)
            {
                // 恢复原始边框样式
                _dropTargetBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(176, 196, 222));
                _dropTargetBorder.BorderThickness = new Thickness(1);

                // 恢复原始背景色
                if (_dropTargetBorder.DataContext is OutputFieldItem item)
                {
                    var converter = new FieldNameToColorConverter();
                    _dropTargetBorder.Background = (SolidColorBrush)converter.Convert(item.FieldName, null, null, null);
                }

                _dropTargetBorder = null;
            }

            // 清除插入指示器（如果有的话）
            if (_insertionIndicator != null)
            {
                if (_insertionIndicator.Parent is Panel parent)
                {
                    parent.Children.Remove(_insertionIndicator);
                }
                _insertionIndicator = null;
            }
        }

        /// <summary>
        /// 鼠标进入事件
        /// </summary>
        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = new SolidColorBrush(Colors.DarkBlue);
                border.BorderThickness = new Thickness(2);
            }
        }

        /// <summary>
        /// 鼠标离开事件
        /// </summary>
        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(176, 196, 222));
                border.BorderThickness = new Thickness(1);
            }
        }

        /// <summary>
        /// 查找可视化子元素
        /// </summary>
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }
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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
