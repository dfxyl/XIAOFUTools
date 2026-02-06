using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks; 
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Text;
using System.Globalization;

namespace XIAOFUTools.Tools.DownloadOnlineImagery
{
    /// <summary>
    /// 下载级别项
    /// </summary>
    public class DownloadLevelItem
    {
        public string Level { get; set; }
        public string Description { get; set; }
        public int Scale { get; set; }

        public override string ToString()
        {
            return $"{Level}级 (1:{Scale:N0})";
        }
    }

    /// <summary>
    /// 下载在线影像视图模型
    /// </summary>
    internal class DownloadOnlineImageryViewModel : INotifyPropertyChanged
    {
        #region 属性

        private ObservableCollection<Layer> _featureLayers;
        public ObservableCollection<Layer> FeatureLayers
        {
            get => _featureLayers;
            set => SetProperty(ref _featureLayers, value);
        }

        private Layer _selectedFeatureLayer;
        public Layer SelectedFeatureLayer
        {
            get => _selectedFeatureLayer;
            set
            {
                SetProperty(ref _selectedFeatureLayer, value);
                NotifyCanExecuteChanged();
            }
        }

        private string _outputFolder = "";
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                SetProperty(ref _outputFolder, value);
                NotifyCanExecuteChanged();
            }
        }

        private ObservableCollection<DownloadLevelItem> _downloadLevels;
        public ObservableCollection<DownloadLevelItem> DownloadLevels
        {
            get => _downloadLevels;
            set => SetProperty(ref _downloadLevels, value);
        }

        private DownloadLevelItem _selectedDownloadLevel;
        public DownloadLevelItem SelectedDownloadLevel
        {
            get => _selectedDownloadLevel;
            set
            {
                SetProperty(ref _selectedDownloadLevel, value);
                NotifyCanExecuteChanged();
            }
        }

        private bool _mergeImages = true;
        public bool MergeImages
        {
            get => _mergeImages;
            set => SetProperty(ref _mergeImages, value);
        }

        private bool _isProcessing = false;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
                NotifyCanExecuteChanged();
            }
        }

        public bool CanProcess => !IsProcessing;

        private int _progress = 0;
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private bool _isProgressIndeterminate = false;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        private string _logMessages = "";
        public string LogMessages
        {
            get => _logMessages;
            set => SetProperty(ref _logMessages, value);
        }

        #endregion

        #region 命令

        private ICommand _browseOutputFolderCommand;
        public ICommand BrowseOutputFolderCommand
        {
            get => _browseOutputFolderCommand ?? (_browseOutputFolderCommand = new RelayCommand(BrowseOutputFolder));
        }

        private ICommand _startDownloadCommand;
        public ICommand StartDownloadCommand
        {
            get => _startDownloadCommand ?? (_startDownloadCommand = new RelayCommand(StartDownload, CanStartDownload));
        }

        private ICommand _showHelpCommand;
        public ICommand ShowHelpCommand
        {
            get => _showHelpCommand ?? (_showHelpCommand = new RelayCommand(ShowHelp));
        }

        private ICommand _stopDownloadCommand;
        public ICommand StopDownloadCommand
        {
            get => _stopDownloadCommand ?? (_stopDownloadCommand = new RelayCommand(StopDownload, CanStopDownload));
        }

        private ICommand _refreshLayersCommand;
        public ICommand RefreshLayersCommand
        {
            get => _refreshLayersCommand ?? (_refreshLayersCommand = new RelayCommand(RefreshLayers));
        }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public DownloadOnlineImageryViewModel()
        {
            try
            {
                AddLogMessage("正在初始化ViewModel...");
                InitializeData();

                // 延迟加载图层，避免在构造函数中执行异步操作
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                    new System.Action(LoadFeatureLayers),
                    System.Windows.Threading.DispatcherPriority.Loaded);

                AddLogMessage("ViewModel初始化完成。");
            }
            catch (Exception ex)
            {
                AddLogMessage($"ViewModel初始化失败: {ex.Message}");
            }
        }

        #region 私有方法

        /// <summary>
        /// 初始化数据
        /// </summary>
        private void InitializeData()
        {
            // 初始化下载级别
            DownloadLevels = new ObservableCollection<DownloadLevelItem>
            {
                new DownloadLevelItem { Level = "9", Description = "9级", Scale = 1155581 },
                new DownloadLevelItem { Level = "10", Description = "10级", Scale = 577791 },
                new DownloadLevelItem { Level = "11", Description = "11级", Scale = 288895 },
                new DownloadLevelItem { Level = "12", Description = "12级", Scale = 144448 },
                new DownloadLevelItem { Level = "13", Description = "13级", Scale = 72224 },
                new DownloadLevelItem { Level = "14", Description = "14级", Scale = 36112 },
                new DownloadLevelItem { Level = "15", Description = "15级", Scale = 18056 },
                new DownloadLevelItem { Level = "16", Description = "16级", Scale = 9028 },
                new DownloadLevelItem { Level = "17", Description = "17级", Scale = 4514 },
                new DownloadLevelItem { Level = "18", Description = "18级", Scale = 2257 },
                new DownloadLevelItem { Level = "19", Description = "19级", Scale = 1128 },
                new DownloadLevelItem { Level = "20", Description = "20级", Scale = 564 },
                new DownloadLevelItem { Level = "21", Description = "21级", Scale = 282 }
            };

            // 设置默认级别为15级
            SelectedDownloadLevel = DownloadLevels.FirstOrDefault(x => x.Level == "15");

            // 设置默认输出文件夹
            try
            {
                var project = Project.Current;
                if (project != null)
                {
                    OutputFolder = Path.Combine(Path.GetDirectoryName(project.Path), "DownloadedImagery");
                }
                else
                {
                    OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DownloadedImagery");
                }
            }
            catch
            {
                OutputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DownloadedImagery");
            }

            AddLogMessage("工具已初始化，请选择要素图层和输出文件夹。");
        }

        /// <summary>
        /// 加载要素图层
        /// </summary>
        private async void LoadFeatureLayers()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map != null)
                    {
                        var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                        
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            FeatureLayers = new ObservableCollection<Layer>(layers);
                            if (FeatureLayers.Count > 0)
                            {
                                SelectedFeatureLayer = FeatureLayers.First();
                                AddLogMessage($"已加载 {FeatureLayers.Count} 个要素图层。");
                            }
                            else
                            {
                                AddLogMessage("当前地图中没有要素图层。");
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                AddLogMessage($"加载要素图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            AddLogMessage("正在刷新图层列表...");
            LoadFeatureLayers();
        }

        /// <summary>
        /// 浏览输出文件夹
        /// </summary>
        private void BrowseOutputFolder()
        {
            try
            {
                AddLogMessage("正在打开文件夹选择对话框...");

                var dialog = new Microsoft.Win32.OpenFolderDialog()
                {
                    Title = "选择影像输出文件夹",
                    Multiselect = false
                };

                if (!string.IsNullOrEmpty(OutputFolder) && Directory.Exists(OutputFolder))
                {
                    dialog.InitialDirectory = OutputFolder;
                }

                if (dialog.ShowDialog() == true)
                {
                    OutputFolder = dialog.FolderName;
                    AddLogMessage($"已选择输出文件夹: {OutputFolder}");
                }
                else
                {
                    AddLogMessage("用户取消了文件夹选择。");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"浏览文件夹时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 是否可以开始下载
        /// </summary>
        private bool CanStartDownload()
        {
            return !IsProcessing &&
                   SelectedFeatureLayer != null &&
                   !string.IsNullOrEmpty(OutputFolder) &&
                   SelectedDownloadLevel != null;
        }

        /// <summary>
        /// 是否可以停止下载
        /// </summary>
        private bool CanStopDownload()
        {
            return IsProcessing;
        }

        /// <summary>
        /// 停止下载
        /// </summary>
        private void StopDownload()
        {
            try
            {
                if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                    AddLogMessage("正在停止下载操作...");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"停止下载时出错: {ex.Message}");
            }
        }

        private System.Threading.CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 开始下载
        /// </summary>
        private async void StartDownload()
        {
            if (!CanStartDownload())
            {
                AddLogMessage("下载条件不满足，请检查参数设置。");
                return;
            }

            IsProcessing = true;
            IsProgressIndeterminate = false;  // 改为确定进度模式
            Progress = 0;

            // 创建取消令牌
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new System.Threading.CancellationTokenSource();

            try
            {
                AddLogMessage("开始下载在线影像...");
                AddLogMessage($"要素图层: {SelectedFeatureLayer.Name}");
                AddLogMessage($"输出文件夹: {OutputFolder}");
                AddLogMessage($"下载级别: {SelectedDownloadLevel}");
                AddLogMessage($"合并影像: {(MergeImages ? "是" : "否")}");

                // 首先清理任何现有的临时图层
                await CleanupTemporaryLayers();

                // 等待一下确保清理完成
                await Task.Delay(500);

                await Task.Run(() => PerformDownload());
            }
            catch (Exception ex)
            {
                AddLogMessage($"下载过程中出错: {ex.Message}");
            }
            finally
            {
                // 确保清理所有临时图层
                try
                {
                    await CleanupTemporaryLayers();
                }
                catch (Exception ex)
                {
                    AddLogMessage($"清理临时图层时出错: {ex.Message}");
                }

                // 清理输出文件夹中的临时文件
                try
                {
                    await CleanupTemporaryFiles();
                }
                catch (Exception ex)
                {
                    AddLogMessage($"清理临时文件时出错: {ex.Message}");
                }

                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 100;
            }
        }

        /// <summary>
        /// 执行下载
        /// </summary>
        private async Task PerformDownload()
        {
            try
            {
                // 步骤1: 准备工作 (5%)
                Progress = 5;
                AddLogMessage("正在准备下载...");

                // 确保输出文件夹存在
                if (!Directory.Exists(OutputFolder))
                {
                    Directory.CreateDirectory(OutputFolder);
                    AddLogMessage($"已创建输出文件夹: {OutputFolder}");
                }

                // 步骤2: 获取要素图层范围 (10%)
                Progress = 10;
                AddLogMessage("正在获取要素图层范围...");

                Envelope extent = null;
                SpatialReference spatialReference = null;

                await QueuedTask.Run(() =>
                {
                    if (SelectedFeatureLayer is FeatureLayer featureLayer)
                    {
                        extent = featureLayer.QueryExtent();
                        spatialReference = featureLayer.GetSpatialReference();
                    }
                });

                if (extent == null)
                {
                    AddLogMessage("无法获取要素图层范围。");
                    return;
                }

                // 步骤3: 计算格网参数 (15%)
                Progress = 15;
                AddLogMessage($"要素范围: X({extent.XMin:F2}, {extent.XMax:F2}), Y({extent.YMin:F2}, {extent.YMax:F2})");
                AddLogMessage($"坐标系: {spatialReference?.Name ?? "未知"}");

                var scale = SelectedDownloadLevel.Scale;
                var gridParams = CalculateGridParameters(extent, scale, spatialReference);

                AddLogMessage($"格网大小: {gridParams.CellWidth:F6} x {gridParams.CellHeight:F6}");
                AddLogMessage($"格网数量: {gridParams.GridCount} 个");

                // 步骤4: 创建格网并导出影像 (20%-95%)
                Progress = 20;

                // 检查是否取消
                if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    AddLogMessage("操作已取消。");
                    return;
                }

                await CreateGridAndExportImages(extent, gridParams, spatialReference);

                // 步骤5: 完成 (100%)
                Progress = 100;
                AddLogMessage("影像下载完成。");
            }
            catch (Exception ex)
            {
                AddLogMessage($"下载过程出错: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            try
            {
                AddLogMessage("正在显示帮助信息...");

                string helpText = @"下载在线影像工具使用说明：

1. 要素图层：选择用于确定下载范围的要素图层
2. 输出文件夹：选择影像保存的文件夹
3. 下载级别：选择影像的缩放级别（9-21级）
   - 级别越高，影像越清晰，文件越大
   - 建议根据实际需要选择合适的级别
4. 合并影像：是否将下载的多个影像合并为一个文件

注意事项：
- 下载时间取决于范围大小和网络速度
- 请确保有足够的磁盘空间
- 下载过程中请勿关闭程序";

                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpText, "帮助信息");
                AddLogMessage("帮助信息已显示。");
            }
            catch (Exception ex)
            {
                AddLogMessage($"显示帮助时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加日志消息
        /// </summary>
        private void AddLogMessage(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string logEntry = $"[{timestamp}] {message}\r\n";

                if (System.Windows.Application.Current?.Dispatcher != null)
                {
                    if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                    {
                        LogMessages += logEntry;
                    }
                    else
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            LogMessages += logEntry;
                        });
                    }
                }
                else
                {
                    LogMessages += logEntry;
                }
            }
            catch (Exception ex)
            {
                // 如果日志记录失败，至少不要让程序崩溃
                System.Diagnostics.Debug.WriteLine($"AddLogMessage failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 通知命令状态可能已改变
        /// </summary>
        private void NotifyCanExecuteChanged()
        {
            ((RelayCommand)StartDownloadCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)StopDownloadCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 格网参数
        /// </summary>
        private class GridParameters
        {
            public double CellWidth { get; set; }
            public double CellHeight { get; set; }
            public int GridCount { get; set; }
        }

        /// <summary>
        /// 计算格网参数
        /// </summary>
        private GridParameters CalculateGridParameters(Envelope extent, int scale, SpatialReference spatialReference)
        {
            // 格网尺寸（英寸）
            double gridWidthInch = 12.42425;
            double gridHeightInch = 7.42425;

            double cellWidth, cellHeight;

            if (spatialReference.IsGeographic)
            {
                // 地理坐标系
                double metersPerDegree = 111000; // 粗略估计
                double widthMeters = InchesToMeters(gridWidthInch) * scale;
                double heightMeters = InchesToMeters(gridHeightInch) * scale;
                cellWidth = widthMeters / metersPerDegree;
                cellHeight = heightMeters / metersPerDegree;
            }
            else
            {
                // 投影坐标系
                cellWidth = InchesToMeters(gridWidthInch) * scale;
                cellHeight = InchesToMeters(gridHeightInch) * scale;
            }

            // 计算格网数量
            int xCount = (int)Math.Ceiling((extent.XMax - extent.XMin) / cellWidth);
            int yCount = (int)Math.Ceiling((extent.YMax - extent.YMin) / cellHeight);
            int gridCount = xCount * yCount;

            return new GridParameters
            {
                CellWidth = cellWidth,
                CellHeight = cellHeight,
                GridCount = gridCount
            };
        }

        /// <summary>
        /// 英寸转米
        /// </summary>
        private double InchesToMeters(double inches)
        {
            return inches * 0.0254; // 1 英寸 = 0.0254 米
        }

        /// <summary>
        /// 创建格网并导出影像
        /// </summary>
        private async Task CreateGridAndExportImages(Envelope extent, GridParameters gridParams, SpatialReference spatialReference)
        {
            AddLogMessage("开始创建格网...");

            // 创建临时格网
            string tempFolder = Path.GetTempPath();
            string gridOutput = Path.Combine(tempFolder, $"temp_grid_{Guid.NewGuid():N}.shp");

            try
            {
                // 计算格网的行列数
                int numRows = (int)Math.Ceiling((extent.YMax - extent.YMin) / gridParams.CellHeight);
                int numColumns = (int)Math.Ceiling((extent.XMax - extent.XMin) / gridParams.CellWidth);

                // 使用ArcGIS Pro的CreateFishnet工具创建格网（不添加到地图）
                var parameters = Geoprocessing.MakeValueArray(
                    gridOutput,                                    // out_feature_class
                    $"{extent.XMin} {extent.YMin}",               // origin_coord
                    $"{extent.XMin} {extent.YMin + gridParams.CellHeight}", // y_axis_coord
                    gridParams.CellWidth,                         // cell_width
                    gridParams.CellHeight,                        // cell_height
                    numRows,                                      // number_rows
                    numColumns,                                   // number_columns
                    $"{extent.XMax} {extent.YMax}",              // corner_coord
                    "NO_LABELS",                                  // labels
                    extent,                                       // template
                    "POLYGON"                                     // geometry_type
                );

                // 设置环境变量，覆盖输出
                var env = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

                // 设置执行标志，不添加输出到地图
                var executeFlags = GPExecuteToolFlags.GPThread | GPExecuteToolFlags.AddToHistory;

                AddLogMessage("正在创建格网...");
                var result = await Geoprocessing.ExecuteToolAsync("management.CreateFishnet", parameters, env, null, null, executeFlags);
                if (result.IsFailed)
                {
                    throw new Exception($"创建格网失败: {string.Join(", ", result.Messages.Select(m => m.Text))}");
                }

                AddLogMessage("格网创建完成，开始导出影像...");

                // 导出影像
                await ExportImagesFromGrid(gridOutput, spatialReference);

                // 如果需要合并影像
                if (MergeImages)
                {
                    await MergeExportedImages();
                }
            }
            finally
            {
                // 清理临时文件
                try
                {
                    if (File.Exists(gridOutput))
                    {
                        AddLogMessage("清理临时格网文件...");
                        var shpFiles = Directory.GetFiles(Path.GetDirectoryName(gridOutput),
                            Path.GetFileNameWithoutExtension(gridOutput) + ".*");

                        foreach (var file in shpFiles)
                        {
                            try
                            {
                                File.Delete(file);
                                AddLogMessage($"已删除格网文件: {Path.GetFileName(file)}");
                            }
                            catch (Exception ex)
                            {
                                AddLogMessage($"删除格网文件 {Path.GetFileName(file)} 失败: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLogMessage($"清理格网文件时出错: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 从格网导出影像
        /// </summary>
        private async Task ExportImagesFromGrid(string gridPath, SpatialReference spatialReference)
        {
            var exportedFiles = new List<string>();

            AddLogMessage("正在读取格网并导出影像...");

            // 保存选中要素图层的原始可见性状态
            bool originalSelectedLayerVisibility = false;
            FeatureLayer gridLayer = null;

            await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                var mapView = MapView.Active;

                if (map == null || mapView == null)
                {
                    throw new Exception("无法获取当前地图视图");
                }

                if (SelectedFeatureLayer != null)
                {
                    originalSelectedLayerVisibility = SelectedFeatureLayer.IsVisible;
                    // 临时隐藏选中的要素图层，避免影响导出效果
                    SelectedFeatureLayer.SetVisibility(false);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddLogMessage($"已临时隐藏要素图层: {SelectedFeatureLayer.Name}");
                    });
                }

                // 添加格网图层到地图（立即隐藏）
                gridLayer = LayerFactory.Instance.CreateLayer(new Uri(gridPath), map) as FeatureLayer;

                try
                {
                    if (gridLayer != null)
                    {
                        // 立即隐藏格网图层，避免在地图上显示
                        gridLayer.SetVisibility(false);

                        // 强制刷新地图以确保图层被隐藏
                        MapView.Active?.Redraw(true);

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddLogMessage($"已创建并隐藏格网图层: {gridLayer.Name}");
                        });
                        var featureClass = gridLayer.GetFeatureClass();
                        int totalCount = (int)featureClass.GetCount();
                        int currentIndex = 0;

                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddLogMessage($"找到 {totalCount} 个格网单元，开始导出影像...");
                            if (totalCount > 50)
                            {
                                AddLogMessage($"警告: 格网数量较多({totalCount}个)，导出可能需要较长时间。");
                            }
                        });

                        using (var cursor = featureClass.Search())
                        {
                            while (cursor.MoveNext())
                            {
                                // 检查是否取消
                                if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        AddLogMessage("用户取消了下载操作。");
                                    });
                                    break;
                                }

                                using (var feature = cursor.Current)
                                {
                                    var geometry = feature["Shape"] as Polygon;
                                    if (geometry != null)
                                    {
                                        var gridExtent = geometry.Extent;

                                        // 添加缓冲区
                                        var bufferedExtent = AddBufferToExtent(gridExtent, 0.05, spatialReference);

                                        // 确保所有临时图层都隐藏
                                        HideAllTemporaryLayers();

                                        // 设置地图范围
                                        mapView.ZoomTo(bufferedExtent);

                                        // 等待地图刷新并确保视图更新
                                        System.Threading.Thread.Sleep(500);

                                        // 再次确保所有临时图层隐藏，然后刷新地图视图
                                        HideAllTemporaryLayers();
                                        mapView.Redraw(true);

                                        // 再等待一下确保地图完全刷新
                                        System.Threading.Thread.Sleep(200);

                                        // 导出影像
                                        string fileName = $"image_{currentIndex:D4}_{gridExtent.XMin:F0}_{gridExtent.YMin:F0}.tif";
                                        string outputPath = Path.Combine(OutputFolder, fileName);

                                        try
                                        {
                                            // 创建TIFF导出格式
                                            var tiffFormat = new TIFFFormat()
                                            {
                                                OutputFileName = outputPath,
                                                Resolution = 100,
                                                Height = 1080,
                                                Width = 1660,
                                                ColorMode = TIFFColorMode.TwentyFourBitTrueColor,
                                                HasWorldFile = true,
                                                ImageCompression = TIFFImageCompression.LZW
                                            };

                                            // 验证输出路径
                                            if (tiffFormat.ValidateOutputFilePath())
                                            {
                                                // 导出地图视图
                                                mapView.Export(tiffFormat);

                                                // 验证文件是否成功创建
                                                if (File.Exists(outputPath))
                                                {
                                                    exportedFiles.Add(outputPath);
                                                }
                                                else
                                                {
                                                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                                    {
                                                        AddLogMessage($"警告: 影像文件 {fileName} 未成功创建");
                                                    });
                                                }
                                            }
                                            else
                                            {
                                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                                {
                                                    AddLogMessage($"错误: 输出路径验证失败 {outputPath}");
                                                });
                                            }
                                        }
                                        catch (Exception exportEx)
                                        {
                                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                            {
                                                AddLogMessage($"导出影像 {fileName} 时出错: {exportEx.Message}");
                                            });
                                        }

                                        currentIndex++;

                                        // 更新进度 (20%-95%之间)
                                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            // 导出进度占总进度的75% (从20%到95%)
                                            int exportProgress = 20 + (int)((double)currentIndex / totalCount * 75);
                                            Progress = exportProgress;

                                            if (currentIndex % 5 == 0 || currentIndex == totalCount)
                                            {
                                                AddLogMessage($"已导出 {currentIndex}/{totalCount} 个影像 ({exportProgress}%)");
                                            }
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        AddLogMessage($"导出影像时出错: {ex.Message}");
                    });
                    throw;
                }
            });

            // 清理工作：移除临时图层并恢复可见性
            await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map != null)
                {
                    // 移除临时格网图层
                    if (gridLayer != null)
                    {
                        map.RemoveLayer(gridLayer);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddLogMessage($"已移除格网图层: {gridLayer.Name}");
                        });
                    }

                    // 恢复选中要素图层的原始可见性状态
                    if (SelectedFeatureLayer != null)
                    {
                        SelectedFeatureLayer.SetVisibility(originalSelectedLayerVisibility);
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            AddLogMessage($"已恢复要素图层可见性: {SelectedFeatureLayer.Name} -> {originalSelectedLayerVisibility}");
                        });
                    }
                }
            });

            // 保存导出的文件列表
            _exportedImageFiles = exportedFiles;
            AddLogMessage($"影像导出完成，共 {exportedFiles.Count} 个文件。");
        }

        private List<string> _exportedImageFiles = new List<string>();

        /// <summary>
        /// 删除与指定文件相关的所有辅助文件
        /// </summary>
        /// <param name="mainFilePath">主文件路径</param>
        private void DeleteRelatedFiles(string mainFilePath)
        {
            try
            {
                string directory = Path.GetDirectoryName(mainFilePath);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(mainFilePath);
                string fileName = Path.GetFileName(mainFilePath);

                // 定义可能的辅助文件扩展名
                string[] auxiliaryExtensions = {
                    ".tfw",           // 世界文件
                    ".tifw",          // TIFF世界文件
                    ".tif.aux.xml",   // ArcGIS辅助文件
                    ".tiff.aux.xml",  // TIFF辅助文件
                    ".aux.xml",       // 通用辅助文件
                    ".ovr",           // 概览文件
                    ".rrd",           // 金字塔文件
                    ".xml",           // 元数据文件
                    ".prj",           // 投影文件
                    ".clr",           // 颜色文件
                    ".tif.xml",       // TIFF元数据
                    ".tiff.xml"       // TIFF元数据
                };

                foreach (string ext in auxiliaryExtensions)
                {
                    string auxFile = Path.Combine(directory, fileNameWithoutExt + ext);
                    if (File.Exists(auxFile))
                    {
                        try
                        {
                            File.Delete(auxFile);
                            AddLogMessage($"已删除辅助文件: {Path.GetFileName(auxFile)}");
                        }
                        catch (Exception ex)
                        {
                            AddLogMessage($"删除辅助文件 {Path.GetFileName(auxFile)} 失败: {ex.Message}");
                        }
                    }
                }

                // 特殊处理：查找所有以主文件名开头的相关文件
                try
                {
                    var relatedFiles = Directory.GetFiles(directory, fileNameWithoutExt + ".*")
                        .Where(f => !f.Equals(mainFilePath, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (string relatedFile in relatedFiles)
                    {
                        try
                        {
                            File.Delete(relatedFile);
                            AddLogMessage($"已删除相关文件: {Path.GetFileName(relatedFile)}");
                        }
                        catch (Exception ex)
                        {
                            AddLogMessage($"删除相关文件 {Path.GetFileName(relatedFile)} 失败: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLogMessage($"查找相关文件时出错: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"删除相关文件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 隐藏所有可见的临时图层
        /// </summary>
        private void HideAllTemporaryLayers()
        {
            try
            {
                var map = MapView.Active?.Map;
                if (map != null)
                {
                    var tempLayers = map.GetLayersAsFlattenedList()
                        .Where(layer =>
                            (layer.Name.Contains("temp_grid") ||
                             layer.Name.Contains("selected_grid") ||
                             layer.Name.Contains("index_grid") ||
                             layer.Name.Contains("fishnet") ||
                             layer.Name.ToLower().Contains("grid")) &&
                            layer.IsVisible)
                        .ToList();

                    foreach (var layer in tempLayers)
                    {
                        layer.SetVisibility(false);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"隐藏临时图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理输出文件夹中的临时文件
        /// </summary>
        private async Task CleanupTemporaryFiles()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(OutputFolder))
                        return;

                    AddLogMessage("正在清理输出文件夹中的临时文件...");

                    // 查找可能的临时文件模式
                    string[] tempPatterns = {
                        "temp_*.*",
                        "*_temp.*",
                        "*.tmp",
                        "*.temp",
                        "*_grid.*",
                        "grid_*.*"
                    };

                    int deletedCount = 0;
                    foreach (string pattern in tempPatterns)
                    {
                        try
                        {
                            var tempFiles = Directory.GetFiles(OutputFolder, pattern);
                            foreach (string tempFile in tempFiles)
                            {
                                try
                                {
                                    File.Delete(tempFile);
                                    AddLogMessage($"已删除临时文件: {Path.GetFileName(tempFile)}");
                                    deletedCount++;
                                }
                                catch (Exception ex)
                                {
                                    AddLogMessage($"删除临时文件 {Path.GetFileName(tempFile)} 失败: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLogMessage($"查找临时文件模式 {pattern} 时出错: {ex.Message}");
                        }
                    }

                    if (deletedCount > 0)
                    {
                        AddLogMessage($"共清理了 {deletedCount} 个临时文件。");
                    }
                    else
                    {
                        AddLogMessage("未发现需要清理的临时文件。");
                    }
                }
                catch (Exception ex)
                {
                    AddLogMessage($"清理临时文件时出错: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 清理所有临时图层
        /// </summary>
        private async Task CleanupTemporaryLayers()
        {
            await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map != null)
                {
                    // 查找并移除所有可能的临时图层
                    var layersToRemove = map.GetLayersAsFlattenedList()
                        .Where(layer =>
                            layer.Name.Contains("temp_grid") ||
                            layer.Name.Contains("selected_grid") ||
                            layer.Name.Contains("index_grid") ||
                            layer.Name.Contains("fishnet") ||
                            layer.Name.ToLower().Contains("grid") &&
                            (layer.Name.Contains("temp") || layer.Name.Contains("临时")))
                        .ToList();

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (layersToRemove.Count > 0)
                        {
                            AddLogMessage($"发现 {layersToRemove.Count} 个临时图层，正在清理...");
                        }
                    });

                    foreach (var layer in layersToRemove)
                    {
                        try
                        {
                            map.RemoveLayer(layer);
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddLogMessage($"已移除临时图层: {layer.Name}");
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                AddLogMessage($"移除临时图层 {layer.Name} 时出错: {ex.Message}");
                            });
                        }
                    }

                    // 强制刷新地图
                    MapView.Active?.Redraw(true);
                }
            });
        }

        /// <summary>
        /// 为范围添加缓冲区
        /// </summary>
        private Envelope AddBufferToExtent(Envelope extent, double bufferFactor, SpatialReference spatialReference)
        {
            double xBuffer = (extent.XMax - extent.XMin) * bufferFactor;
            double yBuffer = (extent.YMax - extent.YMin) * bufferFactor;

            return EnvelopeBuilderEx.CreateEnvelope(
                extent.XMin - xBuffer,
                extent.YMin - yBuffer,
                extent.XMax + xBuffer,
                extent.YMax + yBuffer,
                spatialReference);
        }

        /// <summary>
        /// 合并导出的影像
        /// </summary>
        private async Task MergeExportedImages()
        {
            if (_exportedImageFiles.Count == 0)
            {
                AddLogMessage("没有影像文件需要合并。");
                return;
            }

            // 合并进度 (95%-98%)
            Progress = 95;
            AddLogMessage("开始合并影像...");

            try
            {
                string mergedImagePath = Path.Combine(OutputFolder, "merged_output.tif");

                await QueuedTask.Run(() =>
                {
                    // 使用ArcGIS Pro的MosaicToNewRaster工具合并影像
                    var parameters = Geoprocessing.MakeValueArray(
                        string.Join(";", _exportedImageFiles),
                        OutputFolder,
                        "merged_output.tif",
                        "",
                        "8_BIT_UNSIGNED",
                        "",
                        3,
                        "BLEND",
                        "FIRST"
                    );

                    var result = Geoprocessing.ExecuteToolAsync("management.MosaicToNewRaster", parameters).Result;
                    if (result.IsFailed)
                    {
                        throw new Exception($"合并影像失败: {string.Join(", ", result.Messages.Select(m => m.Text))}");
                    }
                });

                Progress = 96;
                AddLogMessage($"影像合并完成: {mergedImagePath}");

                // 构建金字塔
                Progress = 97;
                await BuildPyramids(mergedImagePath);

                // 删除临时影像文件及其辅助文件
                Progress = 98;
                if (File.Exists(mergedImagePath))
                {
                    AddLogMessage("删除临时影像文件及辅助文件...");
                    foreach (var file in _exportedImageFiles)
                    {
                        try
                        {
                            // 删除主文件
                            if (File.Exists(file))
                            {
                                File.Delete(file);
                                AddLogMessage($"已删除: {Path.GetFileName(file)}");
                            }

                            // 删除相关的辅助文件
                            DeleteRelatedFiles(file);
                        }
                        catch (Exception ex)
                        {
                            AddLogMessage($"删除文件 {file} 失败: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddLogMessage($"合并影像时出错: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 构建金字塔
        /// </summary>
        private async Task BuildPyramids(string imagePath)
        {
            try
            {
                AddLogMessage("正在构建金字塔...");

                await QueuedTask.Run(() =>
                {
                    var parameters = Geoprocessing.MakeValueArray(imagePath);
                    var result = Geoprocessing.ExecuteToolAsync("management.BuildPyramids", parameters).Result;

                    if (result.IsFailed)
                    {
                        AddLogMessage($"构建金字塔警告: {string.Join(", ", result.Messages.Select(m => m.Text))}");
                    }
                });

                AddLogMessage("金字塔构建完成。");
            }
            catch (Exception ex)
            {
                AddLogMessage($"构建金字塔时出错: {ex.Message}");
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void NotifyPropertyChanged(System.Linq.Expressions.Expression<Func<object>> propertyExpression)
        {
            var memberExpression = propertyExpression.Body as System.Linq.Expressions.MemberExpression;
            if (memberExpression == null)
            {
                var unaryExpression = propertyExpression.Body as System.Linq.Expressions.UnaryExpression;
                if (unaryExpression != null)
                {
                    memberExpression = unaryExpression.Operand as System.Linq.Expressions.MemberExpression;
                }
            }

            if (memberExpression != null)
            {
                OnPropertyChanged(memberExpression.Member.Name);
            }
        }

        #endregion
    }

    /// <summary>
    /// RelayCommand实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { System.Windows.Input.CommandManager.RequerySuggested += value; }
            remove { System.Windows.Input.CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object parameter)
        {
            _execute();
        }

        public void RaiseCanExecuteChanged()
        {
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }
}
