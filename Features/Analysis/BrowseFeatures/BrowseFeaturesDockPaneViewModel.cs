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
using XIAOFUTools.Features.Analysis.BrowseFeatures.Infrastructure;

namespace XIAOFUTools.Features.Analysis.BrowseFeatures
{
    internal sealed partial class BrowseFeaturesDockPaneViewModel : PropertyChangedBase
    {

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

        private readonly RelayCommand _refreshLayersCommand;
        private readonly RelayCommand _refreshSnapshotCommand;
        private readonly RelayCommand _browseOutputGdbCommand;
        private readonly RelayCommand _firstFeatureCommand;
        private readonly RelayCommand _previousFeatureCommand;
        private readonly RelayCommand _nextFeatureCommand;
        private readonly RelayCommand _lastFeatureCommand;
        private readonly RelayCommand _previousPartCommand;
        private readonly RelayCommand _nextPartCommand;
        private readonly RelayCommand _openSettingsCommand;
        private readonly RelayCommand _showHelpCommand;
        private readonly IBrowseFeaturesSettingsWindowService _settingsWindowService;

        public BrowseFeaturesDockPaneViewModel() : this(new BrowseFeaturesSettingsWindowService())
        {
        }

        internal BrowseFeaturesDockPaneViewModel(IBrowseFeaturesSettingsWindowService settingsWindowService)
        {
            _settingsWindowService = settingsWindowService ?? throw new ArgumentNullException(nameof(settingsWindowService));
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

            _refreshLayersCommand = new RelayCommand(async () => await RefreshLayersAsync(), () => !IsBusy);
            _refreshSnapshotCommand = new RelayCommand(async () => await RefreshSnapshotAsync(), () => !IsBusy && SelectedLayer != null);
            _browseOutputGdbCommand = new RelayCommand(BrowseOutputGeodatabase, () => !IsBusy);
            _firstFeatureCommand = new RelayCommand(async () => await NavigateToIndexAsync(0), CanGoFirst);
            _previousFeatureCommand = new RelayCommand(async () => await NavigateToIndexAsync(_currentIndex - 1), CanGoPrevious);
            _nextFeatureCommand = new RelayCommand(async () => await NavigateToIndexAsync(_currentIndex + 1), CanGoNext);
            _lastFeatureCommand = new RelayCommand(async () => await NavigateToIndexAsync(_snapshotItems.Count - 1), CanGoLast);
            _previousPartCommand = new RelayCommand(async () => await NavigatePartAsync(-1), CanGoPreviousPart);
            _nextPartCommand = new RelayCommand(async () => await NavigatePartAsync(1), CanGoNextPart);
            _openSettingsCommand = new RelayCommand(OpenSettings);
            _showHelpCommand = new RelayCommand(ShowHelp);

            _selectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
            _ = RefreshLayersAsync();
        }

        private SnapshotListItem _selectedSnapshotItem;
    }
}
