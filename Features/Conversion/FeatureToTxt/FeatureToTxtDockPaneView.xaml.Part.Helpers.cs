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
    }
}
