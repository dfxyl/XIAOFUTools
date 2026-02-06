using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.BoundaryPointGenerator
{
    /// <summary>
    /// 生成四至坐标点DockPane视图模型
    /// </summary>
    internal class BoundaryPointGeneratorDockPaneViewModel : PropertyChangedBase
    {
        private dynamic _mapSelectionChangedToken;

        #region 属性

        // 取消操作标志
        private bool _cancelRequested = false;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set
            {
                SetProperty(ref _cancelRequested, value);
            }
        }
        
        // 是否正在处理
        private bool _isProcessing = false;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
            }
        }
        
        // 是否可以处理
        public bool CanProcess => !IsProcessing;

        // 面图层列表
        private ObservableCollection<FeatureLayer> _polygonLayers;
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set
            {
                SetProperty(ref _polygonLayers, value);
            }
        }

        // 选中的面图层
        private FeatureLayer _selectedPolygonLayer;
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                NotifyPropertyChanged(() => HasSelectedLayer);
                // 当选择图层改变时，自动更新输出路径
                UpdateOutputPath();
                // 同步更新选择信息
                UpdateSelectionInfo();
            }
        }

        // 是否有选中图层
        public bool HasSelectedLayer => SelectedPolygonLayer != null;

        // 输出路径
        private string _outputPath;
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                // 使用通用工具规范化输出路径（自动识别 GDB/SHP，并补全缺省名称）
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_SZD" : "四至坐标点SZD";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPath, normalized);
            }
        }

        // 状态信息
        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                SetProperty(ref _statusMessage, value);
            }
        }

        // 日志内容
        private string _logContent;
        public string LogContent
        {
            get => _logContent;
            set
            {
                SetProperty(ref _logContent, value);
            }
        }

        // 日志构建器
        private StringBuilder _logBuilder = new StringBuilder();

        // 进度
        private int _progress;
        public int Progress
        {
            get => _progress;
            set
            {
                SetProperty(ref _progress, value);
            }
        }

        // 是否为不确定进度
        private bool _isProgressIndeterminate;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set
            {
                SetProperty(ref _isProgressIndeterminate, value);
            }
        }

        // 选中的保留字段
        private List<string> _selectedFields = new List<string>();
        public List<string> SelectedFields
        {
            get => _selectedFields;
            set
            {
                _selectedFields = value ?? new List<string>();
                SetProperty(ref _selectedFields, value);
                NotifyPropertyChanged(() => SelectedFieldsDisplayText);
            }
        }

        // 选中字段的显示文本
        public string SelectedFieldsDisplayText
        {
            get
            {
                if (SelectedFields == null || SelectedFields.Count == 0)
                {
                    return "未选择字段";
                }
                return $"已选择 {SelectedFields.Count} 个字段";
            }
        }

        // 生成模式列表与当前选择（四向点/四角点）
        public ObservableCollection<string> GenerationModes { get; private set; } = new ObservableCollection<string>();

        private string _selectedGenerationMode;
        public string SelectedGenerationMode
        {
            get => _selectedGenerationMode;
            set { SetProperty(ref _selectedGenerationMode, value); }
        }

        // 选择集相关属性
        private bool _useSelection = true;
        public bool UseSelection
        {
            get => _useSelection;
            set { SetProperty(ref _useSelection, value); }
        }

        private bool _hasSelection;
        public bool HasSelection
        {
            get => _hasSelection;
            set { SetProperty(ref _hasSelection, value); }
        }

        private int _selectedCount;
        public int SelectedCount
        {
            get => _selectedCount;
            set { SetProperty(ref _selectedCount, value); }
        }

        private string _selectionInfoText;
        public string SelectionInfoText
        {
            get => _selectionInfoText;
            set { SetProperty(ref _selectionInfoText, value); }
        }

        #endregion

        #region 命令

        // 浏览输出路径命令
        private ICommand _browseOutputCommand;
        public ICommand BrowseOutputCommand
        {
            get
            {
                return _browseOutputCommand ?? (_browseOutputCommand = new RelayCommand(() =>
                {
                    try
                    {
                        var initialLocation = GetProjectGDBPath();
                        var picked = PathDialogUtils.PickSaveFeatureClassPath("选择输出位置", initialLocation);
                        if (!string.IsNullOrWhiteSpace(picked))
                            OutputPath = picked;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"选择输出位置出错: {ex.Message}", "错误");
                    }
                }));
            }
        }

        // 取消命令（只用于停止处理）
        private ICommand _cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                return _cancelCommand ?? (_cancelCommand = new RelayCommand(() =>
                {
                    if (IsProcessing)
                    {
                        // 如果正在处理，则设置取消标志
                        CancelRequested = true;
                        StatusMessage = "正在取消操作...";
                        LogWarning("用户请求取消操作");
                    }
                }, () => IsProcessing));
            }
        }

        // 运行命令
        private ICommand _runCommand;
        public ICommand RunCommand
        {
            get
            {
                return _runCommand ?? (_runCommand = new RelayCommand(Execute, () => CanExecute()));
            }
        }
        
        // 帮助命令
        private ICommand _showHelpCommand;
        public ICommand ShowHelpCommand
        {
            get
            {
                return _showHelpCommand ?? (_showHelpCommand = new RelayCommand(() => ShowHelp()));
            }
        }

        // 选择字段命令
        private ICommand _selectFieldsCommand;
        public ICommand SelectFieldsCommand
        {
            get
            {
                return _selectFieldsCommand ?? (_selectFieldsCommand = new RelayCommand(() => SelectFields(), () => HasSelectedLayer));
            }
        }

        // 刷新图层命令
        private ICommand _refreshLayersCommand;
        public ICommand RefreshLayersCommand
        {
            get
            {
                return _refreshLayersCommand ?? (_refreshLayersCommand = new RelayCommand(() => RefreshLayers()));
            }
        }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public BoundaryPointGeneratorDockPaneViewModel()
        {
            // 初始化属性
            PolygonLayers = new ObservableCollection<FeatureLayer>();

            // 设置默认输出路径为工程数据库
            UpdateOutputPath();

            StatusMessage = "请选择面图层。";
            LogContent = "";
            Progress = 0;
            IsProgressIndeterminate = false;

            // 加载面图层
            LoadPolygonLayers();

            // 订阅地图选择变化事件并初始化一次选择信息
            _mapSelectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
            UpdateSelectionInfo();

            // 初始化生成模式
            GenerationModes.Clear();
            GenerationModes.Add("四向点");
            GenerationModes.Add("四角点");
            SelectedGenerationMode = "四向点";
        }

        /// <summary>
        /// 刷新图层列表（供DockPane调用）
        /// </summary>
        public void RefreshLayers()
        {
            LoadPolygonLayers();
        }

        /// <summary>
        /// 加载面图层
        /// </summary>
        private void LoadPolygonLayers()
        {
            Task.Run(async () =>
            {
                try
                {
                    var layers = await LayerUtils.GetPolygonLayersAsync();
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var fl in layers) PolygonLayers.Add(fl);
                        if (PolygonLayers.Count > 0)
                            SelectedPolygonLayer = PolygonLayers[0];
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                    });
                }
            });
        }

        /// <summary>
        /// 更新所选要素数量提示
        /// </summary>
        private void UpdateSelectionInfo()
        {
            Task.Run(async () =>
            {
                var info = await SelectionUtils.GetSelectionInfoAsync(SelectedPolygonLayer);
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    UseSelection = SelectionUtils.RecommendUseSelection(UseSelection, info.HasSelection);
                    HasSelection = info.HasSelection;
                    SelectedCount = info.Count;
                    SelectionInfoText = info.InfoText;
                });
            });
        }

        /// <summary>
        /// 地图选择变化事件
        /// </summary>
        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            UpdateSelectionInfo();
        }
        
        /// <summary>
        /// 获取当前项目地理数据库路径
        /// </summary>
        private string GetProjectGDBPath()
        {
            return PathDialogUtils.GetProjectDefaultGdb();
        }

        /// <summary>
        /// 获取地理处理工具的字段类型
        /// </summary>
        private string GetGeoprocessingFieldType(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.String => "TEXT",
                FieldType.Integer => "LONG",
                FieldType.SmallInteger => "SHORT",
                FieldType.Double => "DOUBLE",
                FieldType.Single => "FLOAT",
                FieldType.Date => "DATE",
                FieldType.GUID => "GUID",
                FieldType.GlobalID => "GUID",
                _ => "TEXT"
            };
        }

        /// <summary>
        /// 更新输出路径，默认命名：图层名称+SZD
        /// </summary>
        private void UpdateOutputPath()
        {
            string projectGDB = GetProjectGDBPath();
            string outputName;

            if (SelectedPolygonLayer != null)
            {
                // 格式：图层名称+SZD
                outputName = $"{SelectedPolygonLayer.Name}_SZD";
            }
            else
            {
                // 默认名称
                outputName = "四至点SZD";
            }

            if (!string.IsNullOrEmpty(projectGDB))
            {
                OutputPath = Path.Combine(projectGDB, outputName);
            }
            else
            {
                OutputPath = outputName;
            }
        }

        /// 确定命令是否可以执行
        /// </summary>
        private bool CanExecute()
        {
            return SelectedPolygonLayer != null && !string.IsNullOrEmpty(OutputPath) && !IsProcessing;
        }

        /// <summary>
        /// 执行生成四至坐标点操作
        /// </summary>
        private async void Execute()
        {
            // 防止重复执行
            if (IsProcessing)
            {
                LogWarning("工具正在运行中，请等待完成后再次执行");
                return;
            }

            // 重置取消标志
            CancelRequested = false;
            // 设置处理状态
            IsProcessing = true;

            StatusMessage = "正在处理...";
            ClearLog();
            // 重置进度条
            Progress = 0;
            IsProgressIndeterminate = true;

            try
            {
                LogInfo("开始生成四至坐标点...");

                await QueuedTask.Run(async () =>
                {
                    try
                    {
                        // 检查取消请求
                        if (CancelRequested)
                        {
                            LogWarning("操作已取消");
                            return;
                        }

                        // 创建输出要素类
                        var outputFeatureClassPath = await CreateOutputFeatureClass();
                        if (outputFeatureClassPath == null)
                        {
                            LogError("创建输出要素类失败");
                            return;
                        }

                        LogInfo($"成功创建输出要素类: {outputFeatureClassPath}");

                        // 处理面要素，生成四至坐标点
                        await ProcessPolygonFeatures(outputFeatureClassPath);

                        if (!CancelRequested)
                        {
                            LogInfo("四至坐标点生成完成！");

                            // 在UI线程更新状态
                            if (System.Windows.Application.Current?.Dispatcher != null)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    StatusMessage = "处理完成！";
                                    Progress = 100;
                                    IsProgressIndeterminate = false;
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理过程中发生错误: {ex.Message}");
                        if (System.Windows.Application.Current?.Dispatcher != null)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                StatusMessage = $"处理失败: {ex.Message}";
                            });
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"执行失败: {ex.Message}");
                StatusMessage = $"执行失败: {ex.Message}";
            }
            finally
            {
                // 重置处理状态
                IsProcessing = false;
                IsProgressIndeterminate = false;
            }
        }

        /// <summary>
        /// 创建输出要素类
        /// </summary>
        private async Task<string> CreateOutputFeatureClass()
        {
            try
            {
                // 准备输出路径信息
                string outputPath = OutputPath;
                if (string.IsNullOrEmpty(outputPath))
                {
                    LogError("输出路径为空");
                    return null;
                }

                // 获取输入图层的空间参考
                SpatialReference spatialReference = null;
                if (SelectedPolygonLayer != null)
                {
                    try
                    {
                        using (var table = SelectedPolygonLayer.GetTable())
                        {
                            if (table != null)
                            {
                                var definition = table.GetDefinition() as FeatureClassDefinition;
                                spatialReference = definition?.GetSpatialReference();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"获取空间参考失败: {ex.Message}");
                        throw;
                    }
                }

                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_SZD" : "四至坐标点SZD";
                var info = OutputDatasetUtils.ParseOutputPath(outputPath, defName);

                // 覆盖处理
                if (OutputDatasetUtils.Exists(info))
                {
                    bool overwrite = false;
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            var msg = info.IsGdb
                                ? $"目标要素类已存在：{info.CatalogPath}。是否覆盖？"
                                : $"目标Shapefile已存在：{info.CatalogPath}。是否覆盖？";
                            var result = MessageBox.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                            overwrite = result == System.Windows.MessageBoxResult.Yes;
                        });
                    }
                    if (!overwrite)
                    {
                        LogWarning("用户取消覆盖，操作已中止。");
                        return null;
                    }

                    try
                    {
                        await OutputDatasetUtils.DeleteIfExistsAsync(info);
                        LogInfo($"删除已存在的数据集: {info.CatalogPath}");
                    }
                    catch (Exception delEx)
                    {
                        LogWarning($"删除已有数据集失败: {delEx.Message}");
                    }
                }

                // 创建要素类
                var featureClassPath = await OutputDatasetUtils.CreateFeatureClassAsync(info, "POINT", spatialReference);

                // 添加源要素ID字段
                var addFieldParams0 = Geoprocessing.MakeValueArray(
                    featureClassPath,
                    "源要素ID",
                    "LONG"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams0);

                // 添加方向字段
                var addFieldParams1 = Geoprocessing.MakeValueArray(
                    featureClassPath,
                    "方向",
                    "TEXT",
                    null, null, 10
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams1);

                // 添加X坐标字段（按测绘习惯，此处字段名保持不变，但写入时将交换XY）
                var addFieldParams2 = Geoprocessing.MakeValueArray(
                    featureClassPath,
                    "X坐标_米",
                    "DOUBLE"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams2);

                // 添加Y坐标字段（按测绘习惯，此处字段名保持不变，但写入时将交换XY）
                var addFieldParams3 = Geoprocessing.MakeValueArray(
                    featureClassPath,
                    "Y坐标_米",
                    "DOUBLE"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams3);

                // 添加选中的保留字段
                if (SelectedFields != null && SelectedFields.Count > 0)
                {
                    try
                    {
                        using (var table = SelectedPolygonLayer.GetTable())
                        {
                            if (table != null)
                            {
                                var definition = table.GetDefinition();
                                var sourceFields = definition.GetFields();

                                foreach (var fieldName in SelectedFields)
                                {
                                    var sourceField = sourceFields.FirstOrDefault(f => f.Name == fieldName);
                                    if (sourceField != null)
                                    {
                                        var fieldType = GetGeoprocessingFieldType(sourceField.FieldType);
                                        var addFieldParams = Geoprocessing.MakeValueArray(
                                            featureClassPath,
                                            sourceField.Name,
                                            fieldType,
                                            null, null, sourceField.Length > 0 ? sourceField.Length : (object)null
                                        );
                                        await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams);
                                        LogInfo($"添加保留字段: {sourceField.Name} ({fieldType})");
                                    }
                                }
                            }
                            else
                            {
                                LogError("无法获取图层表格");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"添加保留字段失败: {ex.Message}");
                        throw;
                    }
                }

                return featureClassPath;
            }
            catch (Exception ex)
            {
                LogError($"创建输出要素类时发生错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 处理面要素，生成四至坐标点
        /// </summary>
        private async Task ProcessPolygonFeatures(string outputFeatureClassPath)
        {
            try
            {
                using (var inputTable = SelectedPolygonLayer.GetTable())
                {
                    if (inputTable == null)
                    {
                        LogError("无法获取输入图层的表格");
                        return;
                    }
                    // 收集所有要创建的点
                    var pointsToCreate = new List<(MapPoint point, Dictionary<string, object> attributes)>();

                    int totalFeatures = 0;
                    int processedFeatures = 0;

                    // 根据是否存在选择集且用户选择启用决定处理范围
                    bool useSelection = UseSelection && SelectedPolygonLayer.SelectionCount > 0;
                    if (useSelection)
                    {
                        totalFeatures = SelectedPolygonLayer.SelectionCount;
                        LogInfo($"检测到选择集: {totalFeatures} 个要素，将仅处理选择的要素。");
                    }
                    else
                    {
                        // 先计算总数（全部要素）
                        using (var countCursor = inputTable.Search())
                        {
                            while (countCursor.MoveNext())
                            {
                                totalFeatures++;
                            }
                        }
                        LogInfo($"未检测到选择集，将处理全部 {totalFeatures} 个要素。");
                    }

                    // 处理每个要素（优先处理选择集）
                    using (var cursor = useSelection
                        ? SelectedPolygonLayer.GetSelection().Search(new QueryFilter(), false)
                        : inputTable.Search())
                    {
                        while (cursor.MoveNext())
                        {
                            if (CancelRequested)
                            {
                                LogWarning("操作已取消");
                                break;
                            }

                            var feature = cursor.Current as Feature;
                            if (feature != null)
                            {
                                // 获取面几何和要素ID
                                var polygon = feature.GetShape() as Polygon;
                                var featureOID = feature.GetObjectID();

                                LogInfo($"正在处理面要素 {featureOID}");

                                if (polygon != null)
                                {
                                    LogInfo($"面要素 {featureOID} 的几何信息: PartCount={polygon.PartCount}, Area={polygon.Area:F2}");

                                    // 按生成模式生成四至坐标点
                                    List<BoundaryPoint> boundaryPoints = SelectedGenerationMode == "四角点"
                                        ? GenerateCornerPoints(polygon)
                                        : GenerateBoundaryPoints(polygon);

                                    LogInfo($"面要素 {featureOID} 生成了 {boundaryPoints.Count} 个四至点");

                                    // 收集点要素数据
                                    foreach (var point in boundaryPoints)
                                    {
                                        var attributes = new Dictionary<string, object>
                                        {
                                            ["源要素ID"] = featureOID,
                                            ["方向"] = point.Direction,
                                            // 按测绘习惯交换XY：X字段写入Y值，Y字段写入X值
                                            ["X坐标_米"] = point.Point.Y,
                                            ["Y坐标_米"] = point.Point.X
                                        };

                                        // 添加保留字段的值
                                        if (SelectedFields != null && SelectedFields.Count > 0)
                                        {
                                            foreach (var fieldName in SelectedFields)
                                            {
                                                try
                                                {
                                                    var fieldValue = feature[fieldName];
                                                    attributes[fieldName] = fieldValue;
                                                }
                                                catch (Exception ex)
                                                {
                                                    LogWarning($"获取字段 {fieldName} 的值失败: {ex.Message}");
                                                    attributes[fieldName] = null;
                                                }
                                            }
                                        }

                                        pointsToCreate.Add((point.Point, attributes));
                                        // 日志中也按测绘习惯显示为 (X=Y, Y=X)
                                        LogInfo($"  - {point.Direction}点: ({point.Point.Y:F2}, {point.Point.X:F2})");
                                    }
                                }
                                else
                                {
                                    LogWarning($"面要素 {featureOID} 的几何为空");
                                }
                            }
                            else
                            {
                                LogWarning($"第 {processedFeatures + 1} 个要素为空");
                            }

                            processedFeatures++;

                            // 更新进度
                            int progressValue = (int)((double)processedFeatures / totalFeatures * 100);
                            if (System.Windows.Application.Current?.Dispatcher != null)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    Progress = progressValue;
                                    IsProgressIndeterminate = false;
                                });
                            }

                            LogInfo($"已处理 {processedFeatures}/{totalFeatures} 个要素");
                        }
                    }

                    // 使用地理处理工具插入点
                    if (!CancelRequested && pointsToCreate.Count > 0)
                    {
                        LogInfo($"正在插入 {pointsToCreate.Count} 个点要素...");
                        await InsertPointFeatures(outputFeatureClassPath, pointsToCreate);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"处理面要素时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 插入点要素
        /// </summary>
        private async Task InsertPointFeatures(string outputFeatureClassPath, List<(MapPoint point, Dictionary<string, object> attributes)> pointsToCreate)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    // 打开输出要素类（兼容 GDB 根、要素数据集路径与 Shapefile）
                    var catalogPath = outputFeatureClassPath;
                    if (string.IsNullOrEmpty(catalogPath))
                    {
                        LogError("无效的输出要素类路径");
                        return;
                    }

                    FeatureClass featureClass = null;
                    Geodatabase gdb = null;
                    FileSystemDatastore fsds = null;
                    try
                    {
                        if (catalogPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                        {
                            var folder = Path.GetDirectoryName(catalogPath);
                            var shpName = Path.GetFileName(catalogPath);
                            var conn = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                            fsds = new FileSystemDatastore(conn);
                            featureClass = fsds.OpenDataset<FeatureClass>(shpName);
                        }
                        else
                        {
                            int gdbIdx = catalogPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                            if (gdbIdx >= 0)
                            {
                                var gdbRoot = catalogPath.Substring(0, gdbIdx + 4);
                                var relative = catalogPath.Length > gdbIdx + 4 ? catalogPath.Substring(gdbIdx + 4).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : string.Empty;
                                if (string.IsNullOrEmpty(relative))
                                {
                                    LogError("GDB 输出路径缺少要素类名");
                                    return;
                                }
                                gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbRoot)));
                                featureClass = gdb.OpenDataset<FeatureClass>(relative);
                            }
                            else
                            {
                                // 兜底：尝试按目录 + 名称方式打开
                                var workspace = Path.GetDirectoryName(catalogPath);
                                var featureClassName = Path.GetFileNameWithoutExtension(catalogPath);
                                bool isGdb = workspace.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase);
                                if (isGdb)
                                {
                                    gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace)));
                                    featureClass = gdb.OpenDataset<FeatureClass>(featureClassName);
                                }
                                else
                                {
                                    var conn = new FileSystemConnectionPath(new Uri(workspace), FileSystemDatastoreType.Shapefile);
                                    fsds = new FileSystemDatastore(conn);
                                    featureClass = fsds.OpenDataset<FeatureClass>(featureClassName + ".shp");
                                }
                            }
                        }

                        int insertedCount = 0;
                        
                        foreach (var pointData in pointsToCreate)
                        {
                            if (CancelRequested) break;

                            try
                            {
                                using (var rowBuffer = featureClass.CreateRowBuffer())
                                {
                                    // 设置几何
                                    rowBuffer[featureClass.GetDefinition().GetShapeField()] = pointData.point;

                                    // 设置属性
                                    foreach (var attr in pointData.attributes)
                                    {
                                        try
                                        {
                                            rowBuffer[attr.Key] = attr.Value;
                                        }
                                        catch (Exception ex)
                                        {
                                            LogWarning($"设置字段 {attr.Key} 的值失败: {ex.Message}");
                                        }
                                    }

                                    using (var feature = featureClass.CreateRow(rowBuffer))
                                    {
                                        feature.Store();
                                        insertedCount++;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError($"插入点要素失败: {ex.Message}");
                            }
                        }

                        LogInfo($"成功插入 {insertedCount} 个点要素");
                    }
                    finally
                    {
                        featureClass?.Dispose();
                        gdb?.Dispose();
                        fsds?.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"插入点要素时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成面要素的四至坐标点（基于面折点的最东西南北点）
        /// </summary>
        private List<BoundaryPoint> GenerateBoundaryPoints(Polygon polygon)
        {
            var points = new List<BoundaryPoint>();

            try
            {
                // 获取面的所有折点
                var allPoints = new List<MapPoint>();

                // 遍历所有环（外环和内环）
                for (int partIndex = 0; partIndex < polygon.PartCount; partIndex++)
                {
                    var part = polygon.Parts[partIndex];

                    // 遍历每个线段的端点
                    foreach (var segment in part)
                    {
                        allPoints.Add(segment.StartPoint);
                        allPoints.Add(segment.EndPoint);
                    }
                }

                if (allPoints.Count == 0)
                {
                    LogWarning("面要素没有找到折点");
                    return points;
                }

                // 去除重复点（使用坐标比较）
                var uniquePoints = new List<MapPoint>();
                foreach (var point in allPoints)
                {
                    bool isDuplicate = false;
                    foreach (var existingPoint in uniquePoints)
                    {
                        if (Math.Abs(point.X - existingPoint.X) < 0.0001 &&
                            Math.Abs(point.Y - existingPoint.Y) < 0.0001)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                    if (!isDuplicate)
                    {
                        uniquePoints.Add(point);
                    }
                }

                if (uniquePoints.Count == 0)
                {
                    LogWarning("去重后没有有效的折点");
                    return points;
                }

                // 找到最东、最西、最南、最北的折点
                MapPoint eastPoint = uniquePoints[0];   // 最大X坐标
                MapPoint westPoint = uniquePoints[0];   // 最小X坐标
                MapPoint southPoint = uniquePoints[0];  // 最小Y坐标
                MapPoint northPoint = uniquePoints[0];  // 最大Y坐标

                foreach (var point in uniquePoints)
                {
                    // 最东点（最大X坐标）
                    if (point.X > eastPoint.X)
                    {
                        eastPoint = point;
                    }

                    // 最西点（最小X坐标）
                    if (point.X < westPoint.X)
                    {
                        westPoint = point;
                    }

                    // 最南点（最小Y坐标）
                    if (point.Y < southPoint.Y)
                    {
                        southPoint = point;
                    }

                    // 最北点（最大Y坐标）
                    if (point.Y > northPoint.Y)
                    {
                        northPoint = point;
                    }
                }

                // 添加四至坐标点
                points.Add(new BoundaryPoint { Point = eastPoint, Direction = "东" });
                points.Add(new BoundaryPoint { Point = westPoint, Direction = "西" });
                points.Add(new BoundaryPoint { Point = southPoint, Direction = "南" });
                points.Add(new BoundaryPoint { Point = northPoint, Direction = "北" });
            }
            catch (Exception ex)
            {
                LogError($"生成四至坐标点时发生错误: {ex.Message}");
            }

            return points;
        }

        /// <summary>
        /// 生成面要素的四角点（基于外包矩形的四个角：东北/西北/东南/西南）
        /// </summary>
        private List<BoundaryPoint> GenerateCornerPoints(Polygon polygon)
        {
            var points = new List<BoundaryPoint>();
            try
            {
                if (polygon == null)
                {
                    LogWarning("面要素几何为空，无法生成四角点");
                    return points;
                }

                // 提取所有折点
                var allPoints = new List<MapPoint>();
                for (int partIndex = 0; partIndex < polygon.PartCount; partIndex++)
                {
                    var part = polygon.Parts[partIndex];
                    foreach (var segment in part)
                    {
                        allPoints.Add(segment.StartPoint);
                        allPoints.Add(segment.EndPoint);
                    }
                }

                if (allPoints.Count == 0)
                {
                    LogWarning("面要素没有找到折点");
                    return points;
                }

                // 去重（与四向点相同的策略）
                var uniquePoints = new List<MapPoint>();
                foreach (var p in allPoints)
                {
                    bool isDup = false;
                    foreach (var q in uniquePoints)
                    {
                        if (Math.Abs(p.X - q.X) < 0.0001 && Math.Abs(p.Y - q.Y) < 0.0001)
                        {
                            isDup = true;
                            break;
                        }
                    }
                    if (!isDup) uniquePoints.Add(p);
                }

                if (uniquePoints.Count == 0)
                {
                    LogWarning("去重后没有有效的折点");
                    return points;
                }

                // 使用对角方向的线性评分挑选拐角
                // NE: 最大化 (x + y)
                // NW: 最大化 (y - x)
                // SE: 最大化 (x - y)
                // SW: 最小化 (x + y)（等价于最大化 -(x + y)）
                MapPoint ne = uniquePoints[0];
                MapPoint nw = uniquePoints[0];
                MapPoint se = uniquePoints[0];
                MapPoint sw = uniquePoints[0];

                double neScore = ne.X + ne.Y;
                double nwScore = ne.Y - ne.X;
                double seScore = ne.X - ne.Y;
                double swScore = ne.X + ne.Y; // 用作最小比较

                foreach (var p in uniquePoints)
                {
                    double sNE = p.X + p.Y;
                    double sNW = p.Y - p.X;
                    double sSE = p.X - p.Y;
                    double sSW = p.X + p.Y;

                    if (sNE > neScore || (Math.Abs(sNE - neScore) < 1e-9 && (p.X > ne.X || p.Y > ne.Y)))
                    { ne = p; neScore = sNE; }
                    if (sNW > nwScore || (Math.Abs(sNW - nwScore) < 1e-9 && (p.Y > nw.Y || p.X < nw.X)))
                    { nw = p; nwScore = sNW; }
                    if (sSE > seScore || (Math.Abs(sSE - seScore) < 1e-9 && (p.X > se.X || p.Y < se.Y)))
                    { se = p; seScore = sSE; }
                    if (sSW < swScore || (Math.Abs(sSW - swScore) < 1e-9 && (p.X < sw.X || p.Y < sw.Y)))
                    { sw = p; swScore = sSW; }
                }

                points.Add(new BoundaryPoint { Point = ne, Direction = "东北" });
                points.Add(new BoundaryPoint { Point = nw, Direction = "西北" });
                points.Add(new BoundaryPoint { Point = se, Direction = "东南" });
                points.Add(new BoundaryPoint { Point = sw, Direction = "西南" });
            }
            catch (Exception ex)
            {
                LogError($"生成四角点时发生错误: {ex.Message}");
            }
            return points;
        }

        #region 日志方法

        /// <summary>
        /// 清除日志
        /// </summary>
        private void ClearLog()
        {
            _logBuilder.Clear();
            LogContent = "";
        }

        /// <summary>
        /// 记录信息
        /// </summary>
        private void LogInfo(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logBuilder.AppendLine(logMessage);

            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LogContent = _logBuilder.ToString();
                });
            }
        }

        /// <summary>
        /// 记录警告
        /// </summary>
        private void LogWarning(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] 警告: {message}";
            _logBuilder.AppendLine(logMessage);

            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LogContent = _logBuilder.ToString();
                });
            }
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        private void LogError(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] 错误: {message}";
            _logBuilder.AppendLine(logMessage);

            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LogContent = _logBuilder.ToString();
                });
            }
        }

        #endregion

        /// <summary>
        /// 选择保留字段
        /// </summary>
        private void SelectFields()
        {
            if (SelectedPolygonLayer == null) return;

            try
            {
                QueuedTask.Run(() =>
                {
                    try
                    {
                        using (var table = SelectedPolygonLayer.GetTable())
                        {
                            if (table != null)
                            {
                                var definition = table.GetDefinition();
                                var fields = definition.GetFields().ToList();

                                // 在UI线程显示对话框
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var dialog = new FieldSelectionDialog(fields, SelectedFields);
                                    if (dialog.ShowDialog() == true)
                                    {
                                        SelectedFields = dialog.SelectedFieldNames;
                                        LogInfo($"已选择 {SelectedFields.Count} 个保留字段");
                                    }
                                });
                            }
                            else
                            {
                                LogError("无法获取图层表格");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"获取字段列表失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"获取字段列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理事件订阅
        /// </summary>
        public void Cleanup()
        {
            if (_mapSelectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_mapSelectionChangedToken);
                _mapSelectionChangedToken = null;
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private void ShowHelp()
        {
            var helpContent = "生成四至坐标点工具使用说明\n\n" +
                "功能描述：\n" +
                "根据输入的面要素生成四至坐标点（最东、最西、最南、最北点）。\n\n" +
                "参数说明：\n" +
                "• 面图层：选择要处理的面要素图层\n" +
                "• 输出四至点图层：指定输出点要素的位置和名称\n" +
                "• 保留原始字段：选择要从源图层复制到输出图层的字段\n\n" +
                "操作步骤：\n" +
                "1. 选择要处理的面图层\n" +
                "2. 设置输出四至点图层路径（默认输出到工程数据库）\n" +
                "3. 可选：点击\"选择字段...\"选择要保留的原始字段\n" +
                "4. 点击\"开始\"按钮执行处理\n" +
                "5. 处理过程中可点击\"停止\"按钮取消操作\n\n" +
                "输出结果：\n" +
                "生成的点要素包含以下字段：\n" +
                "• 源要素ID：源面要素的ObjectID\n" +
                "• 方向：标识点的方位（东、西、南、北）\n" +
                "• X坐标_米：点的X坐标值\n" +
                "• Y坐标_米：点的Y坐标值\n" +
                "• 选中的原始字段：从源图层复制的字段值\n\n" +
                "注意事项：\n" +
                "• 输出到文件夹时将创建Shapefile格式\n" +
                "• 输出到地理数据库时将创建要素类\n" +
                "• 四至点基于面要素本身折点的最东西南北坐标计算\n" +
                "• 每个面要素将生成4个点（东、西、南、北各一个）\n" +
                "• 保留字段的值会复制到每个生成的四至点";

            MessageBox.Show(helpContent, "帮助", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// 四至坐标点数据结构
    /// </summary>
    internal class BoundaryPoint
    {
        public MapPoint Point { get; set; }
        public string Direction { get; set; }
    }
}
