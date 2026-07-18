#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;
using XIAOFUTools.Features.General.InternetTileDownload.Infrastructure;
using XIAOFUTools.Features.General.InternetTileDownload.Services;

namespace XIAOFUTools.Features.General.InternetTileDownload
{
    internal sealed partial class InternetTileDownloadViewModel : INotifyPropertyChanged
    {
        private readonly InternetTileAreaResolver _areaResolver = new();
        private readonly InternetTileServiceResolver _serviceResolver;
        private readonly InternetTileDownloadEngine _downloadEngine;

        private CancellationTokenSource? _cancellationTokenSource;
        private Envelope? _customExtent;
        private InternetTileServiceDefinition? _resolvedServiceDefinition;
        private string _serviceUrl = string.Empty;
        private string _serviceSummary = "未解析服务";
        private bool _isProcessing;
        private double _progress;
        private string _logContent = string.Empty;
        private string _statusMessage = "就绪";
        private string _outputFolderPath = string.Empty;
        private string _areaSummary = "使用当前视图";
        private string _outputSpatialReferenceDisplay = "未设置";
        private SpatialReference? _selectedOutputSpatialReference;
        private AreaSourceOption? _selectedAreaSourceOption;
        private FeatureLayer? _selectedFeatureLayer;
        private LevelOption? _selectedLevelOption;

        public InternetTileDownloadViewModel()
        {
            var httpClient = new InternetTileHttpClient();
            _serviceResolver = new InternetTileServiceResolver(httpClient);
            _downloadEngine = new InternetTileDownloadEngine(httpClient);

            LevelOptions = new ObservableCollection<LevelOption>();
            FeatureLayers = new ObservableCollection<FeatureLayer>();
            AreaSourceOptions =
            [
                new AreaSourceOption(InternetTileAreaSourceType.CurrentView, "当前视图"),
                new AreaSourceOption(InternetTileAreaSourceType.CustomExtent, "地图框选"),
                new AreaSourceOption(InternetTileAreaSourceType.FeatureLayer, "面图层范围")
            ];

            _selectedAreaSourceOption = AreaSourceOptions[0];
            UpdateAreaSummary();

            InternetTileDownloadRectangleTool.ExtentCreated += OnExtentCreated;

            ResolveServiceCommand = new RelayCommand(async () => await ResolveServiceAsync(), () => CanResolve);
            DownloadCommand = new RelayCommand(async () => await DownloadAsync(), () => CanDownload);
            CancelCommand = new RelayCommand(Cancel, () => IsProcessing);
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            PickSpatialReferenceCommand = new RelayCommand(PickSpatialReference);
            DrawExtentCommand = new RelayCommand(async () => await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_InternetTileDownloadRectangleTool"));
            RefreshLayersCommand = new RelayCommand(async () => await LoadFeatureLayersAsync());
            ShowHelpCommand = new RelayCommand(ShowHelp);
        }
    }
}
