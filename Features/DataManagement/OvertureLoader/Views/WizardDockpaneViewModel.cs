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
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Services;
using Microsoft.Win32;
using ArcGIS.Desktop.Catalog;
using System.Text;
using System.IO;
using System.Threading;
using ArcGIS.Desktop.Core; // Added for Project.Current
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Views
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

    internal partial class WizardDockpaneViewModel : DockPane
    {
        private const string _dockPaneID = "XIAOFUTools_OvertureLoaderDockPane";
        private DataProcessor _dataProcessor;
        private const string S3_BASE_PATH = "s3://overturemaps-us-west-2/release";
        private const string ADDIN_DATA_SUBFOLDER = "OvertureProAddinData"; // Define a subfolder name
        private IOvertureReleaseClient _releaseClient;
        private IOvertureDataReplacementService _dataReplacementService;
        private IOvertureMfcDataFolderInspector _mfcDataFolderInspector;

        // Add CancellationTokenSource for cancelling operations
        private CancellationTokenSource _cts;

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
        private string _latestRelease;

        private ObservableCollection<SelectableThemeItem> _themes;

        private string _selectedTheme;

        private int _selectedTabIndex = 0;

        private string _statusText = "Initializing...";

        private double _progressValue;

        private StringBuilder _logOutput;

        private string _logOutputText;

        private bool _useCurrentMapExtent = true;

        private bool _useCustomExtent = false;

        private bool _isLoading = true;

        private Envelope _customExtent;

        private bool _hasCustomExtent;

        private string _customExtentDisplay = "未设置自定义范围";

        private string _themeDescription = "选择一个主题以查看描述";

        private string _estimatedFeatures = "--";

        private string _estimatedSize = "--";

        private string _themeIconText = "GlobeIcon"; // Default icon (globe)

        private bool _createMfc = true;

        private string _mfcOutputPath;

        private bool _isSharedMfc = true;

        private string _dataOutputPath;

        private bool _usePreviouslyLoadedData = true;

        private bool _useCustomDataFolder = false;

        private string _customDataFolderPath;

        // Track the last loaded data path for MFC creation
        private string _lastLoadedDataPath;

        private bool _isSelectAllChecked;

        private bool _isUpdatingSelectionInternally = false;
    }
}

