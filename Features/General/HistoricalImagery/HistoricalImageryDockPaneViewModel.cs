using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using XIAOFUTools.Features.General.HistoricalImageryDownload;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Features.General.HistoricalImagery
{
    /// <summary>
    /// 历史影像版本数据模型
    /// </summary>
    public class WaybackVersion : PropertyChangedBase
    {
        private bool _isSelected;

        public int ReleaseNum { get; set; }
        public string ReleaseDate { get; set; }
        public int Year { get; set; }
        public string ItemTitle { get; set; }
        public string Url { get; set; }
        public string MetadataLayerUrl { get; set; }
        public string AcquisitionDate { get; set; }
        public bool IsChanged { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string DisplayName => $"{ReleaseDate}";
    }

    /// <summary>
    /// 当前位置影像元数据
    /// </summary>
    public class ImageryMetadata : PropertyChangedBase
    {
        public string AcquisitionDate { get; set; }
        public string Provider { get; set; }
        public string Satellite { get; set; }
        public string Resolution { get; set; }
        public string Accuracy { get; set; }
        public int ZoomLevel { get; set; }
        public string ReleaseTitle { get; set; }
    }

    /// <summary>
    /// 树形节点数据模型
    /// </summary>
    public class TreeNode : PropertyChangedBase
    {
        private bool _isExpanded;
        private bool _isSelected;

        public string Header { get; set; }
        public ObservableCollection<TreeNode> Children { get; set; }
        public WaybackVersion Version { get; set; }
        public bool IsLeaf => Version != null;

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public TreeNode()
        {
            Children = new ObservableCollection<TreeNode>();
        }
    }

    /// <summary>
    /// 历史影像停靠窗格视图模型
    /// </summary>
    internal partial class HistoricalImageryDockPaneViewModel : PropertyChangedBase
    {
        private static readonly HttpClient _httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        private ObservableCollection<TreeNode> _treeNodes;
        private bool _isLoading;
        private string _statusMessage;
        private TreeNode _selectedNode;
        private string _searchText;
        private ImageryMetadata _currentMetadata;
        private string _metadataDisplay;
        private bool _showChangedOnly;

        private List<WaybackVersion> _allVersions;
        private List<WaybackVersion> _catalogVersions;
        private readonly WaybackHistoricalImageryProvider _waybackProvider = new();
        private readonly HistoricalImageryMapLoadService _mapLoadService;
        private readonly IHistoricalImageryMetadataSourceDialogService _metadataSourceDialogService = new HistoricalImageryMetadataSourceDialogService();
        private dynamic _layersAddedToken;
        private HistoricalImageryLayerRequest _pendingDragRequest;
        private DateTime _pendingDragRequestTimeUtc;

        public HistoricalImageryDockPaneViewModel()
        {
            TreeNodes = new ObservableCollection<TreeNode>();
            _allVersions = new List<WaybackVersion>();
            _catalogVersions = new List<WaybackVersion>();
            _mapLoadService = new HistoricalImageryMapLoadService();
            TreeDragDropHandler = new HistoricalImageryTreeDragDropHandler(this);
            _layersAddedToken = LayersAddedEvent.Subscribe((args) => _ = ApplyPendingDragRequestAsync(args));
            MetadataDisplay = "点击查询按钮获取当前位置影像信息";

            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            AddLayerCommand = new RelayCommand(AddSelectedLayer, () => SelectedNode?.IsLeaf == true);
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
            QueryMetadataCommand = new RelayCommand(async () => await QueryCurrentMetadataAsync());
            ShowHelpCommand = new RelayCommand(ShowHelp);

            // 自动加载版本列表
            _ = LoadVersionsAsync();
        }
    }
}

