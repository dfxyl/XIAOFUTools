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

        private void RebuildTree(TreeViewStateSnapshot snapshot)
        {
            var tree = QuickDataTreeBuilder.Build(_document);
            TreeNodes = new ObservableCollection<QuickDataTreeItemViewModel>(tree.Select(node => new QuickDataTreeItemViewModel(node)));
            AttachTreeNodeStateTracking();
            RestoreTreeViewState(snapshot ?? CreateSnapshotFromDocumentState());
        }


        private void CreateGroup()
        {
            if (IsBusy)
            {
                return;
            }

            var groupName = PromptForText("新建分组", "请输入分组名称：");
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return;
            }

            if (_document.Groups.Any(group => string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase)))
            {
                StatusMessage = $"分组“{groupName}”已存在。";
                return;
            }

            _document.Groups.Add(new QuickDataGroup { Name = groupName });
            PersistLibrary($"已创建分组“{groupName}”。");
        }


        private TreeViewStateSnapshot CreateSnapshotFromDocumentState()
        {
            var snapshot = new TreeViewStateSnapshot();
            foreach (var key in _document.TreeViewState?.ExpandedKeys ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    snapshot.ExpandedKeys.Add(key);
                }
            }

            return snapshot;
        }


        private void ApplySnapshotToDocumentState(TreeViewStateSnapshot snapshot)
        {
            _document.TreeViewState ??= new QuickDataTreeViewState();
            _document.TreeViewState.ExpandedKeys = snapshot.ExpandedKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
        }


        private void ApplySelectionByKeys(HashSet<string> selectedKeys, QuickDataTreeItemViewModel focusedNode)
        {
            foreach (var node in EnumerateTreeNodes(TreeNodes))
            {
                node.IsSelected = selectedKeys.Contains(node.StateKey);
            }

            SelectedNode = focusedNode;
        }

    }
}
