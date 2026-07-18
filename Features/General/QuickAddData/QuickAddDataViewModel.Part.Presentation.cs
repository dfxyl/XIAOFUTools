using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using Microsoft.Win32;

namespace XIAOFUTools.Features.General.QuickAddData
{
    internal sealed partial class QuickAddDataViewModel
    {

        public void SelectSingle(QuickDataTreeItemViewModel node)
        {
            if (node == null)
            {
                return;
            }

            ClearSelection();
            node.IsSelected = true;
            _selectionAnchorKey = node.StateKey;
            SelectedNode = node;
        }


        public void ToggleSelection(QuickDataTreeItemViewModel node)
        {
            if (node == null)
            {
                return;
            }

            node.IsSelected = !node.IsSelected;
            if (node.IsSelected)
            {
                _selectionAnchorKey = node.StateKey;
                SelectedNode = node;
                return;
            }

            SelectedNode = SelectedNodes.FirstOrDefault(selectedNode => selectedNode.StateKey != node.StateKey);
        }


        public void SelectRange(QuickDataTreeItemViewModel targetNode, bool additive)
        {
            if (targetNode == null)
            {
                return;
            }

            var visibleNodes = EnumerateVisibleTreeNodes(TreeNodes)
                .Where(node => node.IsMapDraggable)
                .ToList();

            var orderedKeys = visibleNodes.Select(node => node.StateKey).ToList();
            var anchorKey = string.IsNullOrWhiteSpace(_selectionAnchorKey) ? targetNode.StateKey : _selectionAnchorKey;
            var selectedKeys = QuickDataTreeSelectionHelper.BuildRangeSelection(
                orderedKeys,
                anchorKey,
                targetNode.StateKey,
                SelectedNodes.Select(node => node.StateKey),
                additive);

            ApplySelectionByKeys(selectedKeys, targetNode);
            _selectionAnchorKey = anchorKey;
        }


        private void RenameSelected()
        {
            if (IsBusy)
            {
                return;
            }

            var group = SelectedNode?.ResolveGroup();
            if (SelectedNode?.NodeKind != QuickDataTreeNodeKind.Group || group == null)
            {
                StatusMessage = "请选择一个分组后再重命名。";
                return;
            }

            var newName = PromptForText("重命名分组", "请输入新的分组名称：", group.Name);
            if (string.IsNullOrWhiteSpace(newName) || string.Equals(group.Name, newName, StringComparison.Ordinal))
            {
                return;
            }

            if (_document.Groups.Any(item => !ReferenceEquals(item, group)
                && string.Equals(item.Name, newName, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = $"分组“{newName}”已存在。";
                return;
            }

            group.Name = newName;
            PersistLibrary($"已重命名为“{newName}”。");
        }


        private void AddFilesToSelectedGroup()
        {
            if (IsBusy)
            {
                return;
            }

            var targetGroup = SelectedNode?.NodeKind == QuickDataTreeNodeKind.Group
                ? SelectedNode.Group
                : null;

            if (targetGroup == null)
            {
                StatusMessage = "请在分组节点上右键使用“添加文件”。";
                return;
            }

            ImportFilesIntoGroup(targetGroup);
        }


        private void ImportFolderToSelectedGroup()
        {
            if (IsBusy)
            {
                return;
            }

            var targetGroup = SelectedNode?.NodeKind == QuickDataTreeNodeKind.Group
                ? SelectedNode.Group
                : null;

            if (targetGroup == null)
            {
                StatusMessage = "请在分组节点上右键使用“扫描目录”。";
                return;
            }

            ImportFolderIntoGroup(targetGroup);
        }


        private void DeleteSelected()
        {
            if (IsBusy || SelectedNode == null)
            {
                return;
            }

            var targets = GetActionableSelection().ToList();
            if (targets.Count == 0)
            {
                StatusMessage = "当前选择不支持删除。";
                return;
            }

            var removedGroups = 0;
            var removedNodes = 0;

            foreach (var target in targets)
            {
                if (target.NodeKind == QuickDataTreeNodeKind.Group && target.Group != null)
                {
                    if (_document.Groups.Remove(target.Group))
                    {
                        removedGroups++;
                    }

                    continue;
                }

                if (target.NodeKind != QuickDataTreeNodeKind.DataNode || target.DataNode == null)
                {
                    continue;
                }

                if (target.IsTopLevelDataNode)
                {
                    var group = target.ResolveGroup();
                    if (group?.Nodes.Remove(target.DataNode) == true)
                    {
                        removedNodes++;
                    }

                    continue;
                }

                if (target.Parent?.NodeKind == QuickDataTreeNodeKind.DataNode && target.Parent.DataNode != null)
                {
                    if (target.Parent.DataNode.Children.Remove(target.DataNode))
                    {
                        removedNodes++;
                    }
                }
            }

            if (removedGroups == 0 && removedNodes == 0)
            {
                StatusMessage = "当前选择不支持删除。";
                return;
            }

            PersistLibrary($"已删除 {removedGroups} 个分组、{removedNodes} 个数据项。", clearSelection: true);
        }


        private void RefreshLibraryOrSelectedNode()
        {
            if (IsBusy)
            {
                return;
            }

            if (SelectedNode?.NodeKind == QuickDataTreeNodeKind.DataNode
                && SelectedNode.DataNode?.NodeKind == QuickDataNodeKind.Geodatabase)
            {
                _ = RefreshSelectedDatabaseAsync();
                return;
            }

            ReloadLibrary();
            StatusMessage = "已从磁盘重新加载快捷库。";
        }


        private async Task RefreshSelectedDatabaseAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (SelectedNode?.NodeKind != QuickDataTreeNodeKind.DataNode
                || SelectedNode.DataNode?.NodeKind != QuickDataNodeKind.Geodatabase)
            {
                StatusMessage = "请选择一个数据库节点后再刷新。";
                return;
            }

            IsBusy = true;
            try
            {
                var refreshed = (await Task.Run(() => _importService.ImportPaths(
                    new[] { SelectedNode.DataNode.SourcePath },
                    new QuickDataImportOptions
                    {
                        IncludeFeatureClasses = true,
                        IncludeTables = true,
                        IncludeRasters = false,
                        IncludeLayerFiles = false,
                        Recurse = false
                    })))
                    .FirstOrDefault();

                SelectedNode.DataNode.Children = refreshed?.Children ?? new List<QuickDataNode>();
                PersistLibrary($"已刷新数据库“{SelectedNode.DataNode.Name}”的子项。");
            }
            finally
            {
                IsBusy = false;
            }
        }


        private void OnTreeNodePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isRestoringTreeState
                || !string.Equals(e.PropertyName, nameof(QuickDataTreeItemViewModel.IsExpanded), StringComparison.Ordinal)
                || sender is not QuickDataTreeItemViewModel)
            {
                return;
            }

            PersistTreeExpansionState();
        }


        private void PersistTreeExpansionState()
        {
            var currentExpandedKeys = EnumerateTreeNodes(TreeNodes)
                .Where(node => node.IsExpanded)
                .Select(node => node.StateKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            var existingExpandedKeys = (_document.TreeViewState?.ExpandedKeys ?? new List<string>())
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();

            if (currentExpandedKeys.SequenceEqual(existingExpandedKeys, StringComparer.Ordinal))
            {
                return;
            }

            _document.TreeViewState ??= new QuickDataTreeViewState();
            _document.TreeViewState.ExpandedKeys = currentExpandedKeys;
            _libraryStore.Save(_document);
        }


        private void ShowHelp()
        {
            var helpText = string.Join(Environment.NewLine,
                "快捷添加数据面板说明：",
                string.Empty,
                "1. 分组节点右键支持添加文件和扫描目录。",
                "2. 右键加载/删除和双击展开默认作用于当前节点。",
                "3. 数据库、要素数据集、要素类和表都可以直接拖到地图；树内重排仅支持分组和顶层收藏项。",
                "4. 面板会尽量保留当前展开状态，坐标系和路径信息通过鼠标停留 tooltip 查看。");

            PresentationServices.Dialogs.Show(helpText, "帮助");
        }


        private void ClearSelection()
        {
            foreach (var node in EnumerateTreeNodes(TreeNodes))
            {
                node.IsSelected = false;
            }
        }


        private static bool HasSelectedAncestor(QuickDataTreeItemViewModel node, IReadOnlyCollection<QuickDataTreeItemViewModel> selectedNodes)
        {
            var current = node.Parent;
            while (current != null)
            {
                if (selectedNodes.Any(item => item.StateKey == current.StateKey))
                {
                    return true;
                }

                current = current.Parent;
            }

            return false;
        }

    }
}
