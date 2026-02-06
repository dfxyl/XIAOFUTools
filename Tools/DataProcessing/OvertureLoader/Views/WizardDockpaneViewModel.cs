using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Geometry;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Data;
using XIAOFUTools.Tools.OvertureLoader.Services;
using Microsoft.Win32;
using ArcGIS.Desktop.Catalog;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.IO;
using System.Threading;
using ArcGIS.Desktop.Core; // Added for Project.Current
using System.Windows; // Added for Application.Current.Dispatcher
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.OvertureLoader.Views
{
    // Selectable theme item class for binding
    public class SelectableThemeItem : INotifyPropertyChanged
    {
        private string _displayName;
        private bool? _isSelected; // Changed to nullable bool
        private string _actualType;
        private string _parentThemeForS3;
        private bool _isExpanded;
        private bool _isUpdatingSubItems = false; // Flag to prevent loops when parent updates children

        public SelectableThemeItem Parent { get; internal set; } // Property to hold the parent

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool? IsSelected // Changed to nullable bool
        {
            get => _isSelected;
            set // 'value' here is what the XAML CheckBox binding is trying to set
            {
                bool? determinedNewState = value; // Start with what the UI is suggesting

                if (IsExpandable) // Special handling for parent node clicks
                {
                    // If the parent was fully checked (IsSelected == true internally),
                    // and the user clicks it, the default WPF cycle for a tristate checkbox
                    // (when bound with TargetNullValue) would send 'null' as the new 'value' from the UI.
                    // We want this specific interaction to mean "uncheck all children".
                    if (_isSelected == true && value == null)
                    {
                        determinedNewState = false;
                    }
                    // If it was false or indeterminate, and user clicks, WPF sends 'true' (from false/indeterminate) or 'false' (from indeterminate).
                    // In these cases, 'value' represents the desired state (true to select all, false to deselect all from indeterminate).
                    // So, determinedNewState will either be 'false' (if user unchecks a fully checked parent)
                    // or 'value' (which would be true if user checks an unchecked/indeterminate parent, or false if user deselects from indeterminate).
                }

                if (_isSelected != determinedNewState)
                {
                    _isSelected = determinedNewState;

                    // If this is a parent item, propagate the selection to children
                    // Only propagate if the new state is definitively true or false
                    if (IsExpandable && _isSelected.HasValue && !_isUpdatingSubItems)
                    {
                        _isUpdatingSubItems = true;
                        foreach (var subItem in SubItems)
                        {
                            subItem.IsSelected = _isSelected; // Propagate the true/false state
                        }
                        _isUpdatingSubItems = false;
                    }

                    // Handle Expansion/Collapse for this parent item based on its new IsSelected state
                    if (IsExpandable)
                    {
                        if (_isSelected == true)
                        {
                            IsExpanded = true;  // Expand if parent is fully selected
                        }
                        else if (_isSelected == false)
                        {
                            IsExpanded = false; // Collapse if parent is fully unselected
                        }
                        // If _isSelected is null (indeterminate), do not change IsExpanded state from this selection logic.
                    }

                    OnPropertyChanged();
                    SelectionChanged?.Invoke(this, EventArgs.Empty); // Notify subscribers (like ViewModel for leaves)

                    // If this item has a parent, notify the parent to update its state
                    Parent?.UpdateSelectionStateFromChildren();
                }
            }
        }

        public string ActualType
        {
            get => _actualType;
            set => _actualType = value;
        }

        public string ParentThemeForS3
        {
            get => _parentThemeForS3;
            private set => _parentThemeForS3 = value;
        }

        public ObservableCollection<SelectableThemeItem> SubItems { get; }
        public bool IsExpandable => SubItems.Any();
        public bool IsSelectable { get; } // True if it's a leaf node

        public bool IsExpanded // New property definition
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value && IsExpandable) // Only allow change if expandable
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public event EventHandler SelectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SelectableThemeItem(string displayName, string actualType, string parentThemeForS3, bool isLeafNode = true)
        {
            DisplayName = displayName;
            ActualType = actualType;
            ParentThemeForS3 = parentThemeForS3;
            SubItems = [];
            IsSelectable = isLeafNode; // Leaf nodes are selectable (actual data types)
            _isSelected = false; // Default to false (not indeterminate)
            _isExpanded = false; // Default to not expanded
            // If it's a parent (not a leaf node but has potential for sub-items), 
            // its IsSelected state will be determined by its children later.
        }

        internal void UpdateSelectionStateFromChildren()
        {
            if (!IsExpandable || _isUpdatingSubItems) // Only parents update from children; avoid loops
                return;

            bool? newSelectionState = CalculateSelectionStateFromChildren();

            if (_isSelected != newSelectionState)
            {
                _isSelected = newSelectionState;
                OnPropertyChanged(nameof(IsSelected));
                // We might not want to invoke SelectionChanged here for parents if it triggers data load logic
                // The ViewModel should primarily listen to SelectionChanged from actual data leaves (IsSelectable = true)
            }
        }

        private bool? CalculateSelectionStateFromChildren()
        {
            if (!SubItems.Any())
                return false; // No children, so parent is effectively unselected (or could be true if it's a leaf parent itself)
                              // For a non-leaf parent, if it has no children, it should probably be 'false'.
                              // This case might not occur if SubItems are always populated for expandable parents.

            bool allTrue = true;
            bool allFalse = true;

            foreach (var subItem in SubItems)
            {
                if (subItem.IsSelected != true) allTrue = false;
                if (subItem.IsSelected != false) allFalse = false;
            }

            if (allTrue) return true;
            if (allFalse) return false;
            return null; // Indeterminate
        }
    }

    internal class WizardDockpaneViewModel : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_OvertureLoaderDockPane";
        private DataProcessor _dataProcessor;
        private const string RELEASE_URL = "https://labs.overturemaps.org/data/releases.json";
        private const string S3_BASE_PATH = "s3://overturemaps-us-west-2/release";
        private const string ADDIN_DATA_SUBFOLDER = "OvertureProAddinData"; // Define a subfolder name

        // Add CancellationTokenSource for cancelling operations
        private CancellationTokenSource _cts;

        private static readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        static WizardDockpaneViewModel()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ArcGIS-Pro-Overture-Plugin/1.0");
        }

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// 创建停靠窗格内容
        /// </summary>
        protected override System.Windows.Controls.Control OnCreateContent()
        {
            return new WizardDockpaneView();
        }

        // Centralized logic for Default MFC Base Path
        private static string DeterminedDefaultMfcBasePath
        {
            get
            {
                try
                {
                    var project = Project.Current;
                    if (project != null && !string.IsNullOrEmpty(project.HomeFolderPath))
                    {
                        // Use the project's Home Folder Path
                        return Path.Combine(project.HomeFolderPath, ADDIN_DATA_SUBFOLDER);
                    }
                    // Fallback if HomeFolderPath is not available but project path is (less ideal)
                    else if (project != null && !string.IsNullOrEmpty(project.Path))
                    {
                        string projectDir = Path.GetDirectoryName(project.Path);
                        if (!string.IsNullOrEmpty(projectDir))
                            return Path.Combine(projectDir, ADDIN_DATA_SUBFOLDER);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting project home/path for DefaultMfcBasePath: {ex.Message}");
                }
                // Fallback to MyDocuments if project path cannot be determined
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), ADDIN_DATA_SUBFOLDER);
            }
        }

        // Store original Overture S3 theme structure
        private readonly Dictionary<string, string> _overtureS3ThemeTypes = new()
        {
            { "addresses", "address" },
            { "base", "land,water,land_use,land_cover,bathymetry,infrastructure" },
            { "buildings", "building,building_part" },
            { "divisions", "division,division_boundary,division_area" },
            { "places", "place" },
            { "transportation", "connector,segment" }
        };

        // Friendly display names for parent themes
        private readonly Dictionary<string, string> _parentThemeDisplayNames = new()
        {
            { "addresses", "地址" },
            { "base", "基础图层" },
            { "buildings", "建筑物" },
            { "divisions", "行政区划" },
            { "places", "兴趣点" },
            { "transportation", "交通网络" }
        };

        private readonly Dictionary<string, string> ThemeIcons = new()
        {
            { "addresses", "GeocodeAddressesIcon" },
            { "base", "GlobeIcon" },
            { "buildings", "BuildingLayerIcon" },
            { "divisions", "BoundaryIcon" },
            { "places", "PointOfInterestIcon" },
            { "transportation", "TransportationNetworkIcon" }
        };

        private readonly Dictionary<string, string> ThemeDescriptions = new()
        {
            { "addresses", "地址点，包括街道名称、门牌号和邮政编码。" },
            { "base", "基础图层，包括陆地、水体、土地利用、土地覆盖和基础设施边界。" },
            { "buildings", "建筑物轮廓，包含可用的高度信息。" },
            { "divisions", "行政边界，包括国家、州、城市和其他行政区划。" },
            { "places", "兴趣点和场所，包括商业场所、地标和便民设施。" },
            { "transportation", "交通网络，包括道路、铁路、路径和其他通道。" }
        };

        // IMPORTANT: Review and adjust these estimates for accuracy.
        // These are now based on specific "ActualType" rather than parent themes.
        private readonly Dictionary<string, int> ThemeFeatureEstimates = new()
        {
            // Addresses
            { "address", 500 }, // Previously under "addresses"

            // Base Sub-types
            { "land", 100 },
            { "water", 50 },
            { "land_use", 40 },
            { "land_cover", 40 },
            { "bathymetry", 20 },
            { "infrastructure", 50 },
            // Total for old "base" was 300, current sum is 300

            // Buildings Sub-types
            { "building", 700 },
            { "building_part", 100 },
            // Total for old "buildings" was 800, current sum is 800

            // Divisions Sub-types
            { "division", 30 },
            { "division_boundary", 40 },
            { "division_area", 30 },
            // Total for old "divisions" was 100, current sum is 100

            // Places
            { "place", 250 }, // Previously under "places"

            // Transportation Sub-types
            { "connector", 150 },
            { "segment", 600 }
            // Total for old "transportation" was 750, current sum is 750
        };

        private CustomExtentTool _customExtentTool;

        // Property for the TreeView to bind its selected item for preview
        private SelectableThemeItem _selectedItemForPreview;
        public SelectableThemeItem SelectedItemForPreview
        {
            get => _selectedItemForPreview;
            set
            {
                SetProperty(ref _selectedItemForPreview, value);
                // Update preview panel when the focused item in TreeView changes
                if (value != null)
                {
                    // Old SelectedTheme string is now derived from SelectedItemForPreview for compatibility
                    SelectedTheme = value.ParentThemeForS3; // Or value.DisplayName, depending on usage
                }
                UpdateThemePreview(); // Update description, estimates etc.
                (ShowThemeInfoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // Properties for TreeView Preview
        public int SelectedLeafItemCount => GetSelectedLeafItems().Count;
        public List<SelectableThemeItem> AllSelectedLeafItemsForPreview => GetSelectedLeafItems();

        // Public parameterless constructor for XAML Designer
        public WizardDockpaneViewModel()
        {
            // This constructor is ONLY for the XAML designer.
            // It should initialize properties to provide a design-time preview.
            // Do NOT call full runtime initialization logic (like InitializeAsync).
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                Themes = new ObservableCollection<SelectableThemeItem>
                {
                    new SelectableThemeItem("Addresses (Design)", "addresses", "addresses", true),
                    new SelectableThemeItem("Base (Design)", "base", "base", false)
                    {
                        SubItems =
                        {
                            new SelectableThemeItem("Land (Design)", "land", "base", true),
                            new SelectableThemeItem("Water (Design)", "water", "base", true)
                        }
                    }
                };
                LatestRelease = "202X-XX-XX (设计)";
                StatusText = "设计模式预览 - 主题已加载";
                IsLoading = false;
                DataOutputPath = "C:\\Design\\Path\\Data";
                MfcOutputPath = "C:\\Design\\Path\\Connections";
                CustomExtentDisplay = "未设置自定义范围 (设计)";
                ThemeDescription = "选择一个主题 (设计)";
                EstimatedFeatures = "-- (设计)";
                EstimatedSize = "-- (设计)";
                LogOutput = new StringBuilder("设计模式日志输出。\n就绪。");
                LogOutputText = LogOutput.ToString();
            }
            else
            {
                // This case (public ctor at runtime) should ideally not happen if Pro uses the protected one.
                // If it does, we must ensure full initialization.
                System.Diagnostics.Debug.WriteLine("WARNING: Public parameterless constructor called at runtime. Performing full initialization.");
                InitializeViewModelForRuntime();
            }
        }

        // Protected constructor for ArcGIS Pro framework runtime instantiation
        // Removing this constructor as the public one handles both design-time and runtime.
        /*
        protected WizardDockpaneViewModel() 
        {
            // This is the primary runtime constructor expected by ArcGIS Pro.
            InitializeViewModelForRuntime();
        }
        */

        private void InitializeViewModelForRuntime()
        {
            System.Diagnostics.Debug.WriteLine("InitializeViewModelForRuntime executing...");
            _dataProcessor = new DataProcessor();

            LoadDataCommand = new RelayCommand(async () => await LoadOvertureDataAsync(), () => GetSelectedLeafItems().Count > 0);
            ShowThemeInfoCommand = new RelayCommand(() => ShowThemeInfo(), () => SelectedItemForPreview != null);
            SetCustomExtentCommand = new RelayCommand(() => SetCustomExtent(), () => UseCustomExtent);
            BrowseMfcLocationCommand = new RelayCommand(() => BrowseMfcLocation());
            BrowseDataLocationCommand = new RelayCommand(() => BrowseDataLocation());
            BrowseCustomDataFolderCommand = new RelayCommand(() => BrowseCustomDataFolder());
            CreateMfcCommand = new RelayCommand(async () => await CreateMfcAsync(), () => (UsePreviouslyLoadedData && !string.IsNullOrEmpty(_lastLoadedDataPath)) || (UseCustomDataFolder && !string.IsNullOrEmpty(CustomDataFolderPath)));
            GoToCreateMfcTabCommand = new RelayCommand(() => ShowCreateMfcTab(), () => true);
            CancelCommand = new RelayCommand(() =>
            {
                if (_cts != null && !_cts.IsCancellationRequested) { _cts.Cancel(); AddToLog("用户取消了操作。"); }
                ResetState(); AddToLog("插件状态已重置。");
                try { this.Hide(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error closing dockpane: {ex.Message}"); }
            });
            SelectAllCommand = new RelayCommand(
                () => IsSelectAllChecked = !IsSelectAllChecked, // Action: Toggle the IsSelectAllChecked property
                () => Themes != null && Themes.Any(t => t.IsSelectable || t.SubItems.Any()) // CanExecute: If there are any selectable themes
            );
            ShowHelpCommand = new RelayCommand(() => ShowHelp());

            CustomExtentTool.ExtentCreatedStatic += OnExtentCreated;

            Themes = new ObservableCollection<SelectableThemeItem>();
            LogOutput = new StringBuilder();
            LogOutput.AppendLine("Initializing WizardDockpaneViewModel...");
            LogOutputText = LogOutput.ToString(); // Initialize LogOutputText

            // Set default paths immediately, they might be updated by InitializeAsync if LatestRelease changes
            var defaultBasePath = DeterminedDefaultMfcBasePath;
            DataOutputPath = Path.Combine(defaultBasePath, "Data", LatestRelease ?? "latest");
            MfcOutputPath = Path.Combine(defaultBasePath, "Connections");
            NotifyPropertyChanged(nameof(DataOutputPath)); // Notify for initial value
            NotifyPropertyChanged(nameof(MfcOutputPath)); // Notify for initial value

            IsLoading = true; // Set IsLoading to true before starting async init
            StatusText = "Initializing..."; // Initial status
            NotifyPropertyChanged(nameof(IsLoading));
            NotifyPropertyChanged(nameof(StatusText));
            _isSelectAllChecked = false; // Initialize Select All state

            // Do not call InitializeAsync() here. The base DockPane class
            // will invoke it automatically after construction. Calling it
            // explicitly would cause double initialization which leads to
            // errors such as attempting to open the DuckDB connection twice.
        }

        protected override async Task InitializeAsync()
        {
            try
            {
                AddToLog("异步初始化: 正在初始化 DuckDB");
                await _dataProcessor.InitializeDuckDBAsync();
                AddToLog("异步初始化: 正在获取最新版本信息");
                LatestRelease = await GetLatestRelease();
                NotifyPropertyChanged(nameof(LatestRelease));
                AddToLog($"异步初始化: 最新版本设置为: {LatestRelease}");

                InitializeThemes(); // Populate Themes collection
                AddToLog("异步初始化: 主题已初始化");

                var defaultBasePath = DeterminedDefaultMfcBasePath;
                DataOutputPath = Path.Combine(defaultBasePath, "Data", LatestRelease ?? "latest");
                NotifyPropertyChanged(nameof(DataOutputPath));
                AddToLog($"异步初始化: 数据输出路径更新为: {DataOutputPath}");

                StatusText = "准备加载 Overture Maps 数据";
                AddToLog("异步初始化: 准备加载 Overture Maps 数据");
            }
            catch (Exception ex)
            {
                var error = $"Async Initialization error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error during async initialization: {ex}");
                StatusText = error;
                AddToLog($"ERROR: {error}");
            }
            finally
            {
                IsLoading = false;
                NotifyPropertyChanged(nameof(IsLoading));
            }
        }

        #region Properties
        private string _latestRelease;
        public string LatestRelease
        {
            get => _latestRelease;
            set => SetProperty(ref _latestRelease, value);
        }

        private ObservableCollection<SelectableThemeItem> _themes;
        public ObservableCollection<SelectableThemeItem> Themes
        {
            get => _themes;
            private set => SetProperty(ref _themes, value);
        }

        private string _selectedTheme;
        public string SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                SetProperty(ref _selectedTheme, value);
                (LoadDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ShowThemeInfoCommand as RelayCommand)?.RaiseCanExecuteChanged();

                if (value != null && ThemeIcons.TryGetValue(value, out string iconText))
                {
                    ThemeIconText = iconText;
                }
                else
                {
                    ThemeIconText = "GlobeIcon"; // Default icon (globe)
                }

                UpdateThemePreview();
            }
        }

        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        private string _statusText = "Initializing...";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private double _progressValue;
        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        private StringBuilder _logOutput;
        public StringBuilder LogOutput
        {
            get => _logOutput;
            set => SetProperty(ref _logOutput, value);
        }

        private string _logOutputText;
        public string LogOutputText
        {
            get => _logOutputText;
            set => SetProperty(ref _logOutputText, value);
        }

        private bool _useCurrentMapExtent = true;
        public bool UseCurrentMapExtent
        {
            get => _useCurrentMapExtent;
            set
            {
                SetProperty(ref _useCurrentMapExtent, value);
                // Only update UseCustomExtent if setting UseCurrentMapExtent to true
                if (value)
                {
                    UseCustomExtent = false;
                }
                // Always raise can execute changed for the command
                (SetCustomExtentCommand as RelayCommand)?.RaiseCanExecuteChanged();

                // Add debug info
                System.Diagnostics.Debug.WriteLine($"UseCurrentMapExtent set to {value}, UseCustomExtent is now {_useCustomExtent}");
            }
        }

        private bool _useCustomExtent = false;
        public bool UseCustomExtent
        {
            get => _useCustomExtent;
            set
            {
                SetProperty(ref _useCustomExtent, value);
                // Only update UseCurrentMapExtent if setting UseCustomExtent to true
                if (value)
                {
                    UseCurrentMapExtent = false;
                }
                // Always raise can execute changed for the command
                (SetCustomExtentCommand as RelayCommand)?.RaiseCanExecuteChanged();

                // Add debug info
                System.Diagnostics.Debug.WriteLine($"UseCustomExtent set to {value}, UseCurrentMapExtent is now {_useCurrentMapExtent}");
            }
        }

        private bool _isLoading = true;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private Envelope _customExtent;
        public Envelope CustomExtent
        {
            get => _customExtent;
            set
            {
                SetProperty(ref _customExtent, value);
                // Update the display string and has-extent flag when extent changes
                HasCustomExtent = value != null;
                UpdateCustomExtentDisplay();
            }
        }

        private bool _hasCustomExtent;
        public bool HasCustomExtent
        {
            get => _hasCustomExtent;
            set => SetProperty(ref _hasCustomExtent, value);
        }

        private string _customExtentDisplay = "未设置自定义范围";
        public string CustomExtentDisplay
        {
            get => _customExtentDisplay;
            set => SetProperty(ref _customExtentDisplay, value);
        }

        private string _themeDescription = "选择一个主题以查看描述";
        public string ThemeDescription
        {
            get => _themeDescription;
            set => SetProperty(ref _themeDescription, value);
        }

        private string _estimatedFeatures = "--";
        public string EstimatedFeatures
        {
            get => _estimatedFeatures;
            set => SetProperty(ref _estimatedFeatures, value);
        }

        private string _estimatedSize = "--";
        public string EstimatedSize
        {
            get => _estimatedSize;
            set => SetProperty(ref _estimatedSize, value);
        }

        private string _themeIconText = "GlobeIcon"; // Default icon (globe)
        public string ThemeIconText
        {
            get => _themeIconText;
            set => SetProperty(ref _themeIconText, value);
        }

        private bool _createMfc = true;
        public bool CreateMfc
        {
            get => _createMfc;
            set => SetProperty(ref _createMfc, value);
        }

        private string _mfcOutputPath;
        public string MfcOutputPath
        {
            get => _mfcOutputPath;
            set => SetProperty(ref _mfcOutputPath, value);
        }

        private bool _isSharedMfc = true;
        public bool IsSharedMfc
        {
            get => _isSharedMfc;
            set => SetProperty(ref _isSharedMfc, value);
        }

        private string _dataOutputPath;
        public string DataOutputPath
        {
            get => _dataOutputPath;
            set => SetProperty(ref _dataOutputPath, value);
        }

        private bool _usePreviouslyLoadedData = true;
        public bool UsePreviouslyLoadedData
        {
            get => _usePreviouslyLoadedData;
            set
            {
                SetProperty(ref _usePreviouslyLoadedData, value);
                if (value)
                {
                    UseCustomDataFolder = false;
                }
            }
        }

        private bool _useCustomDataFolder = false;
        public bool UseCustomDataFolder
        {
            get => _useCustomDataFolder;
            set
            {
                SetProperty(ref _useCustomDataFolder, value);
                if (value)
                {
                    UsePreviouslyLoadedData = false;
                }
            }
        }

        private string _customDataFolderPath;
        public string CustomDataFolderPath
        {
            get => _customDataFolderPath;
            set => SetProperty(ref _customDataFolderPath, value);
        }

        // Track the last loaded data path for MFC creation
        private string _lastLoadedDataPath;

        private bool _isSelectAllChecked;
        public bool IsSelectAllChecked
        {
            get => _isSelectAllChecked;
            set
            {
                if (_isSelectAllChecked != value)
                {
                    // SetProperty will update the backing field and notify the UI.
                    SetProperty(ref _isSelectAllChecked, value, nameof(IsSelectAllChecked));
                    // Execute the logic to select/deselect all items based on the new value.
                    ExecuteSelectAllInternal(value);
                }
            }
        }

        private bool _isUpdatingSelectionInternally = false;

        #endregion

        #region Commands
        public ICommand LoadDataCommand { get; private set; }
        public ICommand ShowThemeInfoCommand { get; private set; }
        public ICommand SetCustomExtentCommand { get; private set; }
        public ICommand BrowseMfcLocationCommand { get; private set; }
        public ICommand BrowseDataLocationCommand { get; private set; }
        public ICommand BrowseCustomDataFolderCommand { get; private set; }
        public ICommand CreateMfcCommand { get; private set; }
        public ICommand GoToCreateMfcTabCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand SelectAllCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }
        #endregion

        #region Helper Methods
        private void AddToLog(string message)
        {
            // Append the new log entry to the end of the log
            LogOutput.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");

            // Update the text property
            LogOutputText = LogOutput.ToString();
            NotifyPropertyChanged(nameof(LogOutputText));
        }

        private void UpdateThemePreview()
        {
            string description = "选择一个主题或子主题以查看详细信息。";
            string icon = "GlobeIcon"; // Default

            var itemForPreview = SelectedItemForPreview; // The item currently focused in TreeView

            if (itemForPreview != null)
            {
                // Get description and icon from the parent theme typically
                string parentS3Key = itemForPreview.IsSelectable && itemForPreview.SubItems.Count == 0 && Themes.Any(t => t.ActualType == itemForPreview.ParentThemeForS3 && t.SubItems.Count == 0) ?
                                     itemForPreview.ActualType : // If it's a leaf parent (like "places")
                                     itemForPreview.ParentThemeForS3; // Otherwise, use the parent key

                description = ThemeDescriptions.TryGetValue(parentS3Key, out var desc)
                    ? desc
                    : "无描述信息。";

                if (itemForPreview.IsSelectable && itemForPreview.ParentThemeForS3 != itemForPreview.ActualType) // It's a sub-item
                {
                    description += "\nSub-theme: " + itemForPreview.DisplayName;
                }

                icon = ThemeIcons.TryGetValue(parentS3Key, out var iconName) ? iconName : "GlobeIcon";
            }

            // Calculate combined estimates for ALL selected leaf themes
            var allSelectedLeaves = GetSelectedLeafItems();
            if (allSelectedLeaves.Count > 0)
            {
                int totalEstimatedFeatures = 0;
                double totalSizeInKb = 0;

                foreach (var selectedLeaf in allSelectedLeaves)
                {
                    // Use the ActualType of the leaf item to get its specific estimate
                    if (ThemeFeatureEstimates.TryGetValue(selectedLeaf.ActualType, out int itemEstimate))
                    {
                        totalEstimatedFeatures += itemEstimate;
                        totalSizeInKb += itemEstimate * 2.5; // Assuming 2.5KB per feature
                    }
                    else
                    {
                        // Optional: Log if an estimate is missing for an actual type
                        System.Diagnostics.Debug.WriteLine($"Warning: No feature estimate found for ActualType: {selectedLeaf.ActualType}");
                    }
                }
                EstimatedFeatures = $"{totalEstimatedFeatures} total per sq km (approx.)";
                EstimatedSize = totalSizeInKb > 1024
                    ? $"{totalSizeInKb / 1024:F1} MB total per sq km (approx.)"
                    : $"{totalSizeInKb:F0} KB total per sq km (approx.)";

                if (allSelectedLeaves.Count == 1 && itemForPreview != null && itemForPreview == allSelectedLeaves.First()) // If only one item is selected, and it's the one being previewed
                {
                    // Use the ActualType of the itemForPreview to get its specific estimate
                    if (ThemeFeatureEstimates.TryGetValue(itemForPreview.ActualType, out int itemFeatures))
                    {
                        double itemSizeKb = itemFeatures * 2.5;
                        EstimatedFeatures = $"{itemFeatures} per sq km (approx. for {itemForPreview.DisplayName})";
                        EstimatedSize = itemSizeKb > 1024
                            ? $"{itemSizeKb / 1024:F1} MB per sq km (approx. for {itemForPreview.DisplayName})"
                            : $"{itemSizeKb:F0} KB per sq km (approx. for {itemForPreview.DisplayName})";
                    }
                    else
                    {
                        // Fallback if specific estimate is missing for the single selected item
                        EstimatedFeatures = $"-- per sq km (approx. for {itemForPreview.DisplayName})";
                        EstimatedSize = $"-- MB/KB per sq km (approx. for {itemForPreview.DisplayName})";
                        System.Diagnostics.Debug.WriteLine($"Warning: No feature estimate for single selected ActualType: {itemForPreview.ActualType}");
                    }
                }
            }
            else // No items are selected
            {
                // If nothing is selected, but an item is focused for preview, show its individual estimate
                if (itemForPreview != null && itemForPreview.IsSelectable) // Check if the preview item is a selectable leaf
                {
                    // Use the ActualType of the itemForPreview to get its specific estimate
                    if (ThemeFeatureEstimates.TryGetValue(itemForPreview.ActualType, out int itemFeat))
                    {
                        double itemSzKb = itemFeat * 2.5;
                        EstimatedFeatures = $"{itemFeat} per sq km (approx. for {itemForPreview.DisplayName})";
                        EstimatedSize = itemSzKb > 1024
                            ? $"{itemSzKb / 1024:F1} MB per sq km (approx. for {itemForPreview.DisplayName})"
                            : $"{itemSzKb:F0} KB per sq km (approx. for {itemForPreview.DisplayName})";
                    }
                    else
                    {
                        // Fallback if specific estimate is missing for the focused item
                        EstimatedFeatures = $"-- per sq km (approx. for {itemForPreview.DisplayName})";
                        EstimatedSize = $"-- MB/KB per sq km (approx. for {itemForPreview.DisplayName})";
                        System.Diagnostics.Debug.WriteLine($"Warning: No feature estimate for focused ActualType: {itemForPreview.ActualType}");
                    }
                }
                else // Nothing selected and no specific leaf item focused for preview
                {
                    EstimatedFeatures = "--";
                    EstimatedSize = "--";
                }
            }

            ThemeDescription = description;
            ThemeIconText = icon;
            // EstimatedFeatures and EstimatedSize are set above
            NotifyPropertyChanged(nameof(ThemeDescription));
            NotifyPropertyChanged(nameof(EstimatedFeatures));
            NotifyPropertyChanged(nameof(EstimatedSize));
            NotifyPropertyChanged(nameof(ThemeIconText));
            NotifyPropertyChanged(nameof(SelectedLeafItemCount));
            NotifyPropertyChanged(nameof(AllSelectedLeafItemsForPreview));
            UpdateIsSelectAllCheckedStatus(); // Ensure "Select All" checkbox reflects current state
        }

        private void ShowThemeInfo()
        {
            if (SelectedItemForPreview == null) return;

            var item = SelectedItemForPreview;
            string parentS3Key = item.ParentThemeForS3;

            string description = ThemeDescriptions.TryGetValue(parentS3Key, out string themeDesc)
                ? themeDesc
                : "无详细信息可用。";

            string typesInfo = $"S3 主题: {parentS3Key}";
            if (item.IsSelectable && item.ParentThemeForS3 != item.ActualType) // It's a sub-item
            {
                description = $"父级: {MakeFriendlyName(parentS3Key)}\n子主题: {item.DisplayName}\n\n{description}";
                typesInfo += $", S3 类型: {item.ActualType}";
            }
            else // It's a parent item (either leaf or just for preview)
            {
                typesInfo += $", S3 类型: {_overtureS3ThemeTypes[parentS3Key]}";
            }

            var selectedLeafItems = GetSelectedLeafItems();
            string selectedCount = selectedLeafItems.Count > 0 ?
                $"\n\nYou have selected {selectedLeafItems.Count} specific data type(s) in total."
                : "";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                $"{description}\n\n{typesInfo}{selectedCount}",
                $"关于 '{item.DisplayName}'",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void ShowHelp()
        {
            string helpMessage = @"关于 Overture Maps 数据加载器

这是一个用于加载和处理 Overture Maps 数据的工具，基于开源项目进行改进：
https://github.com/COF-RyLopez/ArcGISPro-GeoParquet-Addin

主要功能：
• 从 Overture Maps 下载 GeoParquet 格式的地理数据
• 支持多种数据主题：建筑物、地点、交通、行政边界等
• 创建多文件要素连接 (MFC) 以便在 ArcGIS Pro 中使用
• 自定义数据范围和输出位置

使用步骤：
1. 选择数据主题和类型
2. 设置数据范围（当前地图范围或自定义范围）
3. 点击'加载数据'下载并处理数据
4. 可选择创建 MFC 文件以便重复使用

注意事项：
• 需要稳定的网络连接来下载数据
• 大范围数据可能需要较长时间处理
• 建议先在小范围内测试

技术支持：
如有问题，请查看 ArcGIS Pro 日志或联系开发团队。

版权信息：
基于 COF-RyLopez 的开源项目改进
集成到 XIAOFU 工具箱中";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                helpMessage,
                "Overture Maps 数据加载器 - 帮助",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        private void SetCustomExtent()
        {
            try
            {
                // Add diagnostic logging
                System.Diagnostics.Debug.WriteLine("SetCustomExtent method called");
                AddToLog("调用SetCustomExtent方法 - 尝试激活绘图工具");

                // Ensure the custom extent radio button is selected
                UseCustomExtent = true;

                // Make sure we're subscribed to the static event
                // We already subscribed in the constructor, but ensure it's still active
                try
                {
                    // Remove any existing subscription and add it again to be safe
                    // This prevents multiple handlers if called multiple times
                    CustomExtentTool.ExtentCreatedStatic -= OnExtentCreated;
                    CustomExtentTool.ExtentCreatedStatic += OnExtentCreated;
                    System.Diagnostics.Debug.WriteLine("Re-established event subscription for CustomExtentTool");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error managing event subscriptions: {ex.Message}");
                }

                // Create the instance tool as well for backward compatibility
                if (_customExtentTool == null)
                {
                    _customExtentTool = new CustomExtentTool();
                    _customExtentTool.ExtentCreated += OnExtentCreated;
                }

                // Use ArcGIS Pro's drawing tool to select an extent
                QueuedTask.Run(async () =>
                {
                    System.Diagnostics.Debug.WriteLine("Inside QueuedTask.Run");
                    AddToLog("开始自定义范围绘制操作...");

                    // Get a reference to the active map and make sure one exists
                    var mapView = MapView.Active;
                    if (mapView == null)
                    {
                        AddToLog("无法设置自定义范围: 没有活动的地图视图");
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                            "请在设置自定义范围之前打开地图。",
                            "无活动地图");
                        return;
                    }

                    AddToLog($"找到活动地图视图: {mapView.Map.Name}");

                    try
                    {
                        // Activate our custom tool
                        AddToLog("激活自定义绘图工具...");
                        System.Diagnostics.Debug.WriteLine("Activating custom extent tool");

                        // Use our custom tool by ID as defined in the Config.daml
                        await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_CustomExtentTool");
                        AddToLog("在地图上绘制矩形以设置自定义范围...");
                        System.Diagnostics.Debug.WriteLine("Custom tool activated successfully");
                    }
                    catch (Exception ex)
                    {
                        AddToLog($"工具激活错误: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Exception in tool activation: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                AddToLog($"设置自定义范围错误: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Exception in SetCustomExtent: {ex}");
            }
        }

        // Handler for when our custom tool creates an extent
        private void OnExtentCreated(Envelope extent)
        {
            // Add more detailed logging
            System.Diagnostics.Debug.WriteLine($"Custom extent created: {extent.XMin}, {extent.YMin}, {extent.XMax}, {extent.YMax}");

            Envelope extentInWGS84 = extent;
            if (extent.SpatialReference == null || extent.SpatialReference.Wkid != 4326)
            {
                AddToLog("Custom extent is not in WGS84. Projecting...");
                System.Diagnostics.Debug.WriteLine($"Original SR WKID: {extent.SpatialReference?.Wkid}");
                SpatialReference wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);
                try
                {
                    extentInWGS84 = GeometryEngine.Instance.Project(extent, wgs84) as Envelope;
                    if (extentInWGS84 != null)
                    {
                        AddToLog($"Successfully projected custom extent to WGS84: {extentInWGS84.XMin:F6}, {extentInWGS84.YMin:F6}, {extentInWGS84.XMax:F6}, {extentInWGS84.YMax:F6}");
                        System.Diagnostics.Debug.WriteLine($"Projected extent: {extentInWGS84.XMin}, {extentInWGS84.YMin}, {extentInWGS84.XMax}, {extentInWGS84.YMax}");
                    }
                    else
                    {
                        AddToLog("ERROR: Projection to WGS84 resulted in a null envelope. Using original extent.");
                        System.Diagnostics.Debug.WriteLine("ERROR: Projection to WGS84 resulted in a null envelope.");
                        extentInWGS84 = extent; // Fallback to original if projection fails
                    }
                }
                catch (Exception ex)
                {
                    AddToLog($"ERROR: Failed to project custom extent to WGS84: {ex.Message}. Using original extent.");
                    System.Diagnostics.Debug.WriteLine($"ERROR projecting extent: {ex.Message}");
                    extentInWGS84 = extent; // Fallback to original on error
                }
            }
            else
            {
                AddToLog("Custom extent is already in WGS84 or has no spatial reference, assuming WGS84.");
                System.Diagnostics.Debug.WriteLine("Custom extent is already WGS84 or no SR defined.");
            }

            // Store the extent (potentially projected) - this will trigger the property change handlers
            CustomExtent = extentInWGS84;

            // Explicitly set these properties to ensure UI updates
            HasCustomExtent = true;
            UpdateCustomExtentDisplay();
            NotifyPropertyChanged(nameof(CustomExtentDisplay));
            NotifyPropertyChanged(nameof(HasCustomExtent));

            // Make sure custom extent radio is selected
            UseCustomExtent = true; // This will also set UseCurrentMapExtent = false via its setter

            // Ensure tool is deactivated and provide feedback
            QueuedTask.Run(async () => {
                try
                {
                    var mapView = MapView.Active;
                    if (mapView != null)
                    {
                        mapView.CancelDrawing();
                        System.Diagnostics.Debug.WriteLine("MapView.CancelDrawing() called.");
                    }

                    // Deactivate the custom drawing tool and return to the default explore tool
                    // First, explicitly deactivate the current tool (which should be our CustomExtentTool)
                    await FrameworkApplication.SetCurrentToolAsync(null);
                    System.Diagnostics.Debug.WriteLine("Current tool explicitly deactivated (set to null).");

                    // Then, activate the default explore tool
                    await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
                    System.Diagnostics.Debug.WriteLine("Default explore tool activated.");

                    // Give the UI thread a moment for the cursor to update etc.
                    await Task.Delay(300); // Increased delay slightly just in case

                    // Now that the tool is reset, log the next steps and show confirmation
                    AddToLog("自定义范围设置成功，绘图工具已停用。");
                    AddToLog("自定义范围将用于数据加载。"); // User's referenced log
                    AddToLog("您现在可以选择数据主题并点击'加载数据'。");

                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        $"自定义范围设置成功：\n最小 X,Y: {extent.XMin:F4}, {extent.YMin:F4}\n最大 X,Y: {extent.XMax:F4}, {extent.YMax:F4}",
                        "自定义范围已设置",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during tool deactivation or showing custom extent feedback: {ex.Message}");
                    AddToLog($"Error after setting extent: {ex.Message}");
                }
            });

            // The NotifyPropertyChanged calls for UseCustomExtent and UseCurrentMapExtent
        } // <--- Added missing right curly bracket here

        private void BrowseMfcLocation()
        {
            var initial = MfcOutputPath ?? Path.Combine(DeterminedDefaultMfcBasePath, "Connections");
            var picked = PathDialogUtils.PickFolder("选择MFC文件(.mfc)的保存文件夹", initial);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                MfcOutputPath = picked;
                AddToLog($"MFC连接文件将保存在: {MfcOutputPath}");
                AddToLog($"请确保您的GeoParquet数据文件位于: {DataOutputPath}");
            }
        }

        private void BrowseDataLocation()
        {
            var initial = DataOutputPath ?? Path.Combine(DeterminedDefaultMfcBasePath, "Data");
            var picked = PathDialogUtils.PickFolder("选择GeoParquet数据文件的保存文件夹", initial);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                DataOutputPath = picked;
                AddToLog($"数据文件将保存到: {DataOutputPath}");
            }
        }

        private void BrowseCustomDataFolder()
        {
            var initial = CustomDataFolderPath ?? DataOutputPath ?? Path.Combine(DeterminedDefaultMfcBasePath, "Data");
            var picked = PathDialogUtils.PickFolder("选择包含GeoParquet数据文件的文件夹", initial);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                CustomDataFolderPath = picked;
                AddToLog($"自定义数据文件夹设置为: {CustomDataFolderPath}");
                (CreateMfcCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void UpdateCustomExtentDisplay()
        {
            if (_customExtent != null)
            {
                CustomExtentDisplay = $"Min X: {_customExtent.XMin:F4}\nMin Y: {_customExtent.YMin:F4}\nMax X: {_customExtent.XMax:F4}\nMax Y: {_customExtent.YMax:F4}";
            }
            else
            {
                CustomExtentDisplay = "未设置自定义范围";
            }
        }
        #endregion

        private async Task<string> GetLatestRelease()
        {
            try
            {
                // Execute network request in background thread using ArcGIS Pro's threading model
                return await QueuedTask.Run(async () =>
                {
                    return await RetryAsync(async () =>
                    {
                        var response = await _httpClient.GetStringAsync(RELEASE_URL);
                        System.Diagnostics.Debug.WriteLine($"Release API Response: {response}");

                        // UI updates must be dispatched back to UI thread
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AddToLog("已从 Overture Maps API 接收版本信息");
                        });

                        var releaseInfo = JsonSerializer.Deserialize<ReleaseInfo>(response, _jsonOptions)
                            ?? throw new Exception("反序列化版本信息失败");

                        System.Diagnostics.Debug.WriteLine($"Deserialized Latest Release: {releaseInfo.Latest}");

                        // UI updates must be dispatched back to UI thread
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AddToLog($"可用的最新版本: {releaseInfo.Latest}");
                        });

                        return releaseInfo.Latest;
                    });
                });
            }
            catch (Exception ex)
            {
                AddToLog($"获取最新版本失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error getting latest release: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Provides periodic heartbeat feedback during long-running operations
        /// </summary>
        private async Task StartHeartbeatAsync(string itemName, CancellationToken cancellationToken)
        {
            int heartbeatCount = 0;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(10000, cancellationToken); // Every 10 seconds
                    heartbeatCount++;

                    var timeElapsed = heartbeatCount * 10;
                    StatusText = $"Still loading {itemName}... ({timeElapsed}s elapsed)";
                    AddToLog($"⏱️ Still working on {itemName} ({timeElapsed} seconds elapsed)...");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when operation completes
            }
        }

        /// <summary>
        /// Performs bulk folder deletion and layer removal asynchronously to prevent UI blocking
        /// </summary>
        private async Task PerformBulkDataReplacementAsync(List<SelectableThemeItem> selectedItems, string dataPath)
        {
            try
            {
                StatusText = "Removing existing layers from map...";

                // First, collect all unique actualS3Type folders that need to be deleted
                var typeFoldersToDelete = selectedItems
                    .Select(item => Path.Combine(dataPath, item.ActualType))
                    .Where(Directory.Exists)
                    .Distinct()
                    .ToList();

                AddToLog($"Found {typeFoldersToDelete.Count} theme folders to clean up");

                if (typeFoldersToDelete.Count == 0)
                {
                    return; // Nothing to delete
                }

                // Remove layers that use files from these folders asynchronously
                for (int i = 0; i < typeFoldersToDelete.Count; i++)
                {
                    var folderPath = typeFoldersToDelete[i];
                    var folderName = Path.GetFileName(folderPath);

                    StatusText = $"Removing layers for {folderName} ({i + 1}/{typeFoldersToDelete.Count})...";
                    AddToLog($"Removing layers using data from folder: {folderName}");

                    // Remove layers asynchronously with proper UI thread handling
                    await RemoveLayersUsingFolderAsync(folderPath);

                    // Small delay to allow UI updates
                    await Task.Delay(100);
                }

                StatusText = "Deleting existing data folders...";
                AddToLog("All layers removed. Now deleting data folders...");

                // Now delete the folders asynchronously
                for (int i = 0; i < typeFoldersToDelete.Count; i++)
                {
                    var folderPath = typeFoldersToDelete[i];
                    var folderName = Path.GetFileName(folderPath);

                    StatusText = $"Deleting {folderName} folder ({i + 1}/{typeFoldersToDelete.Count})...";
                    AddToLog($"Deleting folder: {folderName}");

                    // Delete folder asynchronously to prevent UI blocking
                    await Task.Run(() =>
                    {
                        try
                        {
                            if (Directory.Exists(folderPath))
                            {
                                Directory.Delete(folderPath, recursive: true);
                                System.Diagnostics.Debug.WriteLine($"Successfully deleted folder: {folderPath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error deleting folder {folderPath}: {ex.Message}");
                            // Log but don't throw - we'll continue with other folders
                        }
                    });

                    // Small delay to allow UI updates
                    await Task.Delay(50);
                }

                StatusText = "Data cleanup completed. Ready to load new data...";
                AddToLog("Bulk folder deletion completed successfully");
            }
            catch (Exception ex)
            {
                AddToLog($"Warning during bulk cleanup: {ex.Message}");
                StatusText = "Cleanup completed with warnings. Continuing with data load...";
                System.Diagnostics.Debug.WriteLine($"Error in PerformBulkDataReplacementAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes all layers that use files from the specified folder
        /// </summary>
        private async Task RemoveLayersUsingFolderAsync(string folderPath)
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null) return;

                    var membersToRemove = new List<MapMember>();
                    var allLayers = map.GetLayersAsFlattenedList().ToList();
                    var allTables = map.GetStandaloneTablesAsFlattenedList().ToList();

                    string normalizedFolderPath = Path.GetFullPath(folderPath).ToLowerInvariant();

                    // Helper to check if a file path is within the target folder
                    bool IsFileInTargetFolder(string filePath)
                    {
                        if (string.IsNullOrEmpty(filePath)) return false;
                        try
                        {
                            string normalizedFilePath = Path.GetFullPath(filePath).ToLowerInvariant();
                            return normalizedFilePath.StartsWith(normalizedFolderPath, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    }

                    // Check layers
                    foreach (var layer in allLayers)
                    {
                        if (layer is FeatureLayer featureLayer)
                        {
                            try
                            {
                                using var fc = featureLayer.GetFeatureClass();
                                if (fc != null)
                                {
                                    var fcPathUri = fc.GetPath();
                                    if (fcPathUri != null && fcPathUri.IsFile)
                                    {
                                        if (IsFileInTargetFolder(fcPathUri.LocalPath))
                                        {
                                            membersToRemove.Add(featureLayer);
                                            System.Diagnostics.Debug.WriteLine($"Marked layer '{featureLayer.Name}' for removal (folder cleanup)");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error checking layer '{featureLayer.Name}': {ex.Message}");
                            }
                        }
                    }

                    // Check standalone tables
                    foreach (var tableMember in allTables)
                    {
                        if (tableMember is StandaloneTable standaloneTable)
                        {
                            try
                            {
                                using var tbl = standaloneTable.GetTable();
                                if (tbl != null)
                                {
                                    var tblPathUri = tbl.GetPath();
                                    if (tblPathUri != null && tblPathUri.IsFile)
                                    {
                                        if (IsFileInTargetFolder(tblPathUri.LocalPath))
                                        {
                                            membersToRemove.Add(standaloneTable);
                                            System.Diagnostics.Debug.WriteLine($"Marked table '{standaloneTable.Name}' for removal (folder cleanup)");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error checking table '{standaloneTable.Name}': {ex.Message}");
                            }
                        }
                    }

                    // Remove the identified members
                    if (membersToRemove.Count > 0)
                    {
                        var distinctMembersToRemove = membersToRemove.Distinct().ToList();
                        System.Diagnostics.Debug.WriteLine($"Removing {distinctMembersToRemove.Count} map members for folder: {folderPath}");

                        foreach (var member in distinctMembersToRemove)
                        {
                            if (member is Layer layerToRemove)
                            {
                                map.RemoveLayer(layerToRemove);
                                (layerToRemove as IDisposable)?.Dispose();
                            }
                            else if (member is StandaloneTable tableToRemove)
                            {
                                map.RemoveStandaloneTable(tableToRemove);
                                (tableToRemove as IDisposable)?.Dispose();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in RemoveLayersUsingFolderAsync: {ex.Message}");
                }
            });
        }

        private async Task LoadOvertureDataAsync()
        {
            try
            {
                var selectedLeafItems = GetSelectedLeafItems();
                if (selectedLeafItems.Count == 0)
                {
                    AddToLog("未选择任何主题或子主题。");
                    return;
                }

                // Initialize a new cancellation token source
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                var cancellationToken = _cts.Token;

                // Switch to status tab
                SelectedTabIndex = 1;

                StatusText = $"正在加载 {selectedLeafItems.Count} 个选定的数据类型...";
                AddToLog($"开始从版本 {LatestRelease} 加载 {selectedLeafItems.Count} 个数据类型");
                AddToLog($"预计处理时间: {EstimateProcessingTime(selectedLeafItems.Count)} 分钟");

                // Get map extent
                Envelope extent = null;
                await QueuedTask.Run(() =>
                {
                    SpatialReference wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326); // WGS84

                    if (UseCurrentMapExtent && MapView.Active != null)
                    {
                        Envelope mapExtent = MapView.Active.Extent;
                        if (mapExtent != null)
                        {
                            if (mapExtent.SpatialReference == null || mapExtent.SpatialReference.Wkid != 4326)
                            {
                                AddToLog($"Map extent SR is {mapExtent.SpatialReference?.Wkid.ToString() ?? "null"}, projecting to WGS84 (4326).");
                                try
                                {
                                    extent = GeometryEngine.Instance.Project(mapExtent, wgs84) as Envelope;
                                    if (extent == null)
                                    {
                                        AddToLog("Warning: Map extent projection to WGS84 resulted in null. Original extent might be invalid or projection failed.");
                                        // Attempt to use original extent if projection fails catastrophically, though it might lead to issues.
                                        // Or, decide to stop if projection is critical. For now, logging and using original as fallback.
                                        extent = mapExtent; // Fallback to original, though this might be problematic.
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AddToLog($"Error projecting map extent: {ex.Message}. Using original extent values, which might be incorrect for filtering.");
                                    System.Diagnostics.Debug.WriteLine($"Error projecting map extent: {ex.Message}");
                                    extent = mapExtent; // Fallback
                                }
                            }
                            else
                            {
                                AddToLog("Map extent is already in WGS84.");
                                extent = mapExtent;
                            }
                            AddToLog($"Using WGS84 extent from map: {extent.XMin:F6}, {extent.YMin:F6}, {extent.XMax:F6}, {extent.YMax:F6}");
                            System.Diagnostics.Debug.WriteLine($"WGS84 extent from map: {extent.XMin}, {extent.YMin}, {extent.XMax}, {extent.YMax}");
                        }
                        else
                        {
                            AddToLog("Map extent is null.");
                        }
                    }
                    else if (UseCustomExtent && CustomExtent != null) // CustomExtent should already be in WGS84
                    {
                        extent = CustomExtent; // Assumes CustomExtent is now always WGS84
                        AddToLog($"Using custom WGS84 extent: {extent.XMin:F6}, {extent.YMin:F6}, {extent.XMax:F6}, {extent.YMax:F6}");
                        System.Diagnostics.Debug.WriteLine($"Using custom WGS84 extent: {extent.XMin}, {extent.YMin}, {extent.XMax}, {extent.YMax}");
                    }
                    else
                    {
                        AddToLog("No extent specified or available for filtering.");
                    }
                });

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    StatusText = "Operation cancelled";
                    AddToLog("Operation was cancelled");
                    return;
                }

                // Check if any of the themes already have data in the target location
                bool existingDataFound = false;
                string dataPath = DataOutputPath;

                // Check if the folder exists and has theme folders that match selected themes
                if (Directory.Exists(dataPath))
                {
                    foreach (var selectedItem in selectedLeafItems) // Check based on what will be loaded
                    {
                        // New path structure: DataOutputPath / {ActualS3Type} / *.parquet
                        var actualTypeSpecificDataPath = Path.Combine(dataPath, selectedItem.ActualType);
                        if (Directory.Exists(actualTypeSpecificDataPath) && Directory.EnumerateFiles(actualTypeSpecificDataPath, "*.parquet").Any())
                        {
                            existingDataFound = true;
                            break; // Found data for at least one type, no need to check further
                        }
                    }
                }

                // If existing data is found, confirm with user before overwriting
                if (existingDataFound)
                {
                    var confirmResult = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        "发现一个或多个选定主题的现有数据。加载新数据将替换现有文件。\n\n您要继续吗？",
                        "替换现有数据？",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (confirmResult == System.Windows.MessageBoxResult.No)
                    {
                        StatusText = "Operation cancelled by user";
                        AddToLog("Operation cancelled - user chose not to replace existing data");
                        return;
                    }

                    AddToLog("User confirmed replacing existing data");

                    // Perform bulk folder deletion with progress feedback to prevent UI blocking
                    StatusText = "Preparing to replace existing data...";
                    AddToLog("Removing existing layers and deleting theme folders...");
                    await PerformBulkDataReplacementAsync(selectedLeafItems, dataPath);
                    AddToLog("Existing data cleanup completed. Beginning new data loading...");
                }

                int totalDataTypesToProcess = selectedLeafItems.Count;
                int processedDataTypes = 0;

                // Process each selected leaf item (sub-theme or leaf parent theme)
                for (int itemIndex = 0; itemIndex < selectedLeafItems.Count; itemIndex++)
                {
                    var itemToLoad = selectedLeafItems[itemIndex];

                    // Check for cancellation between items
                    if (cancellationToken.IsCancellationRequested)
                    {
                        StatusText = "操作已取消";
                        AddToLog("操作已取消");
                        return;
                    }

                    string parentS3Theme = itemToLoad.ParentThemeForS3;
                    string actualS3Type = itemToLoad.ActualType;
                    string itemDisplayName = itemToLoad.DisplayName; // For logging and layer naming

                    // Enhanced status and logging with better visibility
                    StatusText = $"Processing {itemIndex + 1} of {totalDataTypesToProcess}: {MakeFriendlyName(parentS3Theme)} / {itemDisplayName}";
                    AddToLog($"Processing: {MakeFriendlyName(parentS3Theme)} / {itemDisplayName}");
                    AddToLog($"Data type for S3: theme='{parentS3Theme}', type='{actualS3Type}'");
                    System.Diagnostics.Debug.WriteLine($"Data type for S3: theme='{parentS3Theme}', type='{actualS3Type}'");

                    string trimmedRelease = LatestRelease?.Trim() ?? "";
                    string s3Path = trimmedRelease.Length > 0
                ? $"{S3_BASE_PATH}/{trimmedRelease}/theme={parentS3Theme}/type={actualS3Type}/*.parquet"
                : $"{S3_BASE_PATH}/theme={parentS3Theme}/type={actualS3Type}/*.parquet";

                    AddToLog($"从 S3 路径加载: {s3Path}");
                    System.Diagnostics.Debug.WriteLine($"Loading from S3 path: {s3Path}");

                    // Add explicit UI yield before heavy operations
                    await Task.Delay(50); // Allow UI to update

                    // Create a detailed progress reporter for the S3 data loading phase with heartbeat
                    var ingestProgressReporter = new Progress<string>(status =>
                    {
                        StatusText = status;
                        AddToLog(status);
                        // Update progress to show activity within current item
                        var baseProgress = (processedDataTypes * 100.0) / totalDataTypesToProcess;
                        var ingestProgress = 1.5; // Small increment for S3 loading
                        ProgressValue = Math.Min(baseProgress + ingestProgress, 98.0);
                    });

                    // Add heartbeat timer for long-running S3 operations
                    using var heartbeatCts = new CancellationTokenSource();
                    var heartbeatTask = StartHeartbeatAsync(itemDisplayName, heartbeatCts.Token);

                    StatusText = $"正在从 S3 加载 {itemDisplayName} (可能需要 30-60 秒)...";
                    AddToLog($"⏳ 开始为 {itemDisplayName} 加载 S3 数据 - 请稍候，此操作可能需要一些时间...");

                    bool ingestSuccess = await _dataProcessor.IngestFileAsync(s3Path, extent, actualS3Type, ingestProgressReporter);

                    // Stop heartbeat
                    heartbeatCts.Cancel();
                    try { await heartbeatTask; } catch (OperationCanceledException) { /* Expected */ }

                    if (!ingestSuccess)
                    {
                        AddToLog($"❌ 从 {s3Path} 加载数据失败");
                        StatusText = $"从 {s3Path} 加载数据时出错";
                        continue; // Skip to next item
                    }

                    AddToLog($"✅ 成功从 S3 加载 {itemDisplayName} 数据");

                    if (cancellationToken.IsCancellationRequested)
                    {
                        StatusText = "Operation cancelled";
                        AddToLog("Operation was cancelled");
                        return;
                    }

                    // Add UI yield before layer creation
                    await Task.Delay(50);

                    // Create a feature layer for the loaded data
                    string featureLayerName = $"{MakeFriendlyName(parentS3Theme)} - {itemDisplayName}";
                    StatusText = $"Creating layers for {itemDisplayName}...";
                    AddToLog($"🔄 Creating feature layers for {itemDisplayName}...");

                    var itemProgressReporter = new Progress<string>(status =>
                    {
                        StatusText = status;
                        AddToLog(status);
                        // Update progress bar with more granular feedback during processing
                        var baseProgress = (processedDataTypes * 100.0) / totalDataTypesToProcess;
                        var itemProgress = 3.0; // Small increment for within-item progress
                        ProgressValue = Math.Min(baseProgress + itemProgress, 99.0); // Don't hit 100 until truly done
                    });

                    await _dataProcessor.CreateFeatureLayerAsync(featureLayerName, itemProgressReporter, parentS3Theme, actualS3Type, DataOutputPath);

                    processedDataTypes++;
                    ProgressValue = (processedDataTypes * 100.0) / totalDataTypesToProcess;

                    StatusText = $"✅ Completed {itemDisplayName} ({processedDataTypes}/{totalDataTypesToProcess})";
                    AddToLog($"✅ Successfully loaded {itemDisplayName} for {MakeFriendlyName(parentS3Theme)}");

                    // Add small delay between items to ensure UI responsiveness
                    await Task.Delay(100);
                }

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    StatusText = "Operation cancelled";
                    AddToLog("Operation was cancelled");
                    return;
                }

                // Now that all data is processed, add all layers to map in optimal stacking order
                StatusText = "Adding layers to map in optimal stacking order...";
                AddToLog("🗺️ All data exported successfully. Now adding layers to map with optimal stacking order...");

                // Add UI yield before final layer creation
                await Task.Delay(100);

                var progressReporter = new Progress<string>(status =>
                {
                    StatusText = status;
                    AddToLog(status);
                });

                await _dataProcessor.AddAllLayersToMapAsync(progressReporter);

                // Store the data path for potential MFC creation later
                _lastLoadedDataPath = DataOutputPath;

                // Now that the data is loaded, inform the user they can create an MFC if desired
                AddToLog("----------------");
                AddToLog("数据加载完成。您现在可以:");
                AddToLog("1. 直接使用加载的GeoParquet数据");
                AddToLog("2. 从'创建MFC'选项卡创建多文件要素连接(MFC)");
                AddToLog("注意: 图层已按最佳绘制顺序添加(点在顶部 → 线 → 面在底部)");
                AddToLog("----------------");

                // Show a message box offering to go to the Create MFC tab
                var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "数据加载完成。您现在要创建多文件要素连接(MFC)吗？",
                    "创建MFC？",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // Navigate to the Create MFC tab
                    ShowCreateMfcTab();
                }

                // Update the Create MFC command can-execute state
                (CreateMfcCommand as RelayCommand)?.RaiseCanExecuteChanged();

                StatusText = $"Successfully loaded all selected themes from release {LatestRelease}";
                AddToLog($"所有选定主题加载成功");
                AddToLog("----------------");
                if (extent != null)
                {
                    AddToLog($"范围数据: {extent.XMin:F2}, {extent.YMin:F2}, {extent.XMax:F2}, {extent.YMax:F2}");
                    AddToLog("当您为不同范围加载数据时，现有数据将被替换。");
                    AddToLog("这确保了MFC创建的文件夹结构清洁。");
                    AddToLog("如果不想覆盖，请重命名输出文件夹OvertureProAddinData");
                }
                AddToLog("----------------");
                ProgressValue = 100;
            }
            catch (Exception ex)
            {
                // Determine if this is a file access issue
                bool isFileAccessError = ex.Message.Contains("because it is being used by another process") ||
                                         ex.Message.Contains("access") ||
                                         ex.Message.Contains("denied") ||
                                         ex.Message.Contains("locked");

                bool isNetworkError = ex.Message.Contains("network") ||
                                     ex.Message.Contains("timeout") ||
                                     ex.Message.Contains("connection") ||
                                     ex.Message.Contains("DNS") ||
                                     ex.Message.Contains("SSL");

                if (isFileAccessError)
                {
                    StatusText = "文件访问错误";
                    AddToLog($"错误: 文件访问错误。一个或多个文件被其他进程锁定。");
                    AddToLog($"请尝试以下解决方案:");
                    AddToLog($"1. 关闭可能正在使用该数据的其他ArcGIS Pro项目");
                    AddToLog($"2. 从当前地图中移除使用Overture数据的图层");
                    AddToLog($"3. 在极端情况下，重启ArcGIS Pro后重试");
                    AddToLog($"详细错误: {ex.Message}");
                }
                else if (isNetworkError)
                {
                    StatusText = "网络连接错误";
                    AddToLog($"错误: 网络连接问题。");
                    AddToLog($"请检查以下项目:");
                    AddToLog($"1. 确保网络连接正常");
                    AddToLog($"2. 检查防火墙设置是否阻止了AWS S3访问");
                    AddToLog($"3. 如果在企业网络中，请联系网络管理员");
                    AddToLog($"4. 稍后重试，服务器可能暂时不可用");
                    AddToLog($"技术详情: {ex.Message}");
                }
                else
                {
                    StatusText = $"数据加载错误: {ex.Message}";
                    AddToLog($"错误: {ex.Message}");
                    AddToLog($"堆栈跟踪: {ex.StackTrace}");
                }

                ProgressValue = 0;
                System.Diagnostics.Debug.WriteLine($"Load error: {ex}");
            }
            finally
            {
                // Clean up the cancellation token source
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }
        }

        private async Task CreateMfcAsync()
        {
            try
            {
                // Initialize a new cancellation token source
                _cts?.Dispose();
                _cts = new CancellationTokenSource();
                var cancellationToken = _cts.Token;

                // Switch to status tab
                SelectedTabIndex = 1; // Status tab is now index 1

                StatusText = "Creating Multifile Feature Connection...";
                AddToLog("为数据设置多文件要素连接");
                ProgressValue = 0;

                // Determine the data source folder
                string dataFolder;

                if (UsePreviouslyLoadedData && !string.IsNullOrEmpty(_lastLoadedDataPath))
                {
                    dataFolder = _lastLoadedDataPath;
                    AddToLog($"使用之前加载的数据: {dataFolder}");
                }
                else if (UseCustomDataFolder && !string.IsNullOrEmpty(CustomDataFolderPath))
                {
                    dataFolder = CustomDataFolderPath;
                    AddToLog($"使用自定义数据文件夹: {dataFolder}");
                }
                else
                {
                    StatusText = "Error: No valid data folder specified";
                    AddToLog("ERROR: No valid data folder specified for MFC creation");
                    return;
                }

                // Ensure connection folder exists
                string connectionFolder = MfcOutputPath;
                if (!Directory.Exists(connectionFolder))
                {
                    AddToLog($"Creating connection folder: {connectionFolder}");
                    Directory.CreateDirectory(connectionFolder);
                }

                // Check if data folder exists and has content
                if (!Directory.Exists(dataFolder))
                {
                    AddToLog($"ERROR: Data folder does not exist: {dataFolder}");
                    StatusText = "Error creating Multifile Feature Connection - data folder not found";
                    return;
                }

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    StatusText = "Operation cancelled";
                    AddToLog("Operation was cancelled during MFC preparation");
                    return;
                }

                // Do a sanity check on the data folder contents
                int fileCount = Directory.GetFiles(dataFolder, "*.parquet", SearchOption.AllDirectories).Length;
                AddToLog($"在数据文件夹中找到 {fileCount} 个parquet文件");

                if (fileCount == 0)
                {
                    // Check if theme folders were created
                    var themeFolders = Directory.GetDirectories(dataFolder);
                    AddToLog($"Found {themeFolders.Length} theme folders in {dataFolder}");

                    foreach (var folder in themeFolders)
                    {
                        AddToLog($"Theme folder: {Path.GetFileName(folder)}");
                        var typeFolders = Directory.GetDirectories(folder);
                        AddToLog($"  Contains {typeFolders.Length} type folders");

                        foreach (var typeFolder in typeFolders)
                        {
                            var filesInType = Directory.GetFiles(typeFolder, "*.parquet");
                            AddToLog($"  Type folder {Path.GetFileName(typeFolder)} contains {filesInType.Length} parquet files");
                        }
                    }

                    if (fileCount == 0)
                    {
                        AddToLog("在指定文件夹中未找到数据文件。无法创建MFC。");
                        StatusText = "Error creating Multifile Feature Connection - no data files found";
                        return;
                    }
                }

                // Check for cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    StatusText = "Operation cancelled";
                    AddToLog("Operation was cancelled before MFC creation");
                    return;
                }

                // Create a nice MFC filename based on the release and sanitize it
                string releaseName = LatestRelease?.Replace("-", "") ?? "Latest";
                string mfcName = $"OvertureRelease_{releaseName}";
                string mfcFilePath = Path.Combine(connectionFolder, $"{mfcName}.mfc");

                AddToLog($"MFC源文件夹: {dataFolder}");
                AddToLog($"MFC输出位置: {connectionFolder}");
                AddToLog($"MFC名称: {mfcName}");

                ProgressValue = 30; // Show progress starting

                try
                {
                    // Get the add-in's execution path to help locate bundled DuckDB extensions
                    string addinExecutionPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                    bool success = await Services.MfcUtility.GenerateMfcFileAsync(
                        dataFolder,         // Source folder with the properly structured datasets
                        mfcFilePath,        // Full path to the output MFC file
                        addinExecutionPath, // Path to the add-in's executing directory
                        (logMessage) => AddToLog(logMessage) // Pass the AddToLog method for logging within the utility
                    );

                    // Check for cancellation
                    if (cancellationToken.IsCancellationRequested)
                    {
                        StatusText = "Operation cancelled";
                        AddToLog("Operation was cancelled during MFC creation");
                        return;
                    }

                    ProgressValue = 100; // Complete

                    if (success)
                    {
                        StatusText = "Successfully created Multifile Feature Connection";
                        AddToLog($"MFC创建于: {mfcFilePath}");

                        // Provide simplified instructions for adding the MFC to the project
                        try
                        {
                            AddToLog("----------------");
                            AddToLog("在项目中使用MFC:");
                            AddToLog("1. 在目录窗格中，导航到MFC文件的位置");
                            AddToLog($"2. 右键单击文件: {Path.GetFileName(mfcFilePath)}");
                            AddToLog("3. 选择 '添加到项目'");
                            AddToLog("4. MFC将出现在 '多文件要素连接' 部分");
                            AddToLog("----------------");

                            // Display a message box with instructions
                            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                                $"多文件要素连接创建成功！\n\n" +
                                $"位置: {mfcFilePath}\n\n" +
                                "要将其添加到项目中：\n" +
                                "1. 在目录窗格中导航到MFC文件\n" +
                                $"2. 右键单击 '{Path.GetFileName(mfcFilePath)}'\n" +
                                "3. 选择 '添加到项目'",
                                "MFC创建成功",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            AddToLog($"Warning: {ex.Message}");
                        }
                    }
                    else
                    {
                        StatusText = "Error creating Multifile Feature Connection";
                        AddToLog("创建MFC失败。请查看ArcGIS Pro日志了解详细信息。");

                        // Show a message box with more details to help the user
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                            "无法创建多文件要素连接。\n\n" +
                            "可能的原因：\n" +
                            "1. 在预期的文件夹结构中未找到数据文件\n" +
                            "2. GeoParquet文件结构不正确\n" +
                            "3. ArcGIS Pro没有创建MFC文件的权限\n\n" +
                            "请查看日志选项卡了解更多详细信息。",
                            "MFC创建失败",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    StatusText = "Error creating Multifile Feature Connection";
                    AddToLog($"ERROR: Exception creating MFC: {ex.Message}");
                    AddToLog($"Stack trace: {ex.StackTrace}");
                    ProgressValue = 0;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error creating MFC: {ex.Message}";
                AddToLog($"ERROR: {ex.Message}");
                AddToLog($"Stack trace: {ex.StackTrace}");
                ProgressValue = 0;
                System.Diagnostics.Debug.WriteLine($"MFC creation error: {ex}");
            }
            finally
            {
                // Clean up the cancellation token source
                if (_cts != null)
                {
                    _cts.Dispose();
                    _cts = null;
                }
            }
        }

        private class ReleaseInfo
        {
            public string Latest { get; set; }
            public List<string> Releases { get; set; }
        }

        /// <summary>
        /// Show the DockPane.
        /// </summary>
        internal static void Show()
        {
            var pane = FrameworkApplication.DockPaneManager.Find(_dockPaneID);
            if (pane == null)
                return;

            // Reset the state when showing the dockpane
            if (pane is WizardDockpaneViewModel viewModel)
            {
                viewModel.ResetState();
            }

            pane.Activate();
        }

        public bool IsThemeSelected(string theme)
        {
            var themeItem = Themes.FirstOrDefault(t => t.DisplayName == theme);
            return themeItem != null && themeItem.IsSelected == true; // Corrected: bool? to bool comparison
        }

        public void ToggleThemeSelection(string theme)
        {
            var themeItem = Themes.FirstOrDefault(t => t.DisplayName == theme);
            if (themeItem != null)
            {
                themeItem.IsSelected = !themeItem.IsSelected;
                // The OnThemeSelectionChanged event handler will update SelectedThemes
            }
        }

        // Add a method to check the selected status in the ViewModel
        private void CheckInitialThemeSelection()
        {
            // Update the preview based on the first selected theme (if any)
            if (Themes.Any())
            {
                SelectedTheme = Themes[0].DisplayName;
            }
        }

        // Cleanup method that will be called when the add-in is unloaded
        // No override needed - this will be called by the framework
        private void CleanupResources()
        {
            // Cleanup by unsubscribing from static events
            System.Diagnostics.Debug.WriteLine("WizardDockpaneViewModel cleaning up, unsubscribing from static events");
            CustomExtentTool.ExtentCreatedStatic -= OnExtentCreated;

            // Dispose of the cancellation token source if it exists
            if (_cts != null)
            {
                _cts.Dispose();
                _cts = null;
            }
        }

        ~WizardDockpaneViewModel()
        {
            CleanupResources();
        }

        // This event handler is for the original flat list of themes. 
        // It's superseded by OnLeafThemeSelectionChanged for hierarchical themes.
        // Consider removing or refactoring if only hierarchical selection is used.
        private void OnThemeSelectionChanged(object sender, EventArgs e)
        {
            // Update the SelectedThemes list based on the currently selected theme items
            // _selectedThemes.Clear(); // No longer used
            // foreach (var themeItem in Themes)
            // {
            //     if (themeItem.IsSelected)
            //     {
            //         _selectedThemes.Add(themeItem.DisplayName); // No longer used
            //     }
            // }

            // NotifyPropertyChanged(nameof(SelectedThemes)); // No longer used
            UpdateThemePreview();
            (LoadDataCommand as RelayCommand)?.RaiseCanExecuteChanged();

            // If a theme was selected, set it as the current preview theme
            if (sender is SelectableThemeItem selectedItem && selectedItem.IsSelected == true) // Corrected: bool? to bool comparison
            {
                SelectedTheme = selectedItem.DisplayName; // This might still be useful for a general preview
                                                          // but SelectedItemForPreview is now primary for TreeView focus
            }
            // else if (_selectedThemes.Count > 0) // No longer used
            // {
            //     // If we just deselected an item but others are still selected, show the first selected theme
            //     SelectedTheme = _selectedThemes[0]; // No longer used
            // }
            else if (GetSelectedLeafItems().Count > 0) // CA1860 .Any() to .Count > 0 // If deselected, but other leaves are selected
            {
                SelectedTheme = GetSelectedLeafItems().First().ParentThemeForS3; // Or another suitable property
            }
            else
            {
                // If no themes are selected, clear the selection
                SelectedTheme = null;
            }
        }

        /// <summary>
        /// 重试机制辅助方法，用于网络操作
        /// </summary>
        private async Task<T> RetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3, int delayMs = 1000)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (i < maxRetries - 1)
                {
                    AddToLog($"操作失败，{delayMs}毫秒后重试 (尝试 {i + 1}/{maxRetries}): {ex.Message}");
                    await Task.Delay(delayMs);
                    delayMs *= 2; // 指数退避
                }
            }
            // 最后一次尝试，不捕获异常
            return await operation();
        }

        /// <summary>
        /// 估算处理时间
        /// </summary>
        private string EstimateProcessingTime(int itemCount)
        {
            // 基于经验的时间估算：每个数据类型大约需要1-3分钟
            int estimatedMinutes = Math.Max(1, itemCount * 2);
            if (estimatedMinutes < 60)
            {
                return $"{estimatedMinutes}";
            }
            else
            {
                int hours = estimatedMinutes / 60;
                int minutes = estimatedMinutes % 60;
                return minutes > 0 ? $"{hours}小时{minutes}" : $"{hours}小时";
            }
        }

        private void ResetState()
        {
            // Clear theme selections
            foreach (var themeItem in Themes)
            {
                // Temporarily unsubscribe to avoid multiple event triggers
                if (themeItem.IsSelectable) themeItem.SelectionChanged -= OnLeafThemeSelectionChanged; // Only if it's a leaf
                else foreach (var subItem in themeItem.SubItems) subItem.SelectionChanged -= OnLeafThemeSelectionChanged;

                themeItem.IsSelected = false;
                foreach (var subItem in themeItem.SubItems) subItem.IsSelected = false;

                if (themeItem.IsSelectable) themeItem.SelectionChanged += OnLeafThemeSelectionChanged; // Only if it's a leaf
                else foreach (var subItem in themeItem.SubItems) subItem.SelectionChanged += OnLeafThemeSelectionChanged;
            }

            // Clear selected themes list - no longer needed
            // _selectedThemes.Clear();
            // NotifyPropertyChanged(nameof(SelectedThemes));

            // Reset other properties
            SelectedTheme = null;
            SelectedTabIndex = 0; // Switch back to the first tab
            _isSelectAllChecked = false; // Explicitly reset, though UpdateIsSelectAllCheckedStatus will also do it.
            NotifyPropertyChanged(nameof(IsSelectAllChecked));

            // Reset extent options
            UseCurrentMapExtent = true;
            UseCustomExtent = false;
            CustomExtent = null;

            // Reset data and MFC options
            var defaultBasePath = DeterminedDefaultMfcBasePath;

            // Reset data options
            DataOutputPath = Path.Combine(
                defaultBasePath,
                "Data",
                LatestRelease ?? "latest"
            );

            // Reset MFC options
            IsSharedMfc = true;
            MfcOutputPath = Path.Combine(
                defaultBasePath,
                "Connections"
            );

            // Reset data source options for MFC
            UsePreviouslyLoadedData = true;
            UseCustomDataFolder = false;
            CustomDataFolderPath = null;
            _lastLoadedDataPath = null;

            // Reset progress and status
            ProgressValue = 0;
            StatusText = "准备加载 Overture Maps 数据";

            // Clear log but keep initialization messages
            LogOutput = new();
            LogOutput.AppendLine("初始化完成。准备进行新查询。");
            LogOutputText = LogOutput.ToString();
            NotifyPropertyChanged(nameof(LogOutputText));

            // Raise can execute changed on commands
            (LoadDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ShowThemeInfoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SetCustomExtentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SelectAllCommand as RelayCommand)?.RaiseCanExecuteChanged();

            UpdateIsSelectAllCheckedStatus(); // Ensure Select All checkbox is correctly updated
            System.Diagnostics.Debug.WriteLine("Add-in state has been reset");
        }

        private void ShowCreateMfcTab()
        {
            // Navigate to the Create MFC tab (index 2)
            SelectedTabIndex = 2;
            StatusText = "准备创建多文件要素连接";
            AddToLog("创建MFC选项卡已激活");
        }

        private static string MakeFriendlyName(string s3TypeName) // CA1822 Made static
        {
            if (string.IsNullOrEmpty(s3TypeName)) return s3TypeName;
            // Replace underscores with spaces and capitalize words
            var parts = s3TypeName.Split(['_'], StringSplitOptions.RemoveEmptyEntries); // IDE0300 / CA1861 Simplified array
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + (parts[i].Length > 1 ? parts[i][1..] : ""); // IDE0057 Substring simplified
            }
            return string.Join(" ", parts);
        }

        private void InitializeThemes()
        {
            var themesCollection = new ObservableCollection<SelectableThemeItem>();
            foreach (var kvp in _overtureS3ThemeTypes)
            {
                string s3ParentThemeKey = kvp.Key; // e.g., "base", "buildings"
                string s3SubTypesString = kvp.Value;
                string[] s3SubTypes = s3SubTypesString.Split(',');

                string parentDisplayName = _parentThemeDisplayNames.TryGetValue(s3ParentThemeKey, out var dn) ? dn : MakeFriendlyName(s3ParentThemeKey);

                // Parent item: DisplayName, ActualType (itself, for grouping), ParentS3Theme (itself)
                // A parent is a leaf (and thus selectable) if it has no distinct sub-types.
                bool parentIsLeaf = s3SubTypes.Length == 1 && s3SubTypes[0] == s3ParentThemeKey;
                // or s3SubTypes.Length == 0 (though current data always has types)

                var parentItem = new SelectableThemeItem(parentDisplayName, s3ParentThemeKey, s3ParentThemeKey, parentIsLeaf);

                if (!parentIsLeaf && s3SubTypes.Length > 0)
                {
                    foreach (var s3SubType in s3SubTypes)
                    {
                        string subTypeTrimmed = s3SubType.Trim();
                        string subItemDisplayName = MakeFriendlyName(subTypeTrimmed);
                        // Sub-item: DisplayName, ActualType=s3SubType, ParentS3Theme=s3ParentThemeKey. Sub-items are always leaves.
                        var subItem = new SelectableThemeItem(subItemDisplayName, subTypeTrimmed, s3ParentThemeKey, true);
                        subItem.Parent = parentItem; // Set the parent property for the sub-item
                        subItem.SelectionChanged += OnLeafThemeSelectionChanged; // ViewModel listens to leaves
                        parentItem.SubItems.Add(subItem);
                    }
                }
                else // Parent is a leaf node
                {
                    // Ensure its ActualType is correctly set if it was determined to be a leaf
                    if (s3SubTypes.Any()) parentItem.ActualType = s3SubTypes[0].Trim();
                    parentItem.SelectionChanged += OnLeafThemeSelectionChanged; // ViewModel listens to leaves
                }
                themesCollection.Add(parentItem);
            }
            Themes = themesCollection; // Assign to the public property
            NotifyPropertyChanged(nameof(Themes));
            UpdateIsSelectAllCheckedStatus(); // Set initial state of SelectAll checkbox
            (SelectAllCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Update command state
        }

        private List<SelectableThemeItem> GetSelectedLeafItems()
        {
            var selectedLeaves = new List<SelectableThemeItem>();
            if (Themes == null) return selectedLeaves;

            Action<SelectableThemeItem> collectSelectedLeaves = null;
            collectSelectedLeaves = (item) =>
            {
                if (item.IsSelectable && item.IsSelected == true) // Only add if explicitly true
                {
                    selectedLeaves.Add(item);
                }
                foreach (var subItem in item.SubItems)
                {
                    collectSelectedLeaves(subItem); // Recurse for sub-items (though current structure is one level deep)
                }
            };

            foreach (var parentItem in Themes)
            {
                // If the parent item itself is selectable (a leaf parent like "Places")
                if (parentItem.IsSelectable && parentItem.IsSelected == true)
                {
                    selectedLeaves.Add(parentItem);
                }
                // Otherwise, or in addition if it has sub-items (which it shouldn't if it's IsSelectable=true as a leaf)
                // Check its sub-items (which are always leaves and IsSelectable=true)
                else if (parentItem.SubItems.Count > 0)
                {
                    foreach (var subItem in parentItem.SubItems)
                    {
                        if (subItem.IsSelected == true) // SubItems are leaves
                        {
                            selectedLeaves.Add(subItem);
                        }
                    }
                }
            }
            return selectedLeaves.Distinct().ToList(); // Ensure distinct items if logic paths overlap
        }

        // This method is now the primary handler for selection changes on leaf items
        private void OnLeafThemeSelectionChanged(object sender, EventArgs e)
        {
            if (_isUpdatingSelectionInternally) return; // Skip if a bulk update is in progress

            if (sender is SelectableThemeItem selectedLeafItem)
            {
                // Set this item for preview purposes, even if it's being deselected
                // The preview panel will update based on this item's state and overall selections
                SelectedItemForPreview = selectedLeafItem;
            }
            // Update combined estimates and other UI elements that depend on the full selection set
            UpdateThemePreview(); // This eventually calls UpdateIsSelectAllCheckedStatus
            (LoadDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(SelectedLeafItemCount));
            NotifyPropertyChanged(nameof(AllSelectedLeafItemsForPreview));
            // UpdateIsSelectAllCheckedStatus(); // Explicitly call to ensure status is current
        }

        private void ExecuteSelectAllInternal(bool select)
        {
            if (Themes == null) return;

            _isUpdatingSelectionInternally = true;
            try
            {
                foreach (var themeItem in Themes)
                {
                    if (themeItem.IsSelectable) // Parent is a leaf
                    {
                        themeItem.IsSelected = select;
                    }
                    else if (themeItem.SubItems.Any()) // Parent has sub-items, set its state (will propagate)
                    {
                        themeItem.IsSelected = select; // This will trigger propagation to children
                    }
                    // No need to iterate sub-items here anymore, parent IsSelected setter handles it.

                    // Expand/Collapse parent themes based on 'select' state
                    if (themeItem.IsExpandable)
                    {
                        themeItem.IsExpanded = select; // Set to true if select is true, false if select is false
                    }
                }
            }
            finally
            {
                _isUpdatingSelectionInternally = false;
            }

            // After bulk update, the individual OnLeafThemeSelectionChanged handlers were skipped.
            // We need to manually trigger updates for dependent properties and the overall "Select All" state.
            UpdateThemePreview(); // Refreshes previews, and calls UpdateIsSelectAllCheckedStatus
            (LoadDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            NotifyPropertyChanged(nameof(SelectedLeafItemCount));
            NotifyPropertyChanged(nameof(AllSelectedLeafItemsForPreview));
            // UpdateIsSelectAllCheckedStatus(); // Called by UpdateThemePreview indirectly, but call directly for safety
        }

        private void UpdateIsSelectAllCheckedStatus()
        {
            if (Themes == null || !Themes.Any())
            {
                // Use SetProperty to ensure UI is notified if it changes.
                SetProperty(ref _isSelectAllChecked, false, nameof(IsSelectAllChecked));
                return;
            }

            bool allDataTypesSelected = true;
            bool anySelectableLeafExists = false;

            // We need to check all actual data types (leaf nodes)
            List<SelectableThemeItem> allLeafItems = GetAllLeafDataItems();

            if (!allLeafItems.Any())
            {
                SetProperty(ref _isSelectAllChecked, false, nameof(IsSelectAllChecked));
                return;
            }

            foreach (var leafItem in allLeafItems)
            {
                anySelectableLeafExists = true; // We know it exists if allLeafItems is not empty
                if (leafItem.IsSelected != true) // Check for explicitly true
                {
                    allDataTypesSelected = false;
                    break;
                }
            }

            SetProperty(ref _isSelectAllChecked, anySelectableLeafExists && allDataTypesSelected, nameof(IsSelectAllChecked));
        }

        // Helper to get all actual data type items (leaves)
        private List<SelectableThemeItem> GetAllLeafDataItems()
        {
            var leafItems = new List<SelectableThemeItem>();
            if (Themes == null) return leafItems;

            foreach (var themeItem in Themes)
            {
                if (themeItem.IsSelectable) // It's a leaf parent (e.g., Places)
                {
                    leafItems.Add(themeItem);
                }
                // Add all sub-items, as they are always leaves/actual data types
                leafItems.AddRange(themeItem.SubItems);
            }
            return leafItems.Distinct().ToList(); // Ensure distinct if structure could somehow allow duplicates
        }
    }
}

