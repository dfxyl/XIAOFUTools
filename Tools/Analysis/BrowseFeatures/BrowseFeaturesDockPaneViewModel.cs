using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using XIAOFUTools.Common;
using XIAOFUTools.Tools.Settings;

namespace XIAOFUTools.Tools.BrowseFeatures
{
    internal sealed class BrowseFeaturesDockPaneViewModel : PropertyChangedBase
    {
        internal sealed class ScopeModeOption
        {
            public BrowseScopeMode Value { get; init; }
            public string DisplayName { get; init; } = string.Empty;
            public override string ToString() => DisplayName;
        }

        internal sealed class SortModeOption
        {
            public BrowseSortMode Value { get; init; }
            public string DisplayName { get; init; } = string.Empty;
            public override string ToString() => DisplayName;
        }

        internal sealed class SortFieldOption
        {
            public string Name { get; init; } = string.Empty;
            public string Alias { get; init; } = string.Empty;
            public FieldType FieldType { get; init; }
            public string DisplayName => string.IsNullOrWhiteSpace(Alias) || string.Equals(Name, Alias, StringComparison.Ordinal)
                ? Name
                : $"{Name}({Alias})";
            public override string ToString() => DisplayName;
        }

        private sealed class CurrentFeatureContext
        {
            public long ObjectId { get; init; }
            public Geometry Geometry { get; init; }
            public string GeometryType { get; init; } = string.Empty;
            public string GeometryWkt { get; init; } = string.Empty;
            public IReadOnlyList<Envelope> PartEnvelopes { get; init; } = Array.Empty<Envelope>();
        }

        internal sealed class SnapshotListItem : PropertyChangedBase
        {
            public int DisplayIndex { get; init; }
            public long ObjectId { get; init; }

            private string _reviewStatusText = "未判定";
            private string _notesPreview = string.Empty;

            public string ReviewStatusText
            {
                get => _reviewStatusText;
                set
                {
                    if (SetProperty(ref _reviewStatusText, value))
                    {
                        NotifyPropertyChanged(() => InlineSummary);
                    }
                }
            }

            public string NotesPreview
            {
                get => _notesPreview;
                set
                {
                    if (SetProperty(ref _notesPreview, value))
                    {
                        NotifyPropertyChanged(() => InlineSummary);
                    }
                }
            }

            public string DisplayText => $"{DisplayIndex:D5} | OID {ObjectId}";

            public string InlineSummary => string.IsNullOrWhiteSpace(NotesPreview)
                ? $"{DisplayText} | {ReviewStatusText}"
                : $"{DisplayText} | {ReviewStatusText} | {NotesPreview}";
        }

        private static readonly object OverlayLock = new object();
        private static readonly List<IDisposable> ActiveOverlays = new List<IDisposable>();
        private static long _flashVersion;
        private static string _lastReviewerName = string.Empty;
        private const string ReviewTableNamePrefix = "BrowseFeaturesReview_";

        private readonly BrowseFeaturesReviewStore _reviewStore = new BrowseFeaturesReviewStore();
        private readonly ObservableCollection<FeatureLayer> _featureLayers = new ObservableCollection<FeatureLayer>();
        private readonly ObservableCollection<ScopeModeOption> _scopeModes = new ObservableCollection<ScopeModeOption>();
        private readonly ObservableCollection<SortModeOption> _sortModes = new ObservableCollection<SortModeOption>();
        private readonly ObservableCollection<SortFieldOption> _sortFields = new ObservableCollection<SortFieldOption>();
        private readonly ObservableCollection<string> _reviewStatusOptions = new ObservableCollection<string>(BrowseFeaturesCore.ReviewStatusOptions);
        private readonly ObservableCollection<SnapshotListItem> _snapshotList = new ObservableCollection<SnapshotListItem>();

        private readonly List<FeatureSnapshotItem> _snapshotItems = new List<FeatureSnapshotItem>();

        private FeatureLayer _selectedLayer;
        private ScopeModeOption _selectedScopeMode;
        private SortModeOption _selectedSortMode;
        private SortFieldOption _selectedSortField;
        private string _selectedReviewStatus = "未判定";
        private string _currentNotes = string.Empty;
        private string _reviewerName = string.Empty;
        private string _batchId = BrowseFeaturesCore.CreateDefaultBatchId(DateTime.Now);
        private string _outputGdbPath = Project.Current?.DefaultGeodatabasePath ?? string.Empty;
        private string _outputTableName = $"BrowseFeaturesReview_{BrowseFeaturesCore.CreateDefaultBatchId(DateTime.Now)}";
        private string _logContent = string.Empty;
        private string _statusMessage = "就绪";
        private string _snapshotInfo = "0/0";
        private string _currentOidText = "-";
        private string _partInfoText = "-";
        private string _staleHint = string.Empty;
        private bool _isSnapshotStale;
        private bool _sortDescending;
        private bool _isBusy;

        private int _currentIndex = -1;
        private int _currentPartIndex = 0;
        private CurrentFeatureContext _currentFeature;
        private DateTime _currentFeatureVisitedAt = DateTime.MinValue;
        private bool _reviewTableReady;
        private string _reviewTableSignature = string.Empty;
        private bool _hasExplicitOutputTableName;
        private bool _hasExplicitBatchId;
        private bool _suppressSnapshotListSelectionChanged;
        private bool _suppressReviewPropertyChanged;
        private bool _isPersistingCurrentRecord;
        private dynamic _selectionChangedToken;

        private readonly SimpleRelayCommand _refreshLayersCommand;
        private readonly SimpleRelayCommand _refreshSnapshotCommand;
        private readonly SimpleRelayCommand _browseOutputGdbCommand;
        private readonly SimpleRelayCommand _firstFeatureCommand;
        private readonly SimpleRelayCommand _previousFeatureCommand;
        private readonly SimpleRelayCommand _nextFeatureCommand;
        private readonly SimpleRelayCommand _lastFeatureCommand;
        private readonly SimpleRelayCommand _previousPartCommand;
        private readonly SimpleRelayCommand _nextPartCommand;
        private readonly SimpleRelayCommand _openSettingsCommand;
        private readonly SimpleRelayCommand _showHelpCommand;

        private bool HasSnapshot => _snapshotItems.Count > 0;

        public BrowseFeaturesDockPaneViewModel()
        {
            LoadSettings();

            _scopeModes.Add(new ScopeModeOption { Value = BrowseScopeMode.Selection, DisplayName = "选择集" });
            _scopeModes.Add(new ScopeModeOption { Value = BrowseScopeMode.All, DisplayName = "全量要素" });
            _selectedScopeMode = _scopeModes.First();

            _sortModes.Add(new SortModeOption { Value = BrowseSortMode.ObjectId, DisplayName = "OID排序" });
            _sortModes.Add(new SortModeOption { Value = BrowseSortMode.Field, DisplayName = "字段排序" });
            _selectedSortMode = _sortModes.First();

            if (!string.IsNullOrWhiteSpace(_lastReviewerName) && string.IsNullOrWhiteSpace(_reviewerName))
            {
                _reviewerName = _lastReviewerName;
            }

            _refreshLayersCommand = new SimpleRelayCommand(async () => await RefreshLayersAsync(), () => !IsBusy);
            _refreshSnapshotCommand = new SimpleRelayCommand(async () => await RefreshSnapshotAsync(), () => !IsBusy && SelectedLayer != null);
            _browseOutputGdbCommand = new SimpleRelayCommand(BrowseOutputGeodatabase, () => !IsBusy);
            _firstFeatureCommand = new SimpleRelayCommand(async () => await NavigateToIndexAsync(0), CanGoFirst);
            _previousFeatureCommand = new SimpleRelayCommand(async () => await NavigateToIndexAsync(_currentIndex - 1), CanGoPrevious);
            _nextFeatureCommand = new SimpleRelayCommand(async () => await NavigateToIndexAsync(_currentIndex + 1), CanGoNext);
            _lastFeatureCommand = new SimpleRelayCommand(async () => await NavigateToIndexAsync(_snapshotItems.Count - 1), CanGoLast);
            _previousPartCommand = new SimpleRelayCommand(async () => await NavigatePartAsync(-1), CanGoPreviousPart);
            _nextPartCommand = new SimpleRelayCommand(async () => await NavigatePartAsync(1), CanGoNextPart);
            _openSettingsCommand = new SimpleRelayCommand(OpenSettings);
            _showHelpCommand = new SimpleRelayCommand(ShowHelp);

            _selectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
            _ = RefreshLayersAsync();
        }

        public ObservableCollection<FeatureLayer> FeatureLayers => _featureLayers;

        public ObservableCollection<ScopeModeOption> ScopeModes => _scopeModes;

        public ObservableCollection<SortModeOption> SortModes => _sortModes;

        public ObservableCollection<SortFieldOption> SortFields => _sortFields;

        public ObservableCollection<string> ReviewStatusOptions => _reviewStatusOptions;

        public ObservableCollection<SnapshotListItem> SnapshotList => _snapshotList;

        public FeatureLayer SelectedLayer
        {
            get => _selectedLayer;
            set
            {
                var previousLayerUri = _selectedLayer?.URI;
                if (SetProperty(ref _selectedLayer, value))
                {
                    var currentLayerUri = _selectedLayer?.URI;
                    if (!string.Equals(previousLayerUri, currentLayerUri, StringComparison.Ordinal))
                    {
                        MarkSnapshotStale("目标图层已变更，请刷新快照。");
                    }
                    _ = RefreshSortFieldsAsync();
                    _ = AutoResolveReviewStorageAsync();
                    RaiseCommandStates();
                }
            }
        }

        public ScopeModeOption SelectedScopeMode
        {
            get => _selectedScopeMode;
            set
            {
                if (SetProperty(ref _selectedScopeMode, value))
                {
                    MarkSnapshotStale("遍历范围已变更，请刷新快照。");
                    RaiseCommandStates();
                }
            }
        }

        public SortModeOption SelectedSortMode
        {
            get => _selectedSortMode;
            set
            {
                if (SetProperty(ref _selectedSortMode, value))
                {
                    MarkSnapshotStale("排序方式已变更，请刷新快照。");
                    NotifyPropertyChanged(() => IsFieldSortMode);
                    RaiseCommandStates();
                }
            }
        }

        public SortFieldOption SelectedSortField
        {
            get => _selectedSortField;
            set
            {
                if (SetProperty(ref _selectedSortField, value))
                {
                    if (IsFieldSortMode && HasSnapshot)
                    {
                        MarkSnapshotStale("排序字段已变更，请刷新快照。");
                    }
                }
            }
        }

        public bool IsFieldSortMode => SelectedSortMode?.Value == BrowseSortMode.Field;

        public bool SortDescending
        {
            get => _sortDescending;
            set
            {
                if (SetProperty(ref _sortDescending, value))
                {
                    MarkSnapshotStale("排序方向已变更，请刷新快照。");
                }
            }
        }

        public string SelectedReviewStatus
        {
            get => _selectedReviewStatus;
            set
            {
                if (SetProperty(ref _selectedReviewStatus, value))
                {
                    if (!_suppressReviewPropertyChanged)
                    {
                        _ = PersistCurrentReviewRecordAsync();
                    }
                }
            }
        }

        public string CurrentNotes
        {
            get => _currentNotes;
            set
            {
                if (SetProperty(ref _currentNotes, value))
                {
                    if (!_suppressReviewPropertyChanged)
                    {
                        _ = PersistCurrentReviewRecordAsync();
                    }
                }
            }
        }

        private SnapshotListItem _selectedSnapshotItem;

        public SnapshotListItem SelectedSnapshotItem
        {
            get => _selectedSnapshotItem;
            set
            {
                if (SetProperty(ref _selectedSnapshotItem, value))
                {
                    if (!_suppressSnapshotListSelectionChanged && value != null)
                    {
                        _ = NavigateToSnapshotItemAsync(value);
                    }
                }
            }
        }

        public string ReviewerName
        {
            get => _reviewerName;
            set
            {
                if (SetProperty(ref _reviewerName, value))
                {
                    _lastReviewerName = value ?? string.Empty;
                }
            }
        }

        public string BatchId
        {
            get => _batchId;
            set
            {
                SetProperty(ref _batchId, value);
            }
        }

        public string OutputGdbPath
        {
            get => _outputGdbPath;
            set
            {
                if (SetProperty(ref _outputGdbPath, value))
                {
                    ResetReviewTableState();
                }
            }
        }

        public string OutputTableName
        {
            get => _outputTableName;
            set
            {
                if (SetProperty(ref _outputTableName, value))
                {
                    ResetReviewTableState();
                }
            }
        }

        public string LogContent
        {
            get => _logContent;
            private set => SetProperty(ref _logContent, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public string SnapshotInfo
        {
            get => _snapshotInfo;
            private set => SetProperty(ref _snapshotInfo, value);
        }

        public string CurrentOidText
        {
            get => _currentOidText;
            private set => SetProperty(ref _currentOidText, value);
        }

        public string PartInfoText
        {
            get => _partInfoText;
            private set => SetProperty(ref _partInfoText, value);
        }

        public string StaleHint
        {
            get => _staleHint;
            private set => SetProperty(ref _staleHint, value);
        }

        public bool IsSnapshotStale
        {
            get => _isSnapshotStale;
            private set => SetProperty(ref _isSnapshotStale, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    RaiseCommandStates();
                }
            }
        }

        public SimpleRelayCommand RefreshLayersCommand => _refreshLayersCommand;
        public SimpleRelayCommand RefreshSnapshotCommand => _refreshSnapshotCommand;
        public SimpleRelayCommand BrowseOutputGdbCommand => _browseOutputGdbCommand;
        public SimpleRelayCommand FirstFeatureCommand => _firstFeatureCommand;
        public SimpleRelayCommand PreviousFeatureCommand => _previousFeatureCommand;
        public SimpleRelayCommand NextFeatureCommand => _nextFeatureCommand;
        public SimpleRelayCommand LastFeatureCommand => _lastFeatureCommand;
        public SimpleRelayCommand PreviousPartCommand => _previousPartCommand;
        public SimpleRelayCommand NextPartCommand => _nextPartCommand;
        public SimpleRelayCommand OpenSettingsCommand => _openSettingsCommand;
        public SimpleRelayCommand ShowHelpCommand => _showHelpCommand;
        public async Task RefreshLayersAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            try
            {
                var layers = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        return new List<FeatureLayer>();
                    }

                    return map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .Where(IsSupportedLayer)
                        .OrderBy(layer => layer.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                });

                var previousLayerUri = SelectedLayer?.URI;
                _featureLayers.Clear();
                foreach (var layer in layers)
                {
                    _featureLayers.Add(layer);
                }

                SelectedLayer = _featureLayers.FirstOrDefault(layer => string.Equals(layer.URI, previousLayerUri, StringComparison.Ordinal)) ??
                                _featureLayers.FirstOrDefault();

                await AutoResolveReviewStorageAsync();

                if (SelectedLayer == null)
                {
                    ClearSnapshotState("未找到可用要素图层。");
                }
            }
            catch (Exception ex)
            {
                LogError($"刷新图层失败: {ex.Message}");
                ClearSnapshotState("刷新图层失败。");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void Cleanup()
        {
            if (_selectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_selectionChangedToken);
                _selectionChangedToken = null;
            }

            Interlocked.Increment(ref _flashVersion);
            _ = QueuedTask.Run(ClearOverlays);
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings.BrowseFeatures;
            _reviewerName = settings.ReviewerName ?? string.Empty;
            _outputGdbPath = settings.OutputGdbPath ?? string.Empty;
            _outputTableName = settings.OutputTableName ?? string.Empty;
            _batchId = settings.BatchId ?? string.Empty;
            _hasExplicitOutputTableName = !string.IsNullOrWhiteSpace(_outputTableName);
            _hasExplicitBatchId = !string.IsNullOrWhiteSpace(_batchId);
        }

        private void SaveSettings()
        {
            var settings = SettingsManager.Settings.BrowseFeatures;
            settings.ReviewerName = ReviewerName ?? string.Empty;
            settings.OutputGdbPath = OutputGdbPath ?? string.Empty;
            settings.OutputTableName = OutputTableName ?? string.Empty;
            settings.BatchId = BatchId ?? string.Empty;
            SettingsManager.SaveSettings();
        }

        private void OpenSettings()
        {
            var window = new BrowseFeaturesSettingsWindow(SelectedLayer);
            if (window.ShowDialog() == true)
            {
                LoadSettings();
                ResetReviewTableState();
                NotifyPropertyChanged(() => ReviewerName);
                StatusMessage = "浏览要素设置已更新。";
            }
        }

        private async Task AutoResolveReviewStorageAsync()
        {
            var gdbPath = GetEffectiveOutputGdbPath();
            if (string.IsNullOrWhiteSpace(gdbPath))
            {
                return;
            }

            try
            {
                if (!_hasExplicitOutputTableName)
                {
                    var compatibleTables = await QueuedTask.Run(() =>
                        _reviewStore.GetCompatibleTableNames(gdbPath, ReviewTableNamePrefix));
                    var layerUri = SelectedLayer?.URI ?? string.Empty;
                    var projectKey = BuildCurrentProjectKey();
                    string matchedTableName = null;
                    foreach (var tableName in compatibleTables)
                    {
                        var batchId = await QueuedTask.Run(() =>
                            _reviewStore.TryGetLatestBatchId(gdbPath, tableName, projectKey, layerUri));
                        if (!string.IsNullOrWhiteSpace(batchId))
                        {
                            matchedTableName = tableName;
                            break;
                        }
                    }

                    _outputTableName = matchedTableName ?? compatibleTables.FirstOrDefault() ?? $"{ReviewTableNamePrefix}{BrowseFeaturesCore.CreateDefaultBatchId(DateTime.Now)}";
                    NotifyPropertyChanged(() => OutputTableName);
                }

                if (!_hasExplicitBatchId)
                {
                    var layerUri = SelectedLayer?.URI;
                    if (!string.IsNullOrWhiteSpace(layerUri) && !string.IsNullOrWhiteSpace(_outputTableName))
                    {
                        var latestBatchId = await QueuedTask.Run(() =>
                            _reviewStore.TryGetLatestBatchId(gdbPath, _outputTableName, BuildCurrentProjectKey(), layerUri));
                        _batchId = latestBatchId ?? BrowseFeaturesCore.CreateDefaultBatchId(DateTime.Now);
                        NotifyPropertyChanged(() => BatchId);
                    }
                    else if (string.IsNullOrWhiteSpace(_batchId))
                    {
                        _batchId = BrowseFeaturesCore.CreateDefaultBatchId(DateTime.Now);
                        NotifyPropertyChanged(() => BatchId);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"自动识别审阅表失败: {ex.Message}");
            }
        }

        public async Task RefreshSnapshotAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (SelectedLayer == null)
            {
                MessageBox.Show("请先选择目标要素图层。", "提示");
                return;
            }

            if (IsFieldSortMode && SelectedSortField == null)
            {
                MessageBox.Show("请选择排序字段。", "提示");
                return;
            }

            IsBusy = true;
            try
            {
                if (!await EnsureReviewTableReadyAsync())
                {
                    return;
                }

                var snapshotResult = await BuildSnapshotAsync();
                var snapshot = snapshotResult.Items;
                _snapshotItems.Clear();
                _snapshotItems.AddRange(snapshot);
                RebuildSnapshotList();
                _currentIndex = -1;
                _currentFeature = null;
                _currentPartIndex = 0;
                _currentFeatureVisitedAt = DateTime.MinValue;

                if (_snapshotItems.Count == 0)
                {
                    ClearSnapshotState("快照为空。");
                    LogInfo("未生成可浏览要素（可能为选择集为空或图层无要素）。");
                    return;
                }

                if (snapshotResult.UsedFallbackSort)
                {
                    LogInfo("排序字段不可用，已回退到 OID 升序。");
                }

                IsSnapshotStale = false;
                StaleHint = string.Empty;
                StatusMessage = $"已加载 {_snapshotItems.Count} 条要素。";
                var resumeObjectId = await TryGetResumeObjectIdAsync();
                var resumeIndex = resumeObjectId.HasValue
                    ? _snapshotItems.FindIndex(item => item.ObjectId == resumeObjectId.Value)
                    : -1;
                await NavigateToIndexAsync(resumeIndex >= 0 ? resumeIndex : 0, persistCurrentBeforeNavigate: false);
            }
            catch (Exception ex)
            {
                LogError($"刷新快照失败: {ex.Message}");
                ClearSnapshotState("刷新快照失败。");
            }
            finally
            {
                IsBusy = false;
                RaiseCommandStates();
            }
        }

        private async Task NavigateToSnapshotItemAsync(SnapshotListItem item)
        {
            if (item == null)
            {
                return;
            }

            var index = _snapshotItems.FindIndex(snapshot => snapshot.ObjectId == item.ObjectId);
            if (index >= 0 && index != _currentIndex)
            {
                await NavigateToIndexAsync(index);
            }
        }

        private void RebuildSnapshotList()
        {
            _snapshotList.Clear();
            for (var i = 0; i < _snapshotItems.Count; i++)
            {
                _snapshotList.Add(new SnapshotListItem
                {
                    DisplayIndex = i + 1,
                    ObjectId = _snapshotItems[i].ObjectId
                });
            }
        }

        private async Task<long?> TryGetResumeObjectIdAsync()
        {
            if (!_reviewTableReady || SelectedLayer == null)
            {
                return null;
            }

            try
            {
                var batchId = BatchId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    return null;
                }

                return await QueuedTask.Run(() =>
                    _reviewStore.TryGetLatestObjectId(
                        GetEffectiveOutputGdbPath(),
                        OutputTableName,
                        BuildCurrentProjectKey(),
                        batchId,
                        SelectedLayer.URI ?? string.Empty));
            }
            catch (Exception ex)
            {
                LogError($"恢复上次浏览位置失败: {ex.Message}");
                return null;
            }
        }

        public bool HandleDirectionKey(System.Windows.Input.Key key)
        {
            if (IsBusy || _snapshotItems.Count == 0)
            {
                return false;
            }

            if (key == System.Windows.Input.Key.Left || key == System.Windows.Input.Key.Up)
            {
                if (CanGoPrevious())
                {
                    _ = NavigateToIndexAsync(_currentIndex - 1);
                    return true;
                }
            }
            else if (key == System.Windows.Input.Key.Right || key == System.Windows.Input.Key.Down)
            {
                if (CanGoNext())
                {
                    _ = NavigateToIndexAsync(_currentIndex + 1);
                    return true;
                }
            }

            return false;
        }

        private async Task RefreshSortFieldsAsync()
        {
            _sortFields.Clear();
            _selectedSortField = null;
            NotifyPropertyChanged(() => SelectedSortField);

            if (SelectedLayer == null)
            {
                return;
            }

            try
            {
                var fields = await QueuedTask.Run(() =>
                {
                    using var table = SelectedLayer.GetTable();
                    var definition = table?.GetDefinition();
                    if (definition == null)
                    {
                        return new List<SortFieldOption>();
                    }

                    return definition.GetFields()
                        .Where(field => IsSortableField(field.FieldType))
                        .Where(field => !IsSystemField(field.Name))
                        .OrderBy(field => field.Name, StringComparer.CurrentCultureIgnoreCase)
                        .Select(field => new SortFieldOption
                        {
                            Name = field.Name,
                            Alias = field.AliasName,
                            FieldType = field.FieldType
                        })
                        .ToList();
                });

                foreach (var field in fields)
                {
                    _sortFields.Add(field);
                }

                SelectedSortField = _sortFields.FirstOrDefault();
            }
            catch (Exception ex)
            {
                LogError($"加载排序字段失败: {ex.Message}");
            }
        }

        private async Task<(List<FeatureSnapshotItem> Items, bool UsedFallbackSort)> BuildSnapshotAsync()
        {
            return await QueuedTask.Run(() =>
            {
                var items = new List<FeatureSnapshotItem>();
                var usedFallbackSort = false;
                var layer = SelectedLayer;
                if (layer == null)
                {
                    return (items, usedFallbackSort);
                }

                using var table = layer.GetTable();
                if (table == null)
                {
                    return (items, usedFallbackSort);
                }

                bool canUseFieldSort = false;
                if (SelectedSortMode?.Value == BrowseSortMode.Field && SelectedSortField != null)
                {
                    var definition = table.GetDefinition();
                    if (definition?.FindField(SelectedSortField.Name) >= 0)
                    {
                        canUseFieldSort = true;
                    }
                    else
                    {
                        usedFallbackSort = true;
                    }
                }

                RowCursor cursor;
                var queryFilter = new QueryFilter();
                if (SelectedScopeMode?.Value == BrowseScopeMode.Selection)
                {
                    if (layer.SelectionCount <= 0)
                    {
                        return (items, usedFallbackSort);
                    }

                    cursor = layer.GetSelection().Search(queryFilter, false);
                }
                else
                {
                    cursor = table.Search(queryFilter, false);
                }

                using (cursor)
                {
                    while (cursor.MoveNext())
                    {
                        using var row = cursor.Current;
                        if (row == null)
                        {
                            continue;
                        }

                        object sortValue = null;
                        if (canUseFieldSort && SelectedSortField != null)
                        {
                            try
                            {
                                sortValue = row[SelectedSortField.Name];
                            }
                            catch
                            {
                                sortValue = null;
                            }
                        }

                        items.Add(new FeatureSnapshotItem
                        {
                            ObjectId = row.GetObjectID(),
                            SortValue = sortValue
                        });
                    }
                }

                if (canUseFieldSort && SelectedSortField != null)
                {
                    items.Sort((left, right) =>
                    {
                        var compare = BrowseFeaturesCore.CompareSortValue(left.SortValue, right.SortValue, SortDescending);
                        if (compare != 0)
                        {
                            return compare;
                        }

                        return left.ObjectId.CompareTo(right.ObjectId);
                    });
                }
                else
                {
                    if (usedFallbackSort)
                    {
                        items.Sort((left, right) => left.ObjectId.CompareTo(right.ObjectId));
                    }
                    else
                    {
                        items.Sort((left, right) => SortDescending
                            ? right.ObjectId.CompareTo(left.ObjectId)
                            : left.ObjectId.CompareTo(right.ObjectId));
                    }
                }

                return (items, usedFallbackSort);
            });
        }
        private async Task NavigateToIndexAsync(int targetIndex, bool persistCurrentBeforeNavigate = true)
        {
            if (targetIndex < 0 || targetIndex >= _snapshotItems.Count)
            {
                return;
            }

            if (persistCurrentBeforeNavigate)
            {
                await PersistCurrentReviewRecordAsync(forcePersist: true);
            }

            _currentIndex = targetIndex;
            var targetItem = _snapshotItems[_currentIndex];
            var context = await LoadFeatureContextAsync(targetItem.ObjectId);
            if (context == null || context.Geometry == null || context.Geometry.IsEmpty)
            {
                LogError($"OID={targetItem.ObjectId} 已失效或几何为空。");
                await TryNavigateToNextValidAsync(targetIndex);
                return;
            }

            _currentFeature = context;
            _currentPartIndex = 0;
            _currentFeatureVisitedAt = DateTime.Now;
            SnapshotInfo = $"{_currentIndex + 1}/{_snapshotItems.Count}";
            CurrentOidText = context.ObjectId.ToString(CultureInfo.InvariantCulture);
            PartInfoText = BuildPartInfoText();
            StatusMessage = $"正在浏览第 {_currentIndex + 1} 条要素。";
            UpdateSelectedSnapshotItem(context.ObjectId);

            await FocusCurrentPartAsync();
            await LoadCurrentReviewAsync();
            await PersistCurrentReviewRecordAsync(forcePersist: true);

            RaiseCommandStates();
        }

        private async Task NavigatePartAsync(int delta)
        {
            if (_currentFeature?.PartEnvelopes == null || _currentFeature.PartEnvelopes.Count <= 1)
            {
                return;
            }

            var nextIndex = _currentPartIndex + delta;
            if (nextIndex < 0 || nextIndex >= _currentFeature.PartEnvelopes.Count)
            {
                return;
            }

            _currentPartIndex = nextIndex;
            PartInfoText = BuildPartInfoText();
            await FocusCurrentPartAsync();
            RaiseCommandStates();
        }

        private async Task TryNavigateToNextValidAsync(int failedIndex)
        {
            var nextIndex = failedIndex + 1;
            if (nextIndex < _snapshotItems.Count)
            {
                await NavigateToIndexAsync(nextIndex, persistCurrentBeforeNavigate: false);
                return;
            }

            var previousIndex = failedIndex - 1;
            if (previousIndex >= 0)
            {
                await NavigateToIndexAsync(previousIndex, persistCurrentBeforeNavigate: false);
                return;
            }

            ClearSnapshotState("快照中的要素均不可访问。");
        }

        private async Task<CurrentFeatureContext> LoadFeatureContextAsync(long objectId)
        {
            return await QueuedTask.Run(() =>
            {
                var layer = SelectedLayer;
                if (layer == null)
                {
                    return null;
                }

                using var table = layer.GetTable();
                if (table == null)
                {
                    return null;
                }

                using var cursor = table.Search(new QueryFilter { ObjectIDs = new[] { objectId } }, false);
                if (!cursor.MoveNext())
                {
                    return null;
                }

                using var feature = cursor.Current as Feature;
                var shape = feature?.GetShape();
                if (shape == null || shape.IsEmpty)
                {
                    return null;
                }

                return new CurrentFeatureContext
                {
                    ObjectId = objectId,
                    Geometry = shape,
                    GeometryType = shape.GeometryType.ToString(),
                    GeometryWkt = TryExportToWkt(shape),
                    PartEnvelopes = BuildPartEnvelopes(shape)
                };
            });
        }

        private async Task FocusCurrentPartAsync()
        {
            var context = _currentFeature;
            if (context?.Geometry == null)
            {
                return;
            }

            await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView == null)
                {
                    return;
                }

                var envelope = context.Geometry.Extent;
                if (context.PartEnvelopes != null && context.PartEnvelopes.Count > 0 && _currentPartIndex >= 0 && _currentPartIndex < context.PartEnvelopes.Count)
                {
                    envelope = context.PartEnvelopes[_currentPartIndex];
                }

                if (envelope == null)
                {
                    return;
                }

                var safeEnvelope = ExpandEnvelopeIfNeeded(envelope, mapView.Extent);
                mapView.ZoomTo(safeEnvelope);
            });

            await FlashGeometryAsync(context.Geometry);
        }

        private async Task FlashGeometryAsync(Geometry geometry)
        {
            if (geometry == null)
            {
                return;
            }

            var version = Interlocked.Increment(ref _flashVersion);
            for (var i = 0; i < 2; i++)
            {
                if (version != Interlocked.Read(ref _flashVersion))
                {
                    return;
                }

                await QueuedTask.Run(() => ShowOverlay(geometry));
                await Task.Delay(180);

                if (version != Interlocked.Read(ref _flashVersion))
                {
                    return;
                }

                await QueuedTask.Run(ClearOverlays);
                await Task.Delay(90);
            }
        }

        private async Task LoadCurrentReviewAsync()
        {
            var context = _currentFeature;
            if (context == null || !_reviewTableReady)
            {
                return;
            }

            try
            {
                var batchId = BatchId?.Trim() ?? string.Empty;
                var layerUri = SelectedLayer?.URI ?? string.Empty;
                var review = await QueuedTask.Run(() =>
                    _reviewStore.TryGet(GetEffectiveOutputGdbPath(), OutputTableName, BuildCurrentProjectKey(), batchId, layerUri, context.ObjectId));

                _suppressReviewPropertyChanged = true;
                if (review == null)
                {
                    SelectedReviewStatus = "未判定";
                    CurrentNotes = string.Empty;
                    UpdateSnapshotItem(context.ObjectId, "未判定", string.Empty);
                }
                else
                {
                    SelectedReviewStatus = BrowseFeaturesCore.ToDisplayText(review.ReviewStatus);
                    CurrentNotes = review.Notes ?? string.Empty;
                    UpdateSnapshotItem(context.ObjectId, SelectedReviewStatus, CurrentNotes);
                    if (!string.IsNullOrWhiteSpace(review.Reviewer))
                    {
                        ReviewerName = review.Reviewer;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"读取审阅记录失败: {ex.Message}");
            }
            finally
            {
                _suppressReviewPropertyChanged = false;
            }
        }

        private async Task PersistCurrentReviewRecordAsync(bool forcePersist = false)
        {
            if (_suppressReviewPropertyChanged || !_reviewTableReady || _currentFeature == null || _currentIndex < 0)
            {
                return;
            }

            if (_isPersistingCurrentRecord && !forcePersist)
            {
                return;
            }

            _isPersistingCurrentRecord = true;
            try
            {
                var batchId = BatchId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(batchId))
                {
                    return;
                }

                var record = new ReviewRecord
                {
                    ProjectKey = BuildCurrentProjectKey(),
                    BatchId = batchId,
                    BatchName = batchId,
                    Reviewer = ReviewerName?.Trim() ?? string.Empty,
                    ReviewStatus = BrowseFeaturesCore.NormalizeReviewStatus(SelectedReviewStatus),
                    Notes = CurrentNotes ?? string.Empty,
                    LayerName = SelectedLayer?.Name ?? string.Empty,
                    LayerUri = SelectedLayer?.URI ?? string.Empty,
                    SourceObjectId = _currentFeature.ObjectId,
                    GeometryType = _currentFeature.GeometryType ?? string.Empty,
                    GeometryWkt = _currentFeature.GeometryWkt ?? string.Empty,
                    VisitedAt = _currentFeatureVisitedAt == DateTime.MinValue ? DateTime.Now : _currentFeatureVisitedAt,
                    UpdatedAt = DateTime.Now
                };

                await QueuedTask.Run(() => _reviewStore.Upsert(GetEffectiveOutputGdbPath(), OutputTableName, record));
                await WriteNotesToFeatureFieldAsync(record.Notes);
                UpdateSnapshotItem(_currentFeature.ObjectId, BrowseFeaturesCore.ToDisplayText(record.ReviewStatus), record.Notes);
            }
            catch (Exception ex)
            {
                LogError($"写入审阅记录失败: {ex.Message}");
            }
            finally
            {
                _isPersistingCurrentRecord = false;
            }
        }
        private async Task<bool> EnsureReviewTableReadyAsync()
        {
            var gdbPath = GetEffectiveOutputGdbPath();
            var tableName = OutputTableName?.Trim();
            if (string.IsNullOrWhiteSpace(gdbPath))
            {
                MessageBox.Show("当前项目没有默认地理数据库，请到设置里指定输出 GDB。", "提示");
                return false;
            }

            if (string.IsNullOrWhiteSpace(tableName))
            {
                MessageBox.Show("请设置输出表名。", "提示");
                return false;
            }

            var signature = $"{gdbPath}|{tableName}";
            if (_reviewTableReady && string.Equals(_reviewTableSignature, signature, StringComparison.Ordinal))
            {
                return true;
            }

            try
            {
                var existedBefore = await QueuedTask.Run(() => _reviewStore.TableExists(gdbPath, tableName));
                await QueuedTask.Run(() =>
                {
                    if (existedBefore)
                    {
                        if (!_reviewStore.HasRequiredSchema(gdbPath, tableName))
                        {
                            throw new InvalidOperationException($"目标表已存在但结构不兼容：{tableName}。请更换表名后重试。");
                        }

                        return;
                    }

                    _reviewStore.CreateTable(gdbPath, tableName);
                });

                _reviewTableReady = true;
                _reviewTableSignature = signature;
                LogInfo(existedBefore
                    ? $"已连接审阅清单表: {gdbPath}\\{tableName}"
                    : $"审阅清单表已创建: {gdbPath}\\{tableName}");
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "输出表冲突");
                LogError($"初始化审阅表失败: {ex.Message}");
                _reviewTableReady = false;
                return false;
            }
        }

        private void ResetReviewTableState()
        {
            _reviewTableReady = false;
            _reviewTableSignature = string.Empty;
        }

        private void BrowseOutputGeodatabase()
        {
            try
            {
                var dialog = new OpenItemDialog
                {
                    Title = "选择输出地理数据库",
                    MultiSelect = false,
                    Filter = ItemFilters.Geodatabases,
                    InitialLocation = string.IsNullOrWhiteSpace(OutputGdbPath) ? Project.Current?.HomeFolderPath : OutputGdbPath
                };

                if (dialog.ShowDialog() == true && dialog.Items.Any())
                {
                    OutputGdbPath = dialog.Items.First().Path;
                }
            }
            catch (Exception ex)
            {
                LogError($"选择输出数据库失败: {ex.Message}");
            }
        }

        private void ShowHelp()
        {
            var helpText =
                "浏览要素工具说明\n\n" +
                "1) 选择目标图层、遍历范围（选择集/全量）与排序规则。\n" +
                "2) 点击“刷新快照”生成遍历列表。\n" +
                "3) 通过首条/上一条/下一条/末条进行导航，地图将自动定位并高亮。\n" +
                "4) 对多部件要素可使用“上一部件/下一部件”逐部件检查。\n" +
                "5) 审阅状态与备注会实时写入 GDB 清单表。\n\n" +
                "注意：\n" +
                "- 选择集模式下，地图选择变化后会提示快照过期，需要手动刷新。\n" +
                "- 目标表存在时会阻止写入，请更换表名后再刷新。";

            MessageBox.Show(helpText, "浏览要素帮助");
        }

        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            if (SelectedScopeMode?.Value != BrowseScopeMode.Selection || _snapshotItems.Count == 0)
            {
                return;
            }

            MarkSnapshotStale("检测到选择集变化，快照已过期，请刷新。");
        }

        private void MarkSnapshotStale(string message)
        {
            if (!HasSnapshot)
            {
                return;
            }

            IsSnapshotStale = true;
            StaleHint = message;
            StatusMessage = message;
        }

        private void ClearSnapshotState(string message)
        {
            _snapshotItems.Clear();
            _currentIndex = -1;
            _currentFeature = null;
            _currentPartIndex = 0;
            _currentFeatureVisitedAt = DateTime.MinValue;
            SnapshotInfo = "0/0";
            CurrentOidText = "-";
            PartInfoText = "-";
            _suppressSnapshotListSelectionChanged = true;
            SelectedSnapshotItem = null;
            _suppressSnapshotListSelectionChanged = false;
            _snapshotList.Clear();
            IsSnapshotStale = false;
            StaleHint = string.Empty;
            StatusMessage = message;
            RaiseCommandStates();
        }

        private string BuildPartInfoText()
        {
            var count = _currentFeature?.PartEnvelopes?.Count ?? 0;
            if (count <= 0)
            {
                return "-";
            }

            return $"{_currentPartIndex + 1}/{count}";
        }

        private bool CanGoFirst() => !IsBusy && _snapshotItems.Count > 0 && _currentIndex > 0;
        private bool CanGoPrevious() => !IsBusy && _snapshotItems.Count > 0 && _currentIndex > 0;
        private bool CanGoNext() => !IsBusy && _snapshotItems.Count > 0 && _currentIndex < _snapshotItems.Count - 1;
        private bool CanGoLast() => !IsBusy && _snapshotItems.Count > 0 && _currentIndex >= 0 && _currentIndex < _snapshotItems.Count - 1;
        private bool CanGoPreviousPart() => !IsBusy && _currentFeature?.PartEnvelopes != null && _currentFeature.PartEnvelopes.Count > 1 && _currentPartIndex > 0;
        private bool CanGoNextPart() => !IsBusy && _currentFeature?.PartEnvelopes != null && _currentFeature.PartEnvelopes.Count > 1 && _currentPartIndex < _currentFeature.PartEnvelopes.Count - 1;

        private void RaiseCommandStates()
        {
            _refreshLayersCommand.RaiseCanExecuteChanged();
            _refreshSnapshotCommand.RaiseCanExecuteChanged();
            _browseOutputGdbCommand.RaiseCanExecuteChanged();
            _firstFeatureCommand.RaiseCanExecuteChanged();
            _previousFeatureCommand.RaiseCanExecuteChanged();
            _nextFeatureCommand.RaiseCanExecuteChanged();
            _lastFeatureCommand.RaiseCanExecuteChanged();
            _previousPartCommand.RaiseCanExecuteChanged();
            _nextPartCommand.RaiseCanExecuteChanged();
        }

        private void LogInfo(string message)
        {
            AppendLog(message);
        }

        private void LogError(string message)
        {
            AppendLog($"错误: {message}");
            StatusMessage = message;
        }

        private void AppendLog(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            LogContent = string.IsNullOrWhiteSpace(LogContent)
                ? line
                : $"{LogContent}{Environment.NewLine}{line}";
        }

        private string BuildCurrentProjectKey()
        {
            return Project.Current?.HomeFolderPath ??
                   Project.Current?.Name ??
                   string.Empty;
        }

        private string GetEffectiveOutputGdbPath()
        {
            return string.IsNullOrWhiteSpace(OutputGdbPath)
                ? Project.Current?.DefaultGeodatabasePath ?? string.Empty
                : OutputGdbPath.Trim();
        }

        private async Task WriteNotesToFeatureFieldAsync(string notes)
        {
            var settings = SettingsManager.Settings.BrowseFeatures;
            if (!settings.WriteNotesToFeatureField || string.IsNullOrWhiteSpace(settings.NotesFieldName) || SelectedLayer == null || _currentFeature == null)
            {
                return;
            }

            try
            {
                await QueuedTask.Run(() =>
                {
                    using var table = SelectedLayer.GetTable();
                    var definition = table?.GetDefinition();
                    if (definition == null || definition.FindField(settings.NotesFieldName) < 0)
                    {
                        return;
                    }

                    using var cursor = table.Search(new QueryFilter { ObjectIDs = new[] { _currentFeature.ObjectId } }, false);
                    if (!cursor.MoveNext())
                    {
                        return;
                    }

                    using var row = cursor.Current;
                    row[settings.NotesFieldName] = notes ?? string.Empty;
                    row.Store();
                });
            }
            catch (Exception ex)
            {
                LogError($"备注写回要素字段失败: {ex.Message}");
            }
        }

        private void UpdateSelectedSnapshotItem(long objectId)
        {
            var item = _snapshotList.FirstOrDefault(entry => entry.ObjectId == objectId);
            _suppressSnapshotListSelectionChanged = true;
            SelectedSnapshotItem = item;
            _suppressSnapshotListSelectionChanged = false;
        }

        private void UpdateSnapshotItem(long objectId, string reviewStatusText, string notes)
        {
            var item = _snapshotList.FirstOrDefault(entry => entry.ObjectId == objectId);
            if (item != null)
            {
                item.ReviewStatusText = reviewStatusText;
                item.NotesPreview = BuildNotesPreview(notes);
            }
        }

        private static string BuildNotesPreview(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
            {
                return string.Empty;
            }

            var normalized = notes.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 20 ? normalized : normalized.Substring(0, 20) + "...";
        }

        private static bool IsSupportedLayer(FeatureLayer layer)
        {
            var shapeType = layer.ShapeType;
            return shapeType == esriGeometryType.esriGeometryPoint ||
                   shapeType == esriGeometryType.esriGeometryMultipoint ||
                   shapeType == esriGeometryType.esriGeometryPolyline ||
                   shapeType == esriGeometryType.esriGeometryPolygon;
        }

        private static bool IsSortableField(FieldType fieldType)
        {
            return fieldType == FieldType.String ||
                   fieldType == FieldType.Integer ||
                   fieldType == FieldType.SmallInteger ||
                   fieldType == FieldType.Double ||
                   fieldType == FieldType.Single ||
                   fieldType == FieldType.BigInteger ||
                   fieldType == FieldType.Date ||
                   fieldType == FieldType.DateOnly ||
                   fieldType == FieldType.GUID ||
                   fieldType == FieldType.GlobalID;
        }

        private static bool IsSystemField(string fieldName)
        {
            return string.Equals(fieldName, "OBJECTID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "OID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "FID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "GLOBALID", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "SHAPE", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "SHAPE_LENGTH", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fieldName, "SHAPE_AREA", StringComparison.OrdinalIgnoreCase);
        }
        private static Envelope ExpandEnvelopeIfNeeded(Envelope sourceEnvelope, Envelope currentViewExtent)
        {
            var width = sourceEnvelope.Width;
            var height = sourceEnvelope.Height;
            var sr = sourceEnvelope.SpatialReference;
            var xCenter = (sourceEnvelope.XMin + sourceEnvelope.XMax) / 2.0;
            var yCenter = (sourceEnvelope.YMin + sourceEnvelope.YMax) / 2.0;
            const double paddingRatio = 0.2;
            const double fallbackViewRatio = 0.03;

            if (width > 0 && height > 0)
            {
                var paddingX = width * paddingRatio;
                var paddingY = height * paddingRatio;
                return EnvelopeBuilderEx.CreateEnvelope(
                    sourceEnvelope.XMin - paddingX,
                    sourceEnvelope.YMin - paddingY,
                    sourceEnvelope.XMax + paddingX,
                    sourceEnvelope.YMax + paddingY,
                    sr);
            }

            var viewWidth = currentViewExtent?.Width ?? 0;
            var viewHeight = currentViewExtent?.Height ?? 0;
            var fallbackWidth = viewWidth > 0 ? viewWidth * fallbackViewRatio : 1.0;
            var fallbackHeight = viewHeight > 0 ? viewHeight * fallbackViewRatio : 1.0;

            if (width > 0 || height > 0)
            {
                var paddingX = width > 0 ? width * paddingRatio : fallbackWidth;
                var paddingY = height > 0 ? height * paddingRatio : fallbackHeight;
                return EnvelopeBuilderEx.CreateEnvelope(
                    sourceEnvelope.XMin - paddingX,
                    sourceEnvelope.YMin - paddingY,
                    sourceEnvelope.XMax + paddingX,
                    sourceEnvelope.YMax + paddingY,
                    sr);
            }

            return EnvelopeBuilderEx.CreateEnvelope(
                xCenter - fallbackWidth,
                yCenter - fallbackHeight,
                xCenter + fallbackWidth,
                yCenter + fallbackHeight,
                sr);
        }

        private static void ShowOverlay(Geometry geometry)
        {
            var mapView = MapView.Active;
            if (mapView == null)
            {
                return;
            }

            ClearOverlays();

            if (geometry is MapPoint mapPoint)
            {
                var symbol = SymbolFactory.Instance
                    .ConstructPointSymbol(CIMColor.CreateRGBColor(255, 165, 0), 11, SimpleMarkerStyle.Circle)
                    .MakeSymbolReference();
                lock (OverlayLock)
                {
                    ActiveOverlays.Add(mapView.AddOverlay(mapPoint, symbol));
                }
                return;
            }

            if (geometry is Polygon polygon)
            {
                var boundary = GeometryEngine.Instance.Boundary(polygon);
                var symbol = SymbolFactory.Instance
                    .ConstructLineSymbol(CIMColor.CreateRGBColor(255, 90, 0), 3.2, SimpleLineStyle.Solid)
                    .MakeSymbolReference();
                lock (OverlayLock)
                {
                    ActiveOverlays.Add(mapView.AddOverlay(boundary, symbol));
                }
                return;
            }

            if (geometry is Polyline polyline)
            {
                var symbol = SymbolFactory.Instance
                    .ConstructLineSymbol(CIMColor.CreateRGBColor(255, 90, 0), 3.2, SimpleLineStyle.Solid)
                    .MakeSymbolReference();
                lock (OverlayLock)
                {
                    ActiveOverlays.Add(mapView.AddOverlay(polyline, symbol));
                }
                return;
            }

            if (geometry is Multipoint multipoint)
            {
                var symbol = SymbolFactory.Instance
                    .ConstructPointSymbol(CIMColor.CreateRGBColor(255, 165, 0), 9, SimpleMarkerStyle.Circle)
                    .MakeSymbolReference();
                foreach (var point in multipoint.Points)
                {
                    lock (OverlayLock)
                    {
                        ActiveOverlays.Add(mapView.AddOverlay(point, symbol));
                    }
                }
            }
        }

        private static void ClearOverlays()
        {
            lock (OverlayLock)
            {
                foreach (var overlay in ActiveOverlays)
                {
                    overlay?.Dispose();
                }
                ActiveOverlays.Clear();
            }
        }

        private static IReadOnlyList<Envelope> BuildPartEnvelopes(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty)
            {
                return Array.Empty<Envelope>();
            }

            if (geometry is Multipart multipart)
            {
                var envelopes = new List<Envelope>();
                foreach (var part in multipart.Parts)
                {
                    var hasPoint = false;
                    var xMin = 0.0;
                    var yMin = 0.0;
                    var xMax = 0.0;
                    var yMax = 0.0;

                    foreach (var segment in part)
                    {
                        AddPoint(segment.StartPoint);
                        AddPoint(segment.EndPoint);
                    }

                    if (hasPoint)
                    {
                        envelopes.Add(EnvelopeBuilderEx.CreateEnvelope(xMin, yMin, xMax, yMax, multipart.SpatialReference));
                    }

                    void AddPoint(MapPoint point)
                    {
                        if (point == null)
                        {
                            return;
                        }

                        if (!hasPoint)
                        {
                            xMin = xMax = point.X;
                            yMin = yMax = point.Y;
                            hasPoint = true;
                            return;
                        }

                        xMin = Math.Min(xMin, point.X);
                        yMin = Math.Min(yMin, point.Y);
                        xMax = Math.Max(xMax, point.X);
                        yMax = Math.Max(yMax, point.Y);
                    }
                }

                if (envelopes.Count > 0)
                {
                    return envelopes;
                }
            }

            return geometry.Extent == null
                ? Array.Empty<Envelope>()
                : new[] { geometry.Extent };
        }

        private static string TryExportToWkt(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty)
            {
                return string.Empty;
            }

            try
            {
                // 兼容不同 ArcGIS Pro SDK 版本可能存在的导出重载差异。
                var methods = GeometryEngine.Instance.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(method => string.Equals(method.Name, "ExportToWKT", StringComparison.Ordinal))
                    .ToList();

                foreach (var method in methods)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(typeof(Geometry)))
                    {
                        var value = method.Invoke(GeometryEngine.Instance, new object[] { geometry });
                        if (value is string text && !string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                    else if (parameters.Length == 2 &&
                             parameters[1].ParameterType.IsAssignableFrom(typeof(Geometry)) &&
                             parameters[0].ParameterType.IsEnum)
                    {
                        var flag = Enum.ToObject(parameters[0].ParameterType, 0);
                        var value = method.Invoke(GeometryEngine.Instance, new[] { flag, geometry });
                        if (value is string text && !string.IsNullOrWhiteSpace(text))
                        {
                            return text;
                        }
                    }
                }
            }
            catch
            {
                // WKT 导出失败时返回空字符串，调用侧记录日志但不中断流程。
            }

            return string.Empty;
        }
    }
}
