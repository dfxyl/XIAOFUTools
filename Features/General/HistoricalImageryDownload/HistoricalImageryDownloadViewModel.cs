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

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    internal sealed partial class HistoricalImageryDownloadViewModel : INotifyPropertyChanged
    {
        private readonly HistoricalAreaResolver _areaResolver = new();
        private readonly HistoricalImageryDownloadEngine _downloadEngine;
        private readonly HistoricalImageryProviderFactory _providerFactory;
        private readonly IHistoricalVersionSelectionDialogService _versionSelectionDialogService = new HistoricalVersionSelectionDialogService();

        private CancellationTokenSource? _cancellationTokenSource;
        private Envelope? _customExtent;
        private bool _isProcessing;
        private double _progress;
        private string _logContent = string.Empty;
        private string _statusMessage = "就绪";
        private string _outputFolderPath = string.Empty;
        private string _areaSummary = "使用当前视图";
        private string _selectedVersionSummary = "未选择历史版本";
        private bool _hasVersionResults;
        private bool _isQueryAllVersions;
        private ProviderOption? _selectedProviderOption;
        private GoogleFallbackModeOption? _selectedGoogleFallbackModeOption;
        private AreaSourceOption? _selectedAreaSourceOption;
        private int _selectedZoomLevel = 18;
        private HistoricalVersionSelectionItem? _selectedVersionPreview;
        private FeatureLayer? _selectedFeatureLayer;
        private SpatialReference? _selectedOutputSpatialReference;
        private string _outputSpatialReferenceDisplay = "未设置";

        public HistoricalImageryDownloadViewModel()
        {
            _providerFactory = new HistoricalImageryProviderFactory(
                new GoogleHistoricalImageryProvider(),
                new WaybackHistoricalImageryProvider());
            _downloadEngine = new HistoricalImageryDownloadEngine(_providerFactory);

            ZoomLevels = new ObservableCollection<int>(Enumerable.Range(1, 23));
            Versions = new ObservableCollection<HistoricalVersionSelectionItem>();
            FeatureLayers = new ObservableCollection<FeatureLayer>();

            ProviderOptions =
            [
                new ProviderOption(HistoricalImageryProviderType.GoogleEarth, "Google 历史影像"),
                new ProviderOption(HistoricalImageryProviderType.Wayback, "Esri Wayback")
            ];

            GoogleFallbackModeOptions =
            [
                new GoogleFallbackModeOption(GoogleNearestDateFallbackMode.SeparateOutputs, "分别输出"),
                new GoogleFallbackModeOption(GoogleNearestDateFallbackMode.MixedSingleOutput, "混合日期")
            ];

            AreaSourceOptions =
            [
                new AreaSourceOption(HistoricalAreaSourceType.CurrentView, "当前视图"),
                new AreaSourceOption(HistoricalAreaSourceType.CustomExtent, "地图框选"),
                new AreaSourceOption(HistoricalAreaSourceType.FeatureLayer, "面图层范围")
            ];

            _selectedProviderOption = ProviderOptions[0];
            _selectedGoogleFallbackModeOption = GoogleFallbackModeOptions[0];
            _selectedAreaSourceOption = AreaSourceOptions[0];
            UpdateAreaSummary();

            HistoricalImageryDownloadRectangleTool.ExtentCreated += OnExtentCreated;

            QueryVersionsCommand = new RelayCommand(async () => await QueryVersionsAsync(), () => CanQuery);
            DownloadCommand = new RelayCommand(async () => await DownloadAsync(), () => CanDownload);
            CancelCommand = new RelayCommand(Cancel, () => IsProcessing);
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            OpenVersionSelectionCommand = new RelayCommand(OpenVersionSelection, () => CanConfigureVersions);
            PickSpatialReferenceCommand = new RelayCommand(PickSpatialReference);
            DrawExtentCommand = new RelayCommand(async () => await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_HistoricalImageryDownloadRectangleTool"));
            RefreshLayersCommand = new RelayCommand(async () => await LoadFeatureLayersAsync());
            ShowHelpCommand = new RelayCommand(ShowHelp);
        }
    }
}
