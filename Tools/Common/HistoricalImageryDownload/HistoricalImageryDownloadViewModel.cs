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
using XIAOFUTools.Common;
using XIAOFUTools.Tools.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Tools.HistoricalImageryDownload
{
    internal sealed class HistoricalImageryDownloadViewModel : INotifyPropertyChanged
    {
        private readonly HistoricalAreaResolver _areaResolver = new();
        private readonly HistoricalImageryDownloadEngine _downloadEngine;
        private readonly HistoricalImageryProviderFactory _providerFactory;

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

            AreaSourceOptions =
            [
                new AreaSourceOption(HistoricalAreaSourceType.CurrentView, "当前视图"),
                new AreaSourceOption(HistoricalAreaSourceType.CustomExtent, "地图框选"),
                new AreaSourceOption(HistoricalAreaSourceType.FeatureLayer, "面图层范围")
            ];

            _selectedProviderOption = ProviderOptions[0];
            _selectedAreaSourceOption = AreaSourceOptions[0];
            UpdateAreaSummary();

            HistoricalImageryDownloadRectangleTool.ExtentCreated += OnExtentCreated;

            QueryVersionsCommand = new SimpleRelayCommand(async () => await QueryVersionsAsync(), () => CanQuery);
            DownloadCommand = new SimpleRelayCommand(async () => await DownloadAsync(), () => CanDownload);
            CancelCommand = new SimpleRelayCommand(Cancel, () => IsProcessing);
            BrowseOutputFolderCommand = new SimpleRelayCommand(BrowseOutputFolder);
            OpenVersionSelectionCommand = new SimpleRelayCommand(OpenVersionSelection, () => CanConfigureVersions);
            PickSpatialReferenceCommand = new SimpleRelayCommand(PickSpatialReference);
            DrawExtentCommand = new SimpleRelayCommand(async () => await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_HistoricalImageryDownloadRectangleTool"));
            RefreshLayersCommand = new SimpleRelayCommand(async () => await LoadFeatureLayersAsync());
            ShowHelpCommand = new SimpleRelayCommand(ShowHelp);
        }

        public ObservableCollection<ProviderOption> ProviderOptions { get; }

        public ProviderOption? SelectedProviderOption
        {
            get => _selectedProviderOption;
            set
            {
                if (SetProperty(ref _selectedProviderOption, value))
                {
                    ResetVersions();
                    UpdateCommands();
                }
            }
        }

        public ObservableCollection<AreaSourceOption> AreaSourceOptions { get; } 

        public AreaSourceOption? SelectedAreaSourceOption
        {
            get => _selectedAreaSourceOption;
            set
            {
                if (SetProperty(ref _selectedAreaSourceOption, value))
                {
                    UpdateAreaSummary();
                    UpdateCommands();
                    OnPropertyChanged(nameof(IsCustomExtentSource));
                    OnPropertyChanged(nameof(IsFeatureLayerSource));
                }
            }
        }

        public ObservableCollection<int> ZoomLevels { get; }

        public int SelectedZoomLevel
        {
            get => _selectedZoomLevel;
            set
            {
                if (SetProperty(ref _selectedZoomLevel, value))
                {
                    UpdateCommands();
                }
            }
        }

        public ObservableCollection<HistoricalVersionSelectionItem> Versions { get; }

        public HistoricalVersionSelectionItem? SelectedVersionPreview
        {
            get => _selectedVersionPreview;
            set
            {
                if (SetProperty(ref _selectedVersionPreview, value))
                {
                    UpdateSelectedVersionSummary();
                }
            }
        }

        public ObservableCollection<FeatureLayer> FeatureLayers { get; }

        public FeatureLayer? SelectedFeatureLayer
        {
            get => _selectedFeatureLayer;
            set
            {
                if (SetProperty(ref _selectedFeatureLayer, value))
                {
                    UpdateAreaSummary();
                    UpdateCommands();
                }
            }
        }

        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set
            {
                if (SetProperty(ref _outputFolderPath, value))
                {
                    UpdateCommands();
                }
            }
        }

        public string OutputSpatialReferenceDisplay
        {
            get => _outputSpatialReferenceDisplay;
            set => SetProperty(ref _outputSpatialReferenceDisplay, value);
        }

        public string AreaSummary
        {
            get => _areaSummary;
            set => SetProperty(ref _areaSummary, value);
        }

        public string SelectedVersionSummary
        {
            get => _selectedVersionSummary;
            set => SetProperty(ref _selectedVersionSummary, value);
        }

        public bool IsQueryAllVersions
        {
            get => _isQueryAllVersions;
            set
            {
                if (SetProperty(ref _isQueryAllVersions, value))
                {
                    ResetVersions();
                    OnPropertyChanged(nameof(QueryModeDisplayText));
                }
            }
        }

        public string QueryModeDisplayText => IsQueryAllVersions ? "查询全部" : "查询历史";

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    OnPropertyChanged(nameof(CanQuery));
                    OnPropertyChanged(nameof(CanDownload));
                    OnPropertyChanged(nameof(CanConfigureVersions));
                    UpdateCommands();
                }
            }
        }

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsCustomExtentSource => SelectedAreaSourceOption?.AreaSourceType == HistoricalAreaSourceType.CustomExtent;

        public bool IsFeatureLayerSource => SelectedAreaSourceOption?.AreaSourceType == HistoricalAreaSourceType.FeatureLayer;

        public bool CanQuery => !IsProcessing && SelectedProviderOption != null;

        public bool CanDownload =>
            !IsProcessing &&
            SelectedProviderOption != null &&
            Versions.Any(item => item.IsSelected) &&
            !string.IsNullOrWhiteSpace(OutputFolderPath) &&
            (!IsFeatureLayerSource || SelectedFeatureLayer != null) &&
            (!IsCustomExtentSource || _customExtent != null);

        public bool CanConfigureVersions => _hasVersionResults && !IsProcessing;

        public ICommand QueryVersionsCommand { get; }

        public ICommand DownloadCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand BrowseOutputFolderCommand { get; }

        public ICommand OpenVersionSelectionCommand { get; }

        public ICommand PickSpatialReferenceCommand { get; }

        public ICommand DrawExtentCommand { get; }

        public ICommand RefreshLayersCommand { get; }

        public ICommand ShowHelpCommand { get; }

        public async Task InitializeAsync()
        {
            if (string.IsNullOrWhiteSpace(OutputFolderPath))
            {
                var projectPath = ArcGIS.Desktop.Core.Project.Current?.HomeFolderPath
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                OutputFolderPath = projectPath;
            }

            await LoadFeatureLayersAsync();
            InitializeDefaultSpatialReference();
            SelectedZoomLevel = GetDefaultZoomLevel();
        }

        private void InitializeDefaultSpatialReference()
        {
            _selectedOutputSpatialReference = MapView.Active?.Map?.SpatialReference;
            OutputSpatialReferenceDisplay = _selectedOutputSpatialReference?.Name ?? "未设置";
        }

        private int GetDefaultZoomLevel()
        {
            var mapView = MapView.Active;
            if (mapView == null)
            {
                return 18;
            }

            var mapScale = mapView.Camera.Scale;
            var zoomLevel = (int)Math.Round(Math.Log(591657550.5 / mapScale, 2));
            return Math.Clamp(zoomLevel, 1, 23);
        }

        private async Task LoadFeatureLayersAsync()
        {
            await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                var layers = map?.GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .Where(layer => layer.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon)
                    .ToList() ?? [];

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    FeatureLayers.Clear();
                    foreach (var layer in layers)
                    {
                        FeatureLayers.Add(layer);
                    }

                    if (SelectedFeatureLayer == null && FeatureLayers.Count > 0)
                    {
                        SelectedFeatureLayer = FeatureLayers[0];
                    }
                });
            });
        }

        private async Task QueryVersionsAsync()
        {
            if (SelectedProviderOption == null)
            {
                return;
            }

            try
            {
                IsProcessing = true;
                StatusMessage = "正在查询当前位置历史版本...";
                Progress = 0;
                ResetVersions();

                var query = await GetCurrentCenterQueryAsync();
                var provider = _providerFactory.GetProvider(SelectedProviderOption.ProviderType);
                var versions = await provider.QueryVersionsAsync(query);

                ApplyVersions(versions);

                StatusMessage = $"查询完成：{Versions.Count} 个历史版本";
                Progress = 100;
                AppendLog($"[{SelectedProviderOption.DisplayName}] {QueryModeDisplayText}：{Versions.Count} 个版本");
            }
            catch (Exception ex)
            {
                StatusMessage = $"查询失败：{ex.Message}";
                AppendLog($"查询失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task<HistoricalVersionQuery> GetCurrentCenterQueryAsync()
        {
            return await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active ?? throw new InvalidOperationException("当前没有活动地图视图。");
                var center = mapView.Extent.Center;
                var centerWgs84 = GeometryEngine.Instance.Project(center, SpatialReferences.WGS84) as MapPoint
                    ?? throw new InvalidOperationException("无法获取当前地图中心点。");

                return new HistoricalVersionQuery
                {
                    Longitude = centerWgs84.X,
                    Latitude = centerWgs84.Y,
                    ZoomLevel = SelectedZoomLevel,
                    IncludeAllVersions = IsQueryAllVersions
                };
            });
        }

        private async Task DownloadAsync()
        {
            if (SelectedProviderOption == null)
            {
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            try
            {
                IsProcessing = true;
                Progress = 0;
                StatusMessage = "正在准备下载...";

                var area = await _areaResolver.ResolveAsync(
                    new HistoricalAreaContext
                    {
                        SourceType = SelectedAreaSourceOption?.AreaSourceType ?? HistoricalAreaSourceType.CurrentView,
                        PreferSelection = true
                    },
                    _customExtent,
                    SelectedFeatureLayer,
                    _cancellationTokenSource.Token);

                var requests = HistoricalBatchDownloadPlanner.CreateRequests(
                    SelectedProviderOption.ProviderType,
                    area.SourceType,
                    Versions,
                    SelectedZoomLevel,
                    OutputFolderPath);

                if (requests.Count == 0)
                {
                    throw new InvalidOperationException("请至少勾选一个历史版本。");
                }

                var completedCount = 0;
                var partialCount = 0;
                var targetSpatialReferenceText = GetTargetSpatialReferenceText();

                foreach (var request in requests)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    StatusMessage = $"正在下载 {completedCount + 1}/{requests.Count}: {request.Version.DisplayDate}";
                    AppendLog($"开始下载: {request.OutputFilePath}");

                    var executionRequest = new HistoricalDownloadExecutionRequest
                    {
                        Request = request,
                        ResolvedArea = area,
                        TargetSpatialReferenceText = targetSpatialReferenceText
                    };

                    await ReleaseOutputFileLocksAsync(request.OutputFilePath);

                    var result = await _downloadEngine.DownloadAsync(
                        executionRequest,
                        new Progress<double>(value => Progress = ((completedCount + value) / requests.Count) * 100d),
                        _cancellationTokenSource.Token);

                    completedCount++;
                    partialCount += result.HasPartialCoverage ? 1 : 0;

                    await AddOutputToMapAsync(result.OutputFilePath);
                    AppendLog($"[{completedCount}/{requests.Count}] 下载完成: {result.OutputFilePath}");
                    foreach (var message in result.Messages.Take(20))
                    {
                        AppendLog(message);
                    }
                }

                StatusMessage = partialCount > 0
                    ? $"批量下载完成：{completedCount} 个文件，{partialCount} 个存在缺图"
                    : $"批量下载完成：{completedCount} 个文件";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "下载已取消";
                AppendLog("下载已取消");
            }
            catch (Exception ex)
            {
                StatusMessage = $"下载失败：{ex.Message}";
                AppendLog($"下载失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                Progress = 0;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task AddOutputToMapAsync(string outputFilePath)
        {
            await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    return;
                }

                LayerFactory.Instance.CreateLayer(
                    new Uri(outputFilePath),
                    mapView.Map,
                    layerName: Path.GetFileNameWithoutExtension(outputFilePath));
            });
        }

        private async Task ReleaseOutputFileLocksAsync(string outputFilePath)
        {
            await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                var map = mapView?.Map;
                if (map == null)
                {
                    return;
                }

                var lockedLayers = map.GetLayersAsFlattenedList()
                    .Where(layer => HistoricalLayerUriMatcher.IsMatch(outputFilePath, layer.URI))
                    .ToList();

                foreach (var layer in lockedLayers)
                {
                    map.RemoveLayer(layer);
                }

                if (lockedLayers.Count > 0)
                {
                    mapView.Redraw(true);
                }
            });
        }

        private string? GetTargetSpatialReferenceText()
        {
            var sr = _selectedOutputSpatialReference ?? MapView.Active?.Map?.SpatialReference;
            if (sr == null)
            {
                return null;
            }

            if (sr.Wkid > 0)
            {
                return $"EPSG:{sr.Wkid}";
            }

            return sr.Wkt;
        }

        private void BrowseOutputFolder()
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择历史影像输出文件夹",
                ShowNewFolderButton = true,
                SelectedPath = string.IsNullOrWhiteSpace(OutputFolderPath)
                    ? ArcGIS.Desktop.Core.Project.Current?.HomeFolderPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : OutputFolderPath
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                OutputFolderPath = dialog.SelectedPath;
            }
        }

        private void PickSpatialReference()
        {
            var spatialReference = CoordinateSystemSelector.ShowCoordinateSystemDialog();
            if (spatialReference != null)
            {
                _selectedOutputSpatialReference = spatialReference;
                OutputSpatialReferenceDisplay = spatialReference.Name;
            }
        }

        private void OpenVersionSelection()
        {
            if (Versions.Count == 0)
            {
                return;
            }

            var dialog = new HistoricalVersionSelectionDialog(Versions);
            dialog.ShowDialog();
            UpdateSelectedVersionSummary();
            UpdateCommands();
        }

        private void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void ShowHelp()
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                HistoricalImageryHelpTextBuilder.Build(),
                "历史影像下载帮助");
        }

        private void OnExtentCreated(Envelope extent)
        {
            _customExtent = extent;
            UpdateAreaSummary();
            UpdateCommands();
        }

        private void UpdateAreaSummary()
        {
            AreaSummary = SelectedAreaSourceOption?.AreaSourceType switch
            {
                HistoricalAreaSourceType.CurrentView => "使用当前地图可见范围",
                HistoricalAreaSourceType.CustomExtent => _customExtent == null ? "未框选范围" : $"已框选范围: {_customExtent.Width:F2} x {_customExtent.Height:F2}",
                HistoricalAreaSourceType.FeatureLayer => SelectedFeatureLayer == null ? "未选择面图层" : $"使用图层: {SelectedFeatureLayer.Name}",
                _ => "未设置范围"
            };
        }

        private void AppendLog(string message)
        {
            var builder = new StringBuilder(LogContent);
            builder.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(message);
            LogContent = builder.ToString();
        }

        private void ApplyVersions(IEnumerable<HistoricalVersionItem> versions)
        {
            ResetVersions();

            foreach (var version in versions)
            {
                var selection = new HistoricalVersionSelectionItem(version);
                selection.PropertyChanged += OnVersionSelectionChanged;
                Versions.Add(selection);
            }

            SelectedVersionPreview = Versions.FirstOrDefault();
            _hasVersionResults = Versions.Count > 0;
            UpdateSelectedVersionSummary();
            UpdateCommands();
            OnPropertyChanged(nameof(CanConfigureVersions));
        }

        private void ResetVersions()
        {
            foreach (var selection in Versions)
            {
                selection.PropertyChanged -= OnVersionSelectionChanged;
            }

            Versions.Clear();
            SelectedVersionPreview = null;
            _hasVersionResults = false;
            UpdateSelectedVersionSummary();
            UpdateCommands();
            OnPropertyChanged(nameof(CanConfigureVersions));
        }

        private void OnVersionSelectionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(HistoricalVersionSelectionItem.IsSelected))
            {
                return;
            }

            UpdateSelectedVersionSummary();
            UpdateCommands();
        }

        private void UpdateSelectedVersionSummary()
        {
            var selectedCount = Versions.Count(item => item.IsSelected);
            SelectedVersionSummary = HistoricalVersionSelectionSummaryBuilder.Build(Versions.Count, selectedCount);
        }

        private void UpdateCommands()
        {
            OnPropertyChanged(nameof(CanQuery));
            OnPropertyChanged(nameof(CanDownload));
            (QueryVersionsCommand as SimpleRelayCommand)?.RaiseCanExecuteChanged();
            (DownloadCommand as SimpleRelayCommand)?.RaiseCanExecuteChanged();
            (CancelCommand as SimpleRelayCommand)?.RaiseCanExecuteChanged();
            (OpenVersionSelectionCommand as SimpleRelayCommand)?.RaiseCanExecuteChanged();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public sealed record ProviderOption(HistoricalImageryProviderType ProviderType, string DisplayName)
        {
            public override string ToString() => DisplayName;
        }

        public sealed record AreaSourceOption(HistoricalAreaSourceType AreaSourceType, string DisplayName)
        {
            public override string ToString() => DisplayName;
        }
    }
}
