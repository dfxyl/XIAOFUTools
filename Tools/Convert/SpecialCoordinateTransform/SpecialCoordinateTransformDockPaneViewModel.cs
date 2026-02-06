using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Catalog;
using Microsoft.Win32;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.SpecialCoordinateTransform
{
    /// <summary>
    /// 转换类型信息类
    /// </summary>
    public class ConversionTypeInfo
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        
        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// 特殊坐标转换DockPane视图模型
    /// </summary>
    internal class SpecialCoordinateTransformDockPaneViewModel : PropertyChangedBase
    {
        #region 属性

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isProcessing = false;
        private double _progress = 0;
        private bool _isProgressIndeterminate = false;
        private string _statusMessage = "准备就绪";
        private string _logContent = "";
        // 持有输出数据存储（避免在使用FeatureClass期间被提前释放）
        private Geodatabase _outputGeodatabase;
        private FileSystemDatastore _outputFileDatastore;

        // 输入图层
        private ObservableCollection<Layer> _featureLayers = new ObservableCollection<Layer>();
        private Layer _selectedInputLayer;

        // 输出路径
        private string _outputPath = "";

        // 转换类型
        private ObservableCollection<ConversionTypeInfo> _conversionTypes = new ObservableCollection<ConversionTypeInfo>();
        private ConversionTypeInfo _selectedConversionType;

        public ObservableCollection<Layer> FeatureLayers
        {
            get => _featureLayers;
            set => SetProperty(ref _featureLayers, value);
        }

        public Layer SelectedInputLayer
        {
            get => _selectedInputLayer;
            set
            {
                SetProperty(ref _selectedInputLayer, value);
                NotifyPropertyChanged(nameof(CanProcess));
                // 当选择图层改变时，自动生成输出路径
                UpdateOutputPath();
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set
            {
                SetProperty(ref _outputPath, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        public ObservableCollection<ConversionTypeInfo> ConversionTypes
        {
            get => _conversionTypes;
            set => SetProperty(ref _conversionTypes, value);
        }

        public ConversionTypeInfo SelectedConversionType
        {
            get => _selectedConversionType;
            set
            {
                SetProperty(ref _selectedConversionType, value);
                NotifyPropertyChanged(nameof(CanProcess));
                // 当选择转换类型改变时，自动生成输出路径
                UpdateOutputPath();
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        public bool CanProcess => !IsProcessing && SelectedInputLayer != null && 
                                 !string.IsNullOrEmpty(OutputPath) && SelectedConversionType != null;

        #endregion

        #region 命令

        public ICommand BrowseOutputPathCommand { get; }
        public ICommand RunCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand RefreshLayersCommand { get; }

        #endregion

        #region 构造函数

        public SpecialCoordinateTransformDockPaneViewModel()
        {
            // 初始化集合
            FeatureLayers = new ObservableCollection<Layer>();
            ConversionTypes = new ObservableCollection<ConversionTypeInfo>();

            // 初始化命令
            BrowseOutputPathCommand = new RelayCommand(BrowseOutputPath);
            RunCommand = new RelayCommand(async () => await RunTransformAsync(), () => CanProcess);
            CancelCommand = new RelayCommand(CancelTransform, () => IsProcessing);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            RefreshLayersCommand = new RelayCommand(RefreshLayers);

            // 初始化转换类型
            InitializeConversionTypes();

            // 加载图层
            LoadFeatureLayers();

            // 监听地图变化
            // MapViewInitializedEvent.Subscribe(OnMapViewInitialized);
            // ActiveMapViewChangedEvent.Subscribe(OnActiveMapViewChanged);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            // 精简日志：不输出测试性刷新日志
            LoadFeatureLayers();
        }

        // 移除测试绑定方法，避免产生测试日志

        #endregion

        #region 私有方法

        private void InitializeConversionTypes()
        {
            ConversionTypes.Clear();
            foreach (var type in CoordinateConversionTypes.Types)
            {
                ConversionTypes.Add(new ConversionTypeInfo
                {
                    Key = type.Key,
                    DisplayName = type.Value
                });
            }
            
            if (ConversionTypes.Count > 0)
                SelectedConversionType = ConversionTypes[0];
        }

        private async void LoadFeatureLayers()
        {
            try
            {
                var layers = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null) return new List<FeatureLayer>();
                    return map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .ToList();
                });

                FeatureLayers.Clear();

                if (layers.Count == 0)
                {
                    // 保留必要提示
                    AddLog("当前没有活动地图");
                    return;
                }

                foreach (var layer in layers)
                {
                    FeatureLayers.Add(layer);
                }

                // 如果有图层，默认选择第一个
                if (FeatureLayers.Count > 0 && SelectedInputLayer == null)
                {
                    SelectedInputLayer = FeatureLayers[0];
                    // 选择第一个图层后自动生成输出路径
                    UpdateOutputPath();
                }

                // 通知UI更新
                NotifyPropertyChanged(nameof(FeatureLayers));
                NotifyPropertyChanged(nameof(CanProcess));
            }
            catch (Exception ex)
            {
                AddLog($"加载图层时出错: {ex.Message}");
            }
        }

        private void OnMapViewInitialized(object args)
        {
            LoadFeatureLayers();
        }

        private void OnActiveMapViewChanged(object args)
        {
            LoadFeatureLayers();
        }

        private void UpdateOutputPath()
        {
            if (SelectedInputLayer != null && SelectedConversionType != null)
            {
                try
                {
                    // 获取当前地图的默认地理数据库路径
                    var map = MapView.Active?.Map;
                    if (map != null)
                    {
                        var defaultGdb = Project.Current?.DefaultGeodatabasePath;
                        if (!string.IsNullOrEmpty(defaultGdb))
                        {
                            // 生成转换类型的简短名称
                            var conversionShortName = GetConversionShortName(SelectedConversionType.Key);

                            // 生成输出要素类名称：要素名称_转换坐标名称
                            var outputName = $"{SelectedInputLayer.Name}_{conversionShortName}";

                            // 构建完整路径
                            OutputPath = Path.Combine(defaultGdb, outputName);

                            AddLog($"自动生成输出路径: {OutputPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AddLog($"生成输出路径时出错: {ex.Message}");
                }
            }
        }

        private string GetConversionShortName(string conversionType)
        {
            return conversionType switch
            {
                "wgs84_to_gcj02" => "WGS84ToGCJ02",
                "gcj02_to_wgs84" => "GCJ02ToWGS84",
                "gcj02_to_bd09" => "GCJ02ToBD09",
                "bd09_to_gcj02" => "BD09ToGCJ02",
                "wgs84_to_bd09" => "WGS84ToBD09",
                "bd09_to_wgs84" => "BD09ToWGS84",
                _ => "Converted"
            };
        }

        private void BrowseOutputPath()
        {
            try
            {
                // 使用ArcGIS Pro的SaveItemDialog选择输出位置
                var saveItemDialog = new SaveItemDialog
                {
                    Title = "选择输出位置",
                    OverwritePrompt = true,
                    DefaultExt = "shp",
                    Filter = ItemFilters.FeatureClasses_All
                };

                // 设置初始位置
                var initialLocation = GetProjectGDBPath();
                if (!string.IsNullOrEmpty(initialLocation))
                {
                    saveItemDialog.InitialLocation = initialLocation;
                }

                // 显示对话框并获取结果
                bool? dialogResult = saveItemDialog.ShowDialog();

                if (dialogResult == true)
                {
                    // 获取选中的路径并规范化（自动识别 GDB/SHP 并补全）
                    var defName = $"{SelectedInputLayer?.Name ?? "Feature"}_{GetConversionShortName(SelectedConversionType?.Key ?? "Converted")}";
                    OutputPath = OutputDatasetUtils.NormalizeOutputPath(saveItemDialog.FilePath, defName);
                    AddLog($"选择输出路径: {OutputPath}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"选择输出位置出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取项目地理数据库路径
        /// </summary>
        private string GetProjectGDBPath()
        {
            try
            {
                return Project.Current?.DefaultGeodatabasePath;
            }
            catch
            {
                return null;
            }
        }

        private async Task RunTransformAsync()
        {
            if (!CanProcess) return;

            IsProcessing = true;
            Progress = 0;
            IsProgressIndeterminate = true;
            StatusMessage = "正在处理...";
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await QueuedTask.Run(async () =>
                {
                    await PerformCoordinateTransform(_cancellationTokenSource.Token);
                });

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusMessage = "转换完成";
                    AddLog("坐标转换完成！");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
                AddLog("操作被用户取消");
            }
            catch (Exception ex)
            {
                StatusMessage = $"转换失败: {ex.Message}";
                AddLog($"错误: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 0;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CancelTransform()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void ShowHelp()
        {
            var helpMessage = @"特殊坐标转换工具使用说明：

1. 选择输入要素图层：从下拉列表中选择需要转换的要素图层
2. 输出要素类：
   - 默认输出到当前项目的默认地理数据库
   - 自动命名格式：要素名称_转换坐标名称
   - 点击浏览按钮选择输出位置：
     • 选择 .gdb 数据库文件夹 → 输出为数据库要素类
     • 选择普通文件夹 → 输出为 Shapefile
3. 选择转换类型：
   - WGS84 → GCJ02：GPS坐标转火星坐标
   - GCJ02 → WGS84：火星坐标转GPS坐标
   - GCJ02 → BD09：火星坐标转百度坐标
   - BD09 → GCJ02：百度坐标转火星坐标
   - WGS84 → BD09：GPS坐标转百度坐标
   - BD09 → WGS84：百度坐标转GPS坐标
4. 点击开始按钮执行转换

注意：
- 支持点、线、面要素的坐标转换
- 默认输出为地理数据库要素类，性能更好
- 转换算法仅适用于中国境内的坐标
- 境外坐标将保持不变
- 输出要素类会自动添加到当前地图";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpMessage, "使用帮助");
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}\n";

            // 确保在UI线程上更新
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LogContent += logMessage;
                });
            }
            else
            {
                LogContent += logMessage;
            }
        }

        private async Task PerformCoordinateTransform(CancellationToken cancellationToken)
        {
            var inputLayer = SelectedInputLayer as FeatureLayer;
            if (inputLayer == null)
            {
                throw new InvalidOperationException("输入图层无效");
            }

            AddLog($"开始转换图层: {inputLayer.Name}");
            AddLog($"转换类型: {SelectedConversionType.DisplayName}");
            AddLog($"输出路径: {OutputPath}");

            // 获取输入要素类
            using var table = inputLayer.GetTable();
            var featureClass = table as FeatureClass;
            if (featureClass == null)
            {
                throw new InvalidOperationException("无法获取要素类");
            }

            // 获取要素类定义
            var featureClassDefinition = featureClass.GetDefinition();
            var shapeType = featureClassDefinition.GetShapeType();
            var spatialReference = featureClassDefinition.GetSpatialReference();

            AddLog($"几何类型: {shapeType}");
            AddLog($"空间参考: {spatialReference.Name}");

            // 创建输出要素类
            var outputFeatureClass = await CreateOutputFeatureClass(OutputPath, featureClassDefinition, cancellationToken);

            try
            {
                // 执行坐标转换
                await TransformFeatures(featureClass, outputFeatureClass, cancellationToken);
                AddLog($"输出要素类: {OutputPath}");
            }
            finally
            {
                outputFeatureClass?.Dispose();
                // 处理完成后释放持有的数据存储
                _outputGeodatabase?.Dispose();
                _outputGeodatabase = null;
                _outputFileDatastore?.Dispose();
                _outputFileDatastore = null;
            }
        }

        private async Task<FeatureClass> CreateOutputFeatureClass(string outputPath, FeatureClassDefinition inputDefinition, CancellationToken cancellationToken)
        {
            try
            {
                // 统一使用通用工具解析与创建输出要素类
                var defaultName = $"{SelectedInputLayer?.Name ?? "Feature"}_{GetConversionShortName(SelectedConversionType?.Key ?? "Converted")}";
                var info = OutputDatasetUtils.ParseOutputPath(outputPath, defaultName);

                // 覆盖输出（若已存在则删除）
                await OutputDatasetUtils.DeleteIfExistsAsync(info);

                // 创建要素类
                var geometryType = inputDefinition.GetShapeType().ToString().ToUpper();
                await OutputDatasetUtils.CreateFeatureClassAsync(info, geometryType, inputDefinition.GetSpatialReference(), false, false, SelectedInputLayer);

                // 先释放上一次的存储
                _outputGeodatabase?.Dispose();
                _outputGeodatabase = null;
                _outputFileDatastore?.Dispose();
                _outputFileDatastore = null;

                // 打开创建的要素类（保持数据存储在字段中，避免被提前释放）
                FeatureClass featureClass;
                if (info.IsGdb)
                {
                    // 地理数据库：连接到 GDB 根，使用相对路径打开（支持要素数据集）
                    var connectionPath = new FileGeodatabaseConnectionPath(new Uri(info.GdbRoot));
                    _outputGeodatabase = new Geodatabase(connectionPath);
                    featureClass = _outputGeodatabase.OpenDataset<FeatureClass>(info.RelativePathInGdb);
                }
                else
                {
                    // Shapefile：文件夹 + 名称
                    var connectionPath = new FileSystemConnectionPath(new Uri(info.OutPathWorkspace), FileSystemDatastoreType.Shapefile);
                    _outputFileDatastore = new FileSystemDatastore(connectionPath);
                    featureClass = _outputFileDatastore.OpenDataset<FeatureClass>(info.OutNameNoExt);
                }

                return featureClass;
            }
            catch (Exception ex)
            {
                throw new Exception($"创建输出要素类时出错: {ex.Message}", ex);
            }
        }

        private async Task TransformFeatures(FeatureClass inputFeatureClass, FeatureClass outputFeatureClass, CancellationToken cancellationToken)
        {
            // 获取要素总数
            var totalCount = inputFeatureClass.GetCount();
            AddLog($"要素总数: {totalCount}");

            IsProgressIndeterminate = false;
            Progress = 0;

            var processedCount = 0;
            // 获取字段信息
            var inputDefinition = inputFeatureClass.GetDefinition();
            var outputDefinition = outputFeatureClass.GetDefinition();
            var fieldMap = CreateFieldMap(inputDefinition, outputDefinition);

            // 逐条处理要素（避免缓存Feature导致游标复用引起的重复几何）
            using var cursor = inputFeatureClass.Search();
            while (cursor.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (cursor.Current is Feature feature)
                {
                    var geometry = feature.GetShape();
                    var transformedGeometry = TransformGeometry(geometry);

                    using var rowBuffer = outputFeatureClass.CreateRowBuffer();
                    rowBuffer[outputDefinition.GetShapeField()] = transformedGeometry;

                    foreach (var mapping in fieldMap)
                    {
                        try
                        {
                            var value = feature[mapping.Key];
                            if (value != null)
                            {
                                rowBuffer[mapping.Value] = value;
                            }
                        }
                        catch (Exception ex)
                        {
                            AddLog($"字段映射警告: {mapping.Key} -> {mapping.Value}: {ex.Message}");
                        }
                    }

                    using var row = outputFeatureClass.CreateRow(rowBuffer);

                    processedCount++;
                    Progress = (double)processedCount / totalCount * 100;
                    if (processedCount % 100 == 0 || processedCount == totalCount)
                    {
                        AddLog($"已处理: {processedCount}/{totalCount} 要素");
                    }
                }
            }

            AddLog("坐标转换完成");
        }

        private Dictionary<string, string> CreateFieldMap(FeatureClassDefinition inputDef, FeatureClassDefinition outputDef)
        {
            var fieldMap = new Dictionary<string, string>();
            var inputFields = inputDef.GetFields().Where(f => f.FieldType != FieldType.Geometry && f.FieldType != FieldType.OID);
            var outputFields = outputDef.GetFields().Where(f => f.FieldType != FieldType.Geometry && f.FieldType != FieldType.OID);

            foreach (var inputField in inputFields)
            {
                var outputField = outputFields.FirstOrDefault(f => f.Name.Equals(inputField.Name, StringComparison.OrdinalIgnoreCase));
                if (outputField != null)
                {
                    fieldMap[inputField.Name] = outputField.Name;
                }
            }

            return fieldMap;
        }

        private async Task ProcessFeatureBatch(List<Feature> features, FeatureClass outputFeatureClass,
            Dictionary<string, string> fieldMap, CancellationToken cancellationToken)
        {
            // 使用RowBuffer直接插入数据，不需要编辑操作
            var outputDefinition = outputFeatureClass.GetDefinition();

            foreach (var feature in features)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var geometry = feature.GetShape();
                var transformedGeometry = TransformGeometry(geometry);

                // 创建RowBuffer
                using var rowBuffer = outputFeatureClass.CreateRowBuffer();

                // 设置几何
                rowBuffer[outputDefinition.GetShapeField()] = transformedGeometry;

                // 设置属性
                foreach (var mapping in fieldMap)
                {
                    try
                    {
                        var value = feature[mapping.Key];
                        if (value != null)
                        {
                            rowBuffer[mapping.Value] = value;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 忽略字段映射错误，继续处理
                        AddLog($"字段映射警告: {mapping.Key} -> {mapping.Value}: {ex.Message}");
                    }
                }

                // 插入行
                using var row = outputFeatureClass.CreateRow(rowBuffer);
            }
        }

        private Geometry TransformGeometry(Geometry geometry)
        {
            if (geometry == null) return null;

            var conversionType = SelectedConversionType.Key;

            switch (geometry.GeometryType)
            {
                case GeometryType.Point:
                    return TransformPoint(geometry as MapPoint, conversionType);
                case GeometryType.Polyline:
                    return TransformPolyline(geometry as Polyline, conversionType);
                case GeometryType.Polygon:
                    return TransformPolygon(geometry as Polygon, conversionType);
                default:
                    return geometry;
            }
        }

        private MapPoint TransformPoint(MapPoint point, string conversionType)
        {
            if (point == null) return null;

            var (newX, newY) = CoordinateTransformCore.TransformCoordinate(point.X, point.Y, conversionType);
            return MapPointBuilderEx.CreateMapPoint(newX, newY, point.SpatialReference);
        }

        private Polyline TransformPolyline(Polyline polyline, string conversionType)
        {
            if (polyline == null) return null;

            var builder = new PolylineBuilderEx(polyline.SpatialReference);

            foreach (var part in polyline.Parts)
            {
                var points = new List<MapPoint>();
                foreach (var segment in part)
                {
                    // 获取线段的起点
                    var startPoint = segment.StartPoint;
                    var (newX, newY) = CoordinateTransformCore.TransformCoordinate(startPoint.X, startPoint.Y, conversionType);
                    points.Add(MapPointBuilderEx.CreateMapPoint(newX, newY, polyline.SpatialReference));
                }
                // 添加最后一个点（终点）
                if (part.Count > 0)
                {
                    var lastSegment = part.Last();
                    var endPoint = lastSegment.EndPoint;
                    var (newX, newY) = CoordinateTransformCore.TransformCoordinate(endPoint.X, endPoint.Y, conversionType);
                    points.Add(MapPointBuilderEx.CreateMapPoint(newX, newY, polyline.SpatialReference));
                }

                if (points.Count > 1)
                {
                    builder.AddPart(points);
                }
            }

            return builder.ToGeometry();
        }

        private Polygon TransformPolygon(Polygon polygon, string conversionType)
        {
            if (polygon == null) return null;

            var builder = new PolygonBuilderEx(polygon.SpatialReference);

            foreach (var part in polygon.Parts)
            {
                var points = new List<MapPoint>();
                foreach (var segment in part)
                {
                    // 获取线段的起点
                    var startPoint = segment.StartPoint;
                    var (newX, newY) = CoordinateTransformCore.TransformCoordinate(startPoint.X, startPoint.Y, conversionType);
                    points.Add(MapPointBuilderEx.CreateMapPoint(newX, newY, polygon.SpatialReference));
                }

                if (points.Count > 2)
                {
                    builder.AddPart(points);
                }
            }

            return builder.ToGeometry();
        }

        // 已移除自动添加到地图的功能

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
    }
}
