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
using XIAOFUTools.Tools.InternetTileDownload.Infrastructure;
using XIAOFUTools.Tools.InternetTileDownload.Services;

namespace XIAOFUTools.Tools.InternetTileDownload
{
    internal sealed class InternetTileDownloadViewModel : INotifyPropertyChanged
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

            ResolveServiceCommand = new SimpleRelayCommand(async () => await ResolveServiceAsync(), () => CanResolve);
            DownloadCommand = new SimpleRelayCommand(async () => await DownloadAsync(), () => CanDownload);
            CancelCommand = new SimpleRelayCommand(Cancel, () => IsProcessing);
            BrowseOutputFolderCommand = new SimpleRelayCommand(BrowseOutputFolder);
            PickSpatialReferenceCommand = new SimpleRelayCommand(PickSpatialReference);
            DrawExtentCommand = new SimpleRelayCommand(async () => await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_InternetTileDownloadRectangleTool"));
            RefreshLayersCommand = new SimpleRelayCommand(async () => await LoadFeatureLayersAsync());
            ShowHelpCommand = new SimpleRelayCommand(ShowHelp);
        }

        public ObservableCollection<LevelOption> LevelOptions { get; }

        public ObservableCollection<FeatureLayer> FeatureLayers { get; }

        public AreaSourceOption[] AreaSourceOptions { get; }

        public string ServiceUrl
        {
            get => _serviceUrl;
            set
            {
                if (SetProperty(ref _serviceUrl, value))
                {
                    UpdateCommands();
                }
            }
        }

        public string ServiceSummary
        {
            get => _serviceSummary;
            set => SetProperty(ref _serviceSummary, value);
        }

        public LevelOption? SelectedLevelOption
        {
            get => _selectedLevelOption;
            set
            {
                if (SetProperty(ref _selectedLevelOption, value))
                {
                    UpdateCommands();
                }
            }
        }

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

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
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

        public bool IsCustomExtentSource => SelectedAreaSourceOption?.AreaSourceType == InternetTileAreaSourceType.CustomExtent;

        public bool IsFeatureLayerSource => SelectedAreaSourceOption?.AreaSourceType == InternetTileAreaSourceType.FeatureLayer;

        public bool CanResolve => !IsProcessing && !string.IsNullOrWhiteSpace(ServiceUrl);

        public bool CanDownload =>
            !IsProcessing &&
            _resolvedServiceDefinition != null &&
            SelectedLevelOption != null &&
            !string.IsNullOrWhiteSpace(OutputFolderPath) &&
            (!IsFeatureLayerSource || SelectedFeatureLayer != null) &&
            (!IsCustomExtentSource || _customExtent != null);

        public ICommand ResolveServiceCommand { get; }

        public ICommand DownloadCommand { get; }

        public ICommand CancelCommand { get; }

        public ICommand BrowseOutputFolderCommand { get; }

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
        }

        private void InitializeDefaultSpatialReference()
        {
            _selectedOutputSpatialReference = MapView.Active?.Map?.SpatialReference;
            OutputSpatialReferenceDisplay = _selectedOutputSpatialReference?.Name ?? "未设置";
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

        private async Task ResolveServiceAsync()
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "正在解析互联网切片服务...";
                Progress = 0;
                ClearResolvedService();

                _resolvedServiceDefinition = await _serviceResolver.ResolveAsync(ServiceUrl.Trim());
                ApplyResolvedService(_resolvedServiceDefinition);

                StatusMessage = "服务解析完成";
                Progress = 100;
                AppendLog($"已识别服务: {ServiceSummary}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"服务解析失败：{ex.Message}";
                AppendLog($"服务解析失败: {ex.Message}");
                ClearResolvedService();
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private async Task DownloadAsync()
        {
            if (_resolvedServiceDefinition == null || SelectedLevelOption == null)
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
                    new InternetTileAreaContext
                    {
                        SourceType = SelectedAreaSourceOption?.AreaSourceType ?? InternetTileAreaSourceType.CurrentView,
                        PreferSelection = true
                    },
                    _customExtent,
                    SelectedFeatureLayer,
                    _cancellationTokenSource.Token);

                var outputFilePath = InternetTileOutputPathBuilder.BuildForFolder(OutputFolderPath, ServiceUrl, SelectedLevelOption.LevelId);
                await ReleaseOutputFileLocksAsync(outputFilePath);

                var executionRequest = new InternetTileDownloadExecutionRequest
                {
                    Request = new InternetTileDownloadRequest
                    {
                        ServiceDefinition = _resolvedServiceDefinition,
                        LevelId = SelectedLevelOption.LevelId,
                        OutputFilePath = outputFilePath,
                        UseCache = true
                    },
                    ResolvedArea = area,
                    TargetSpatialReferenceText = GetTargetSpatialReferenceText()
                };

                var inspection = await _downloadEngine.InspectAsync(executionRequest, _cancellationTokenSource.Token);
                foreach (var message in inspection.Messages)
                {
                    AppendLog(message);
                }

                if (inspection.ShouldBlock)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, inspection.Messages));
                }

                if (inspection.ShouldWarn)
                {
                    var warningText = string.Join(Environment.NewLine, inspection.Messages);
                    var dialogResult = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                        $"{warningText}{Environment.NewLine}{Environment.NewLine}是否继续下载？",
                        "大范围下载提示",
                        System.Windows.MessageBoxButton.OKCancel,
                        System.Windows.MessageBoxImage.Warning);
                    if (dialogResult != System.Windows.MessageBoxResult.OK)
                    {
                        StatusMessage = "下载已取消";
                        AppendLog("用户取消了大范围下载。");
                        return;
                    }
                }

                StatusMessage = $"正在下载级别 {SelectedLevelOption.LevelId}...";
                AppendLog($"开始下载: {outputFilePath}");
                AppendLog($"预计瓦片数: {inspection.TotalTileCount}，自动并发: {inspection.RecommendedTileConcurrency}");

                var result = await _downloadEngine.DownloadAsync(
                    executionRequest,
                    new Progress<double>(value => Progress = value * 100d),
                    _cancellationTokenSource.Token);

                await AddOutputToMapAsync(result.OutputFilePath);
                AppendLog($"下载完成: {result.OutputFilePath}");
                foreach (var message in result.Messages.Take(20))
                {
                    AppendLog(message);
                }

                StatusMessage = result.HasPartialCoverage
                    ? $"下载完成：共 {result.DownloadedTileCount}/{result.TotalTileCount} 个瓦片，存在缺图"
                    : $"下载完成：共 {result.DownloadedTileCount} 个瓦片";
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

        private void ApplyResolvedService(InternetTileServiceDefinition definition)
        {
            LevelOptions.Clear();
            foreach (var level in definition.Levels)
            {
                LevelOptions.Add(new LevelOption(level.LevelId, $"级别 {level.LevelId}"));
            }

            SelectedLevelOption = GetPreferredLevelOption(definition);
            ServiceSummary = InternetTileServiceSummaryBuilder.Build(definition);
            UpdateCommands();
        }

        private LevelOption? GetPreferredLevelOption(InternetTileServiceDefinition definition)
        {
            var defaultZoom = GetDefaultZoomLevel();
            var numericLevel = definition.Levels
                .Select(level => new
                {
                    LevelId = level.LevelId,
                    Parsed = TryExtractNumericLevel(level.LevelId)
                })
                .Where(item => item.Parsed.HasValue)
                .OrderBy(item => Math.Abs(item.Parsed!.Value - defaultZoom))
                .ThenByDescending(item => item.Parsed)
                .Select(item => item.LevelId)
                .FirstOrDefault();

            return LevelOptions.FirstOrDefault(item => item.LevelId == numericLevel)
                ?? LevelOptions.LastOrDefault();
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

        private static int? TryExtractNumericLevel(string levelId)
        {
            if (int.TryParse(levelId, out var direct))
            {
                return direct;
            }

            var digits = new string(levelId.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var parsed) ? parsed : null;
        }

        private void ClearResolvedService()
        {
            _resolvedServiceDefinition = null;
            LevelOptions.Clear();
            SelectedLevelOption = null;
            ServiceSummary = "未解析服务";
            UpdateCommands();
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
                Description = "选择互联网切片下载输出文件夹",
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

        private void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void ShowHelp()
        {
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                InternetTileHelpTextBuilder.Build(),
                "互联网切片下载帮助");
        }

        private async void OnExtentCreated(Envelope extent)
        {
            _customExtent = extent;
            UpdateAreaSummary();
            UpdateCommands();

            try
            {
                await SketchToolResetWorkflow.ResetAsync(ArcGisSketchToolResetOperations.Instance);
            }
            catch (Exception ex)
            {
                AppendLog($"恢复地图工具状态失败: {ex.Message}");
            }
        }

        private void UpdateAreaSummary()
        {
            AreaSummary = SelectedAreaSourceOption?.AreaSourceType switch
            {
                InternetTileAreaSourceType.CurrentView => "使用当前地图可见范围",
                InternetTileAreaSourceType.CustomExtent => _customExtent == null ? "未框选范围" : $"已框选范围: {_customExtent.Width:F2} x {_customExtent.Height:F2}",
                InternetTileAreaSourceType.FeatureLayer => SelectedFeatureLayer == null ? "未选择面图层" : $"使用图层: {SelectedFeatureLayer.Name}",
                _ => "未设置范围"
            };
        }

        private void AppendLog(string message)
        {
            var builder = new StringBuilder(LogContent);
            builder.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(message);
            LogContent = builder.ToString();
        }

        private void UpdateCommands()
        {
            OnPropertyChanged(nameof(CanResolve));
            OnPropertyChanged(nameof(CanDownload));
            (ResolveServiceCommand as SimpleRelayCommand)?.RaiseCanExecuteChanged();
            (DownloadCommand as SimpleRelayCommand)?.RaiseCanExecuteChanged();
            (CancelCommand as SimpleRelayCommand)?.RaiseCanExecuteChanged();
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

        public sealed record LevelOption(string LevelId, string DisplayName)
        {
            public override string ToString() => DisplayName;
        }

        public sealed record AreaSourceOption(InternetTileAreaSourceType AreaSourceType, string DisplayName)
        {
            public override string ToString() => DisplayName;
        }
    }
}
