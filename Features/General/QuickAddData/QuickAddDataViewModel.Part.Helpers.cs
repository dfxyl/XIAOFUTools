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

        public bool CanDropTreeNode(QuickDataTreeItemViewModel draggedNode, QuickDataTreeItemViewModel targetNode)
        {
            if (draggedNode == null || targetNode == null || ReferenceEquals(draggedNode, targetNode))
            {
                return false;
            }

            if (!draggedNode.IsTreeReorderable || !targetNode.IsTreeReorderable)
            {
                return false;
            }

            if (draggedNode.IsGroupNode)
            {
                return targetNode.IsGroupNode;
            }

            if (draggedNode.IsTopLevelDataNode)
            {
                return targetNode.IsGroupNode || targetNode.IsTopLevelDataNode;
            }

            return false;
        }


        public bool MoveTreeNode(QuickDataTreeItemViewModel draggedNode, QuickDataTreeItemViewModel targetNode)
        {
            if (!CanDropTreeNode(draggedNode, targetNode))
            {
                return false;
            }

            if (draggedNode.IsGroupNode && draggedNode.Group != null && targetNode.Group != null)
            {
                var targetIndex = _document.Groups.IndexOf(targetNode.Group);
                if (QuickDataLibraryOrganizer.MoveGroup(_document, draggedNode.Group, targetIndex))
                {
                    PersistLibrary($"已移动分组“{draggedNode.Group.Name}”。");
                    return true;
                }

                return false;
            }

            if (draggedNode.IsTopLevelDataNode && draggedNode.DataNode != null)
            {
                var sourceGroup = draggedNode.ResolveGroup();
                if (sourceGroup == null)
                {
                    return false;
                }

                QuickDataGroup targetGroup;
                int targetIndex;

                if (targetNode.IsGroupNode && targetNode.Group != null)
                {
                    targetGroup = targetNode.Group;
                    targetIndex = targetGroup.Nodes.Count;
                }
                else if (targetNode.IsTopLevelDataNode && targetNode.DataNode != null)
                {
                    targetGroup = targetNode.ResolveGroup();
                    if (targetGroup == null)
                    {
                        return false;
                    }

                    targetIndex = QuickDataLibraryOrganizer.GetInsertionIndex(targetGroup, targetNode.DataNode);
                }
                else
                {
                    return false;
                }

                if (QuickDataLibraryOrganizer.MoveTopLevelNode(sourceGroup, draggedNode.DataNode, targetGroup, targetIndex))
                {
                    PersistLibrary($"已移动“{draggedNode.DataNode.Name}”。");
                    return true;
                }
            }

            return false;
        }


        public bool MoveTreeNodes(IReadOnlyList<QuickDataTreeItemViewModel> draggedNodes, QuickDataTreeItemViewModel targetNode)
        {
            if (draggedNodes == null || draggedNodes.Count == 0 || targetNode == null)
            {
                return false;
            }

            if (draggedNodes.Count == 1)
            {
                return MoveTreeNode(draggedNodes[0], targetNode);
            }

            if (draggedNodes.All(node => node.IsGroupNode) && targetNode.IsGroupNode && targetNode.Group != null)
            {
                var groups = draggedNodes.Select(node => node.Group).Where(group => group != null).Distinct().ToList();
                if (groups.Count == 0)
                {
                    return false;
                }

                var targetIndex = _document.Groups.IndexOf(targetNode.Group);
                foreach (var group in groups)
                {
                    QuickDataLibraryOrganizer.MoveGroup(_document, group, targetIndex++);
                }

                PersistLibrary($"已移动 {groups.Count} 个分组。");
                return true;
            }

            if (draggedNodes.All(node => node.IsTopLevelDataNode))
            {
                var sourceGroups = draggedNodes.Select(node => node.ResolveGroup()).Distinct().ToList();
                if (sourceGroups.Count != 1 || sourceGroups[0] == null)
                {
                    return false;
                }

                var sourceGroup = sourceGroups[0];
                QuickDataGroup targetGroup;
                int targetIndex;

                if (targetNode.IsGroupNode && targetNode.Group != null)
                {
                    targetGroup = targetNode.Group;
                    targetIndex = targetGroup.Nodes.Count;
                }
                else if (targetNode.IsTopLevelDataNode && targetNode.DataNode != null)
                {
                    targetGroup = targetNode.ResolveGroup();
                    if (targetGroup == null)
                    {
                        return false;
                    }

                    targetIndex = QuickDataLibraryOrganizer.GetInsertionIndex(targetGroup, targetNode.DataNode);
                }
                else
                {
                    return false;
                }

                var dataNodes = draggedNodes.Select(node => node.DataNode).Where(node => node != null).ToList();
                if (QuickDataLibraryOrganizer.MoveTopLevelNodes(sourceGroup, dataNodes!, targetGroup, targetIndex))
                {
                    PersistLibrary($"已移动 {dataNodes.Count} 个收藏项。");
                    return true;
                }
            }

            return false;
        }


        private void PersistLibrary(string statusMessage, bool clearSelection = false)
        {
            var snapshot = CaptureTreeViewState();
            if (clearSelection)
            {
                snapshot.SelectedKeys.Clear();
                snapshot.AnchorKey = null;
                snapshot.SelectedNodeKey = null;
            }

            ApplySnapshotToDocumentState(snapshot);
            _libraryStore.Save(_document);
            RebuildTree(snapshot);
            StatusMessage = statusMessage;
        }


        private void AttachTreeNodeStateTracking()
        {
            foreach (var node in EnumerateTreeNodes(TreeNodes))
            {
                node.PropertyChanged += OnTreeNodePropertyChanged;
            }
        }


        private static IEnumerable<QuickDataNode> EnumerateNodes(IEnumerable<QuickDataNode> nodes)
        {
            foreach (var node in nodes ?? Enumerable.Empty<QuickDataNode>())
            {
                yield return node;

                foreach (var child in EnumerateNodes(node.Children))
                {
                    yield return child;
                }
            }
        }


        private static IEnumerable<QuickDataTreeItemViewModel> EnumerateTreeNodes(IEnumerable<QuickDataTreeItemViewModel> nodes)
        {
            foreach (var node in nodes ?? Enumerable.Empty<QuickDataTreeItemViewModel>())
            {
                yield return node;

                foreach (var child in EnumerateTreeNodes(node.Children))
                {
                    yield return child;
                }
            }
        }


        private static IEnumerable<QuickDataTreeItemViewModel> EnumerateVisibleTreeNodes(IEnumerable<QuickDataTreeItemViewModel> nodes)
        {
            foreach (var node in nodes ?? Enumerable.Empty<QuickDataTreeItemViewModel>())
            {
                yield return node;

                if (!node.IsExpanded)
                {
                    continue;
                }

                foreach (var child in EnumerateVisibleTreeNodes(node.Children))
                {
                    yield return child;
                }
            }
        }


        private static string PromptForText(string title, string prompt, string initialValue = "") =>
            XIAOFUTools.Shared.Presentation.Dialogs.TextInputDialog.Prompt(title, prompt, initialValue);


        private TreeViewStateSnapshot CaptureTreeViewState()
        {
            var snapshot = new TreeViewStateSnapshot();
            foreach (var node in EnumerateTreeNodes(TreeNodes))
            {
                if (node.IsExpanded)
                {
                    snapshot.ExpandedKeys.Add(node.StateKey);
                }

                if (node.IsSelected)
                {
                    snapshot.SelectedKeys.Add(node.StateKey);
                }
            }

            snapshot.AnchorKey = _selectionAnchorKey;
            snapshot.SelectedNodeKey = SelectedNode?.StateKey;
            return snapshot;
        }


        private void RestoreTreeViewState(TreeViewStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                SelectedNode = null;
                _selectionAnchorKey = null;
                return;
            }

            _isRestoringTreeState = true;
            try
            {
                QuickDataTreeItemViewModel selectedNode = null;
                foreach (var node in EnumerateTreeNodes(TreeNodes))
                {
                    node.IsExpanded = snapshot.ExpandedKeys.Contains(node.StateKey);
                    node.IsSelected = snapshot.SelectedKeys.Contains(node.StateKey);

                    if (selectedNode == null && node.StateKey == snapshot.SelectedNodeKey)
                    {
                        selectedNode = node;
                    }
                }

                SelectedNode = selectedNode ?? EnumerateTreeNodes(TreeNodes).FirstOrDefault(node => node.IsSelected);
                _selectionAnchorKey = snapshot.AnchorKey;
            }
            finally
            {
                _isRestoringTreeState = false;
            }
        }

    }
}
