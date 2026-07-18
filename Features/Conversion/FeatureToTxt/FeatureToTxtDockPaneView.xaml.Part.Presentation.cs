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
    }
}
