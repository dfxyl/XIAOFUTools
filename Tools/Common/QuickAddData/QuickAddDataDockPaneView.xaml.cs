using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace XIAOFUTools.Tools.QuickAddData
{
    public partial class QuickAddDataDockPaneView : UserControl
    {
        private const int VkControl = 0x11;
        private const int VkShift = 0x10;

        private readonly QuickAddDataViewModel _viewModel;
        private Point _dragStartPoint;

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int virtualKey);

        public QuickAddDataDockPaneView()
        {
            InitializeComponent();
            _viewModel = new QuickAddDataViewModel();
            DataContext = _viewModel;
        }

        private void LibraryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _viewModel.SelectedNode = e.NewValue as QuickDataTreeItemViewModel;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.ContextMenu == null)
            {
                return;
            }

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }

        private void LibraryTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);
            var clickedNode = treeViewItem?.DataContext as QuickDataTreeItemViewModel;
            if (clickedNode == null)
            {
                return;
            }

            var modifiers = Keyboard.Modifiers;
            var isShiftPressed = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift
                || Keyboard.IsKeyDown(Key.LeftShift)
                || Keyboard.IsKeyDown(Key.RightShift)
                || IsVirtualKeyDown(VkShift);
            var isCtrlPressed = (modifiers & ModifierKeys.Control) == ModifierKeys.Control
                || Keyboard.IsKeyDown(Key.LeftCtrl)
                || Keyboard.IsKeyDown(Key.RightCtrl)
                || IsVirtualKeyDown(VkControl);

            if (isShiftPressed)
            {
                _viewModel.SelectRange(clickedNode, additive: isCtrlPressed);
            }
            else if (isCtrlPressed)
            {
                _viewModel.ToggleSelection(clickedNode);
            }
            else
            {
                _viewModel.SelectSingle(clickedNode);
            }

            treeViewItem.Focus();
            _viewModel.SelectedNode = clickedNode;

            if (isShiftPressed || isCtrlPressed)
            {
                e.Handled = true;
            }
        }

        private void LibraryTree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            var currentPosition = e.GetPosition(null);
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
        }

        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (treeViewItem == null)
            {
                return;
            }

            treeViewItem.IsSelected = true;
            treeViewItem.Focus();
            var clickedNode = treeViewItem.DataContext as QuickDataTreeItemViewModel;
            if (clickedNode != null && !clickedNode.IsSelected)
            {
                _viewModel.SelectSingle(clickedNode);
            }

            _viewModel.SelectedNode = clickedNode;
            e.Handled = true;
        }

        private void LibraryTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (treeViewItem?.DataContext is not QuickDataTreeItemViewModel clickedNode)
            {
                return;
            }

            // 双击只展开/折叠节点，不加载到地图
            // 如果是文件夹类型节点（Group 或 TypeBucket），切换展开状态
            if (clickedNode.NodeKind == QuickDataTreeNodeKind.Group || 
                clickedNode.NodeKind == QuickDataTreeNodeKind.TypeBucket ||
                clickedNode.NodeKind == QuickDataTreeNodeKind.DataNode && clickedNode.Children.Count > 0)
            {
                clickedNode.IsExpanded = !clickedNode.IsExpanded;
            }
            // 如果需要双击加载功能，可以取消下面的注释
            // else if (clickedNode.NodeKind == QuickDataTreeNodeKind.DataNode && clickedNode.Children.Count == 0)
            // {
            //     _viewModel.SelectedNode = clickedNode;
            //     if (_viewModel.LoadSelectedNodeCommand.CanExecute(null))
            //     {
            //         _viewModel.LoadSelectedNodeCommand.Execute(null);
            //     }
            // }
        }

        private static T VisualUpwardSearch<T>(DependencyObject source) where T : DependencyObject
        {
            while (source != null && source is not T)
            {
                source = VisualTreeHelper.GetParent(source);
            }

            return source as T;
        }

        private static bool IsVirtualKeyDown(int virtualKey)
        {
            return (GetKeyState(virtualKey) & 0x8000) != 0;
        }
    }
}
