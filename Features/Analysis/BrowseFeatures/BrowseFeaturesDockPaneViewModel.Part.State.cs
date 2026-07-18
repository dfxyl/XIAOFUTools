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
using XIAOFUTools.Shared;
using XIAOFUTools.Features.User.Settings;
using XIAOFUTools.Features.Analysis.BrowseFeatures.Core;

namespace XIAOFUTools.Features.Analysis.BrowseFeatures
{
    internal sealed partial class BrowseFeaturesDockPaneViewModel
    {

        private bool HasSnapshot => _snapshotItems.Count > 0;


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


        public RelayCommand RefreshLayersCommand => _refreshLayersCommand;

        public RelayCommand RefreshSnapshotCommand => _refreshSnapshotCommand;

        public RelayCommand BrowseOutputGdbCommand => _browseOutputGdbCommand;

        public RelayCommand FirstFeatureCommand => _firstFeatureCommand;

        public RelayCommand PreviousFeatureCommand => _previousFeatureCommand;

        public RelayCommand NextFeatureCommand => _nextFeatureCommand;

        public RelayCommand LastFeatureCommand => _lastFeatureCommand;

        public RelayCommand PreviousPartCommand => _previousPartCommand;

        public RelayCommand NextPartCommand => _nextPartCommand;

        public RelayCommand OpenSettingsCommand => _openSettingsCommand;

        public RelayCommand ShowHelpCommand => _showHelpCommand;

    }
}
