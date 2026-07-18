#nullable enable

using ArcGIS.Desktop.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

using XIAOFUTools.Features.Custom.GisToolPocket.Application;
using XIAOFUTools.Features.Custom.GisToolPocket.Core;
namespace XIAOFUTools.Features.Custom.GisToolPocket.Presentation
{
    public partial class ToolboxManagerWindow : Window
    {
        private void ToolboxList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshTree();
        }

        private void ToolboxListItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListBoxItem item)
                return;

            if (!item.IsSelected)
            {
                ToolboxList.SelectedItems.Clear();
                item.IsSelected = true;
            }

            item.ContextMenu = CreateSourceItemContextMenu();
        }

        private void ToolTreeItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject);
            if (item is null)
                return;

            item.IsSelected = true;
            item.Focus();
            item.ContextMenu = CreateTreeNodeContextMenu(item.DataContext as ToolboxTreeNode);
        }

        private ContextMenu CreateSourceItemContextMenu()
        {
            var menu = new ContextMenu();
            menu.Items.Add(CreateMenuItem("重命名来源", SourceRenameMenuItem_Click, ToolboxList.SelectedItem is ToolboxCatalog));
            menu.Items.Add(CreateMenuItem(ToolboxList.SelectedItem is ToolboxCatalog catalog && catalog.IsVisible ? "隐藏来源" : "显示来源", SourceToggleVisibleMenuItem_Click, ToolboxList.SelectedItem is ToolboxCatalog));
            menu.Items.Add(CreateMenuItem("刷新选中", SourceReloadMenuItem_Click, SelectedCatalogs().Count > 0));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("上移", SourceMoveUpMenuItem_Click, SelectedCatalogs().Count > 0));
            menu.Items.Add(CreateMenuItem("下移", SourceMoveDownMenuItem_Click, SelectedCatalogs().Count > 0));
            menu.Items.Add(new Separator());
            menu.Items.Add(CreateMenuItem("移除来源", SourceRemoveMenuItem_Click, SelectedCatalogs().Count > 0, isDanger: true));
            return menu;
        }

        private ContextMenu? CreateTreeNodeContextMenu(ToolboxTreeNode? node)
        {
            if (node is null)
                return null;

            var menu = new ContextMenu();
            var isCustom = node.OwnerCatalog is not null && ToolboxApplicationService.IsCustomCatalog(node.OwnerCatalog);

            if (node.Kind == ToolboxTreeNode.CatalogKind)
            {
                if (isCustom)
                {
                    menu.Items.Add(CreateMenuItem("加载工具...", TreeAddToolMenuItem_Click));
                    menu.Items.Add(CreateMenuItem("新建工具集", TreeAddToolsetMenuItem_Click));
                    menu.Items.Add(new Separator());
                }

                menu.Items.Add(CreateMenuItem(isCustom ? "重命名组" : "重命名工具箱", TreeRenameMenuItem_Click));
                menu.Items.Add(CreateMenuItem(node.IsVisible ? "隐藏" : "显示", TreeToggleVisibleMenuItem_Click));
                menu.Items.Add(new Separator());
                menu.Items.Add(CreateMenuItem("展开全部", TreeExpandNodeMenuItem_Click));
                menu.Items.Add(CreateMenuItem("折叠全部", TreeCollapseNodeMenuItem_Click));
                return menu;
            }

            if (node.Kind == ToolboxTreeNode.ToolsetKind)
            {
                if (isCustom)
                {
                    menu.Items.Add(CreateMenuItem("加载工具...", TreeAddToolMenuItem_Click));
                    menu.Items.Add(CreateMenuItem("新建子工具集", TreeAddToolsetMenuItem_Click));
                    menu.Items.Add(new Separator());
                }

                menu.Items.Add(CreateMenuItem("重命名工具集", TreeRenameMenuItem_Click));
                menu.Items.Add(CreateMenuItem(node.IsVisible ? "隐藏工具集" : "显示工具集", TreeToggleVisibleMenuItem_Click));
                if (isCustom)
                    menu.Items.Add(CreateMenuItem("删除工具集", TreeDeleteMenuItem_Click, node.CanDelete, isDanger: true));

                menu.Items.Add(new Separator());
                menu.Items.Add(CreateMenuItem("展开", TreeExpandNodeMenuItem_Click));
                menu.Items.Add(CreateMenuItem("折叠", TreeCollapseNodeMenuItem_Click));
                return menu;
            }

            if (node.Kind == ToolboxTreeNode.ToolKind)
            {
                menu.Items.Add(CreateMenuItem("重命名工具", TreeRenameMenuItem_Click));
                menu.Items.Add(CreateMenuItem(node.IsVisible ? "隐藏工具" : "显示工具", TreeToggleVisibleMenuItem_Click));
                if (isCustom)
                    menu.Items.Add(CreateMenuItem("删除工具", TreeDeleteMenuItem_Click, node.CanDelete, isDanger: true));

                return menu;
            }

            return null;
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler click, bool isEnabled = true, bool isDanger = false)
        {
            var item = new MenuItem
            {
                Header = header,
                IsEnabled = isEnabled
            };

            if (isDanger)
                item.Foreground = new SolidColorBrush(Color.FromRgb(180, 35, 24));

            item.Click += click;
            return item;
        }

        private void ToolTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is ToolboxTreeNode node)
            {
                SelectedNodeTypeText.Text = node.KindText;
                SelectedNodeNameText.Text = node.InternalName;
                SelectedNodePathText.Text = node.FullPath;
                return;
            }

            ClearSelectedNodeDetails();
        }

        private void ExpandTreeButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var node in TreeNodes)
                SetNodeExpanded(node, true);
        }

        private void CollapseTreeButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var node in TreeNodes)
                SetNodeExpanded(node, false);
        }

        private void AddToolsetButton_Click(object sender, RoutedEventArgs e)
        {
            var target = ResolveToolsetCollectionTarget();
            if (target is null)
            {
                MessageBox.Show("请选择一个自定义组或工具集。", "GIS 工具口袋");
                return;
            }

            var name = CreateUniqueToolsetName(target.Collection, "新工具集");
            var toolset = new ToolboxToolset { Name = name };
            target.Collection.Add(toolset);
            RefreshTreeAndReveal(toolset);
            UpdateStatus();
        }

        private void AddSystemToolButton_Click(object sender, RoutedEventArgs e)
        {
            AddSystemTools(forceRoot: false);
        }

        private void DeleteTreeNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolTree.SelectedItem is not ToolboxTreeNode node)
            {
                MessageBox.Show("请选择要删除的节点。", "GIS 工具口袋");
                return;
            }

            if (!node.CanDelete || !node.Delete())
            {
                MessageBox.Show("当前节点不支持删除。", "GIS 工具口袋");
                return;
            }

            RefreshTree();
            UpdateStatus();
        }

        private void SourceAddToolboxMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddButton_Click(sender, e);
        }

        private void SourceAddGroupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddCustomGroupButton_Click(sender, e);
        }

        private void SourceRenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ToolboxList.SelectedItem is not ToolboxCatalog catalog)
                return;

            var name = PromptForText("重命名来源", "名称", catalog.DisplayTitle);
            if (name is null)
                return;

            catalog.DisplayName = name;
            ToolboxList.Items.Refresh();
            RefreshTree();
            UpdateStatus();
        }

        private void SourceReloadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ReloadSelectedButton_Click(sender, e);
        }

        private void SourceToggleVisibleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ToolboxList.SelectedItem is not ToolboxCatalog catalog)
                return;

            catalog.IsVisible = !catalog.IsVisible;
            ToolboxList.Items.Refresh();
            RefreshTreeAndReveal(catalog);
            UpdateStatus();
        }

        private void SourceReloadAllMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ReloadAllButton_Click(sender, e);
        }

        private void SourceMoveUpMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MoveUpButton_Click(sender, e);
        }

        private void SourceMoveDownMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MoveDownButton_Click(sender, e);
        }

        private void SourceImportPackageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ImportPackageButton_Click(sender, e);
        }

        private void SourceExportPackageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ExportPackageButton_Click(sender, e);
        }

        private void SourceRemoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            RemoveButton_Click(sender, e);
        }

        private void SourceClearMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ClearButton_Click(sender, e);
        }

        private void TreeAddToolMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddSystemToolButton_Click(sender, e);
        }

        private void TreeAddToolsetMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddToolsetButton_Click(sender, e);
        }

        private void TreeRenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ToolTree.SelectedItem is not ToolboxTreeNode node || !node.CanRename)
                return;

            var name = PromptForText("重命名", "名称", node.EditableName);
            if (name is null)
                return;

            node.EditableName = name;
            object? model = node.Tool;
            model ??= node.Toolset;
            model ??= node.OwnerCatalog;
            RefreshTreeAndReveal(model);
            UpdateStatus();
        }

        private void TreeDeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DeleteTreeNodeButton_Click(sender, e);
        }

        private void TreeToggleVisibleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ToolTree.SelectedItem is not ToolboxTreeNode node)
                return;

            node.ToggleVisible();
            ToolboxList.Items.Refresh();
            object? model = node.Tool;
            model ??= node.Toolset;
            model ??= node.OwnerCatalog;
            RefreshTreeAndReveal(model);
            UpdateStatus();
        }

        private void TreeExpandNodeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ToolTree.SelectedItem is not ToolboxTreeNode node)
                return;

            SetNodeExpanded(node, true);
        }

        private void TreeCollapseNodeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (ToolTree.SelectedItem is not ToolboxTreeNode node)
                return;

            SetNodeExpanded(node, false);
        }

        private void ToolTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(ToolTree);
            _dragSourceNode = ResolveTreeNodeFromElement(e.OriginalSource as DependencyObject);
        }

        private void ToolTree_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragSourceNode is null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var currentPosition = e.GetPosition(ToolTree);
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (!_dragSourceNode.CanDrag)
                return;

            DragDrop.DoDragDrop(ToolTree, new DataObject(typeof(ToolboxTreeNode), _dragSourceNode), DragDropEffects.Move);
            _dragSourceNode = null;
        }

        private void ToolTree_DragOver(object sender, DragEventArgs e)
        {
            var sourceNode = GetDraggedNode(e.Data);
            var targetInfo = ResolveDropTarget(e);
            if (sourceNode is null || targetInfo is null || !CanDropNode(sourceNode, targetInfo.Target, targetInfo.Placement))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.Move;
            StatusText.Text = FormatDropStatus(sourceNode, targetInfo.Target, targetInfo.Placement);
            e.Handled = true;
        }

        private void ToolTree_Drop(object sender, DragEventArgs e)
        {
            var sourceNode = GetDraggedNode(e.Data);
            var targetInfo = ResolveDropTarget(e);
            if (sourceNode is null || targetInfo is null || !CanDropNode(sourceNode, targetInfo.Target, targetInfo.Placement))
            {
                UpdateStatus();
                return;
            }

            var movedModel = sourceNode.Tool as object ?? sourceNode.Toolset!;
            if (!MoveNode(sourceNode, targetInfo.Target, targetInfo.Placement))
            {
                UpdateStatus();
                return;
            }

            RefreshTreeAndReveal(movedModel);
            UpdateStatus();
            e.Handled = true;
        }

        private void ToolTree_DragLeave(object sender, DragEventArgs e)
        {
            if (!_isBusy)
                UpdateStatus();
        }

    }
}
