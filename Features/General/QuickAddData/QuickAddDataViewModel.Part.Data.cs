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

        public IReadOnlyList<QuickDataTreeItemViewModel> GetDragSelection(QuickDataTreeItemViewModel anchorNode)
        {
            if (anchorNode == null || !anchorNode.IsMapDraggable)
            {
                return Array.Empty<QuickDataTreeItemViewModel>();
            }

            if (!anchorNode.IsSelected)
            {
                return new[] { anchorNode };
            }

            var candidates = SelectedNodes
                .Where(node => node.IsMapDraggable)
                .ToList();

            if (candidates.Count == 0)
            {
                return new[] { anchorNode };
            }

            if (anchorNode.IsGroupNode)
            {
                var groups = candidates.Where(node => node.IsGroupNode).ToList();
                return groups.Count > 0 ? groups : new[] { anchorNode };
            }

            var dataNodes = candidates.Where(node => node.NodeKind == QuickDataTreeNodeKind.DataNode).ToList();
            return dataNodes.Count > 0 ? dataNodes : new[] { anchorNode };
        }


        public IReadOnlyList<string> GetDragCatalogPaths(QuickDataTreeItemViewModel node)
        {
            if (node == null)
            {
                return Array.Empty<string>();
            }

            if (node.IsGroupNode)
            {
                return node.Children
                    .SelectMany(GetDragCatalogPaths)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (node.DataNode == null)
            {
                return Array.Empty<string>();
            }

            return QuickDataDragPathCollector.Collect(node.DataNode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }


        private void ReloadLibrary()
        {
            _document = _libraryStore.Load();
            if (QuickDataLibraryHydrator.HydrateGeodatabases(_document, _importService))
            {
                _libraryStore.Save(_document);
            }
            RebuildTree(CreateSnapshotFromDocumentState());
        }


        private void AddFilesToTargetGroup()
        {
            if (IsBusy)
            {
                return;
            }

            ImportFilesIntoGroup(EnsureTargetGroup());
        }


        private void ImportFolderToTargetGroup()
        {
            if (IsBusy)
            {
                return;
            }

            ImportFolderIntoGroup(EnsureTargetGroup());
        }


        private async Task LoadSelectedNodeAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (SelectedNode == null)
            {
                StatusMessage = "请先选择一个节点。";
                return;
            }

            var targets = GetActionableSelection().ToList();
            if (targets.Count == 0)
            {
                StatusMessage = "请先选择要加载的节点。";
                return;
            }

            IsBusy = true;
            try
            {
                var aggregate = new QuickDataLoadResult();
                foreach (var target in targets)
                {
                    var result = await LoadTreeNodeAsync(target);
                    aggregate.AddedCount += result.AddedCount;
                    aggregate.SkippedCount += result.SkippedCount;
                    aggregate.FailedCount += result.FailedCount;
                    aggregate.MissingActiveMap |= result.MissingActiveMap;
                }

                StatusMessage = aggregate.MissingActiveMap
                    ? "没有活动地图。"
                    : $"加载完成，新增 {aggregate.AddedCount}，跳过 {aggregate.SkippedCount}，失败 {aggregate.FailedCount}。";
            }
            finally
            {
                IsBusy = false;
            }
        }


        private Task<QuickDataLoadResult> LoadTreeNodeAsync(QuickDataTreeItemViewModel node)
        {
            return node.NodeKind switch
            {
                QuickDataTreeNodeKind.Group when node.Group != null => _mapLoadService.LoadGroupToCurrentMapAsync(node.Group),
                QuickDataTreeNodeKind.TypeBucket => _mapLoadService.LoadBucketToCurrentMapAsync(
                    node.DisplayName,
                    node.Children
                        .Where(child => child.DataNode != null)
                        .Select(child => child.DataNode!.CloneDeep())
                        .ToList()),
                QuickDataTreeNodeKind.DataNode when node.DataNode != null => _mapLoadService.LoadNodeToCurrentMapAsync(node.DataNode.CloneDeep()),
                _ => Task.FromResult(new QuickDataLoadResult())
            };
        }


        private QuickDataGroup EnsureTargetGroup()
        {
            var targetGroup = SelectedNode?.ResolveGroup();
            if (targetGroup != null)
            {
                return targetGroup;
            }

            targetGroup = _document.Groups.FirstOrDefault(group => string.Equals(group.Name, DefaultGroupName, StringComparison.OrdinalIgnoreCase));
            if (targetGroup == null)
            {
                targetGroup = new QuickDataGroup { Name = DefaultGroupName };
                _document.Groups.Add(targetGroup);
            }

            return targetGroup;
        }


        private IEnumerable<QuickDataTreeItemViewModel> GetActionableSelection()
        {
            var rawSelection = SelectedNodes.ToList();
            if (rawSelection.Count > 0)
            {
                return rawSelection.Where(node => !HasSelectedAncestor(node, rawSelection)).ToList();
            }

            return SelectedNode != null ? new[] { SelectedNode } : Array.Empty<QuickDataTreeItemViewModel>();
        }

    }
}
