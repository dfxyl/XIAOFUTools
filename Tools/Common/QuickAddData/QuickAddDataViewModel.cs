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

namespace XIAOFUTools.Tools.QuickAddData
{
    internal sealed class QuickAddDataViewModel : PropertyChangedBase
    {
        private const string DefaultGroupName = "默认分组";

        private readonly QuickDataLibraryStore _libraryStore;
        private readonly QuickDataImportService _importService;
        private readonly QuickDataMapLoadService _mapLoadService;
        private readonly QuickAddDataTreeDragDropHandler _treeDragDropHandler;

        private QuickDataLibraryDocument _document;
        private ObservableCollection<QuickDataTreeItemViewModel> _treeNodes;
        private QuickDataTreeItemViewModel _selectedNode;
        private string _selectionAnchorKey;
        private string _statusMessage = "快捷数据面板已就绪。";
        private bool _isBusy;
        private bool _isRestoringTreeState;

        public QuickAddDataViewModel()
        {
            _libraryStore = new QuickDataLibraryStore();
            _importService = new QuickDataImportService(new ArcGisQuickDataGeodatabaseInspector());
            _mapLoadService = new QuickDataMapLoadService();
            _treeDragDropHandler = new QuickAddDataTreeDragDropHandler(this);
            _treeNodes = new ObservableCollection<QuickDataTreeItemViewModel>();
            _document = new QuickDataLibraryDocument();

            CreateGroupCommand = new RelayCommand(CreateGroup);
            RenameSelectedCommand = new RelayCommand(RenameSelected);
            AddFilesCommand = new RelayCommand(AddFilesToTargetGroup);
            AddFilesToSelectedGroupCommand = new RelayCommand(AddFilesToSelectedGroup);
            ImportFolderCommand = new RelayCommand(ImportFolderToTargetGroup);
            ImportToSelectedGroupCommand = new RelayCommand(ImportFolderToSelectedGroup);
            DeleteSelectedCommand = new RelayCommand(DeleteSelected);
            RefreshCommand = new RelayCommand(RefreshLibraryOrSelectedNode);
            RefreshSelectedDatabaseCommand = new RelayCommand(async () => await RefreshSelectedDatabaseAsync());
            LoadSelectedNodeCommand = new RelayCommand(async () => await LoadSelectedNodeAsync());
            ShowHelpCommand = new RelayCommand(ShowHelp);

            ReloadLibrary();
        }

        public ObservableCollection<QuickDataTreeItemViewModel> TreeNodes
        {
            get => _treeNodes;
            set => SetProperty(ref _treeNodes, value);
        }

        public QuickDataTreeItemViewModel SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ICommand CreateGroupCommand { get; }

        public ICommand RenameSelectedCommand { get; }

        public ICommand AddFilesCommand { get; }

        public ICommand AddFilesToSelectedGroupCommand { get; }

        public ICommand ImportFolderCommand { get; }

        public ICommand ImportToSelectedGroupCommand { get; }

        public ICommand DeleteSelectedCommand { get; }

        public ICommand RefreshCommand { get; }

        public ICommand RefreshSelectedDatabaseCommand { get; }

        public ICommand LoadSelectedNodeCommand { get; }

        public ICommand ShowHelpCommand { get; }

        public QuickAddDataTreeDragDropHandler TreeDragDropHandler => _treeDragDropHandler;

        public IReadOnlyList<QuickDataTreeItemViewModel> SelectedNodes => EnumerateTreeNodes(TreeNodes)
            .Where(node => node.IsSelected)
            .ToList();

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

        private void ReloadLibrary()
        {
            _document = _libraryStore.Load();
            if (QuickDataLibraryHydrator.HydrateGeodatabases(_document, _importService))
            {
                _libraryStore.Save(_document);
            }
            RebuildTree(CreateSnapshotFromDocumentState());
        }

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

        private void AddFilesToTargetGroup()
        {
            if (IsBusy)
            {
                return;
            }

            ImportFilesIntoGroup(EnsureTargetGroup());
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

        private void ImportFolderToTargetGroup()
        {
            if (IsBusy)
            {
                return;
            }

            ImportFolderIntoGroup(EnsureTargetGroup());
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

        private void ImportFilesIntoGroup(QuickDataGroup targetGroup)
        {
            var dialog = new OpenFileDialog
            {
                Title = "选择要加入快捷库的数据文件",
                Multiselect = true,
                CheckFileExists = true,
                Filter = "GIS数据|*.shp;*.lyr;*.lyrx;*.tif;*.tiff;*.img;*.jpg;*.jpeg;*.png;*.bmp|全部文件|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            ImportIntoGroup(targetGroup, dialog.FileNames, "文件");
        }

        private void ImportFolderIntoGroup(QuickDataGroup targetGroup)
        {
            var folderPath = XIAOFUTools.Common.PathDialogUtils.PickFolder("选择要导入的文件夹或 GDB");
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }

            ImportIntoGroup(targetGroup, new[] { folderPath }, "目录");
        }

        private void ImportIntoGroup(QuickDataGroup targetGroup, IEnumerable<string> inputPaths, string originLabel)
        {
            var importedNodes = _importService.ImportPaths(inputPaths, new QuickDataImportOptions
            {
                IncludeFeatureClasses = true,
                IncludeTables = true,
                IncludeRasters = true,
                IncludeLayerFiles = true,
                Recurse = true
            });

            if (importedNodes.Count == 0)
            {
                StatusMessage = $"没有从{originLabel}中识别出可加入快捷库的数据。";
                return;
            }

            var detector = new QuickDataDuplicateDetector(EnumerateNodes(targetGroup.Nodes).Select(node => node.UniqueKey));
            var addedCount = 0;
            var skippedCount = 0;

            foreach (var node in importedNodes.OrderBy(node => node.Name))
            {
                if (detector.TryRegister(node))
                {
                    targetGroup.Nodes.Add(node.CloneDeep());
                    addedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            PersistLibrary($"导入完成，新增 {addedCount}，跳过 {skippedCount}。");
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
                var refreshed = _importService.ImportPaths(
                    new[] { SelectedNode.DataNode.SourcePath },
                    new QuickDataImportOptions
                    {
                        IncludeFeatureClasses = true,
                        IncludeTables = true,
                        IncludeRasters = false,
                        IncludeLayerFiles = false,
                        Recurse = false
                    })
                    .FirstOrDefault();

                SelectedNode.DataNode.Children = refreshed?.Children ?? new List<QuickDataNode>();
                PersistLibrary($"已刷新数据库“{SelectedNode.DataNode.Name}”的子项。");
            }
            finally
            {
                IsBusy = false;
            }
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

        private static string PromptForText(string title, string prompt, string initialValue = "")
        {
            var dialog = new QuickDataTextInputDialog(title, prompt, initialValue);
            return dialog.ShowDialog() == true ? dialog.ResponseText : null;
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

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpText, "帮助");
        }

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

        private void ApplySelectionByKeys(HashSet<string> selectedKeys, QuickDataTreeItemViewModel focusedNode)
        {
            foreach (var node in EnumerateTreeNodes(TreeNodes))
            {
                node.IsSelected = selectedKeys.Contains(node.StateKey);
            }

            SelectedNode = focusedNode;
        }

        private void ClearSelection()
        {
            foreach (var node in EnumerateTreeNodes(TreeNodes))
            {
                node.IsSelected = false;
            }
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

        private sealed class TreeViewStateSnapshot
        {
            public HashSet<string> ExpandedKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

            public HashSet<string> SelectedKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

            public string AnchorKey { get; set; }

            public string SelectedNodeKey { get; set; }
        }
    }
}
