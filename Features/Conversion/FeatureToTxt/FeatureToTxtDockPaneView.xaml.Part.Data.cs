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
    public partial class FeatureToTxtDockPaneView
    {

        /// <summary>
        /// 用户控件加载事件
        /// </summary>
        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // 在控件加载后刷新图层列表
            _viewModel?.RefreshLayers();
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
}
