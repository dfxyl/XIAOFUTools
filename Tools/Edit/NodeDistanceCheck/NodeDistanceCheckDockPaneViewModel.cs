using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Editing;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.NodeDistanceCheck
{
    /// <summary>
    /// 节点距离检查工具视图模型
    /// </summary>
    internal class NodeDistanceCheckDockPaneViewModel : PropertyChangedBase
    {
        #region 属性

        // 取消操作标志
        private bool _cancelRequested = false;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
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
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // 是否可以处理
        public bool CanProcess
        {
            get
            {
                bool canProcess = !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrEmpty(OutputPath);
                System.Diagnostics.Debug.WriteLine($"CanProcess: {canProcess}, IsProcessing: {IsProcessing}, SelectedPolygonLayer: {SelectedPolygonLayer?.Name ?? "null"}, OutputPath: {OutputPath ?? "null"}");
                return canProcess;
            }
        }

        // 面要素图层列表
        private ObservableCollection<FeatureLayer> _polygonLayers;
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }

        // 选中的面要素图层
        private FeatureLayer _selectedPolygonLayer;
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                UpdateOutputPath();
                NotifyPropertyChanged(() => CanProcess);
                NotifyPropertyChanged(() => HasSelectedLayer);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // 节点检查选项列表
        private ObservableCollection<string> _checkOptions;
        public ObservableCollection<string> CheckOptions
        {
            get => _checkOptions;
            set => SetProperty(ref _checkOptions, value);
        }

        // 选中的节点检查选项
        private string _selectedCheckOption;
        public string SelectedCheckOption
        {
            get => _selectedCheckOption;
            set => SetProperty(ref _selectedCheckOption, value);
        }

        // 节点检查距离
        private double _checkDistance = 1.0;
        public double CheckDistance
        {
            get => _checkDistance;
            set => SetProperty(ref _checkDistance, value);
        }

        // 输出线要素图层路径
        private string _outputPath;
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                // 使用通用工具规范化输出路径（自动识别 GDB/SHP）
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_节点距离检查" : "节点距离检查结果";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPath, normalized);
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // 状态信息
        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // 日志内容
        private string _logContent;
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        // 日志构建器
        private StringBuilder _logBuilder = new StringBuilder();

        // 进度
        private int _progress;
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        // 是否为不确定进度
        private bool _isProgressIndeterminate;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        // 是否有选中图层
        public bool HasSelectedLayer => SelectedPolygonLayer != null;

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
                        var saveItemDialog = new SaveItemDialog
                        {
                            Title = "选择输出位置",
                            OverwritePrompt = true,
                            DefaultExt = "shp",
                            Filter = ItemFilters.FeatureClasses_All
                        };

                        var initialLocation = GetProjectGDBPath();
                        if (!string.IsNullOrEmpty(initialLocation))
                        {
                            saveItemDialog.InitialLocation = initialLocation;
                        }

                        bool? dialogResult = saveItemDialog.ShowDialog();
                        if (dialogResult == true)
                        {
                            OutputPath = saveItemDialog.FilePath;
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"选择输出位置出错: {ex.Message}", "错误");
                    }
                }));
            }
        }

        // 取消命令
        private ICommand _cancelCommand;
        public ICommand CancelCommand
        {
            get
            {
                return _cancelCommand ?? (_cancelCommand = new RelayCommand(() =>
                {
                    if (IsProcessing)
                    {
                        CancelRequested = true;
                        StatusMessage = "正在取消操作...";
                        LogWarning("用户请求取消操作");
                    }
                }, () => IsProcessing));
            }
        }

        // 运行命令
        private RelayCommand _runCommand;
        public ICommand RunCommand
        {
            get
            {
                if (_runCommand == null)
                {
                    _runCommand = new RelayCommand(Execute, () => CanProcess);
                }
                return _runCommand;
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
        public NodeDistanceCheckDockPaneViewModel()
        {
            // 初始化属性
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            CheckOptions = new ObservableCollection<string>
            {
                "小于等于",
                "小于",
                "大于等于", 
                "大于",
                "等于"
            };

            // 设置默认值
            SelectedCheckOption = "小于等于";
            CheckDistance = 1.0;
            
            StatusMessage = "正在加载图层...";
            LogContent = "";
            Progress = 0;
            IsProgressIndeterminate = false;

            // 先设置一个默认的输出路径
            UpdateOutputPath();

            // 加载面图层
            LoadPolygonLayers();
        }

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            LoadPolygonLayers();
        }

        /// <summary>
        /// 获取当前项目地理数据库路径
        /// </summary>
        private string GetProjectGDBPath()
        {
            try
            {
                var project = Project.Current;
                if (project != null)
                {
                    return project.DefaultGeodatabasePath;
                }
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取项目地理数据库路径失败: {ex.Message}");
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
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
        /// 更新输出路径
        /// </summary>
        private void UpdateOutputPath()
        {
            string projectGDB = GetProjectGDBPath();
            string outputName;

            if (SelectedPolygonLayer != null)
            {
                outputName = $"{SelectedPolygonLayer.Name}_节点距离检查";
            }
            else
            {
                outputName = "节点距离检查结果";
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

        /// <summary>
        /// 加载面图层
        /// </summary>
        private void LoadPolygonLayers()
        {
            QueuedTask.Run(() =>
            {
                try
                {
                    var tempLayers = new List<FeatureLayer>();
                    var map = MapView.Active?.Map;

                    if (map != null)
                    {
                        var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();

                        foreach (var layer in layers)
                        {
                            try
                            {
                                using (var table = layer.GetTable())
                                {
                                    if (table != null)
                                    {
                                        var definition = table.GetDefinition() as FeatureClassDefinition;
                                        if (definition?.GetShapeType() == GeometryType.Polygon)
                                        {
                                            tempLayers.Add(layer);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError($"无法访问图层 {layer.Name}: {ex.Message}");
                            }
                        }
                    }

                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            PolygonLayers?.Clear();
                            if (PolygonLayers != null)
                            {
                                foreach (var layer in tempLayers)
                                {
                                    PolygonLayers.Add(layer);
                                }

                                if (PolygonLayers.Count > 0)
                                {
                                    SelectedPolygonLayer = PolygonLayers[0];
                                    StatusMessage = "请设置检查参数，然后点击开始。";
                                }
                                else
                                {
                                    StatusMessage = "未找到面要素图层，请先添加面图层到地图。";
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            StatusMessage = $"加载图层出错: {ex.Message}";
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 执行节点距离检查
        /// </summary>
        private async void Execute()
        {
            if (IsProcessing)
            {
                LogWarning("工具正在运行中，请等待完成后再次执行");
                return;
            }

            CancelRequested = false;
            IsProcessing = true;
            StatusMessage = "正在处理...";
            ClearLog();
            Progress = 0;
            IsProgressIndeterminate = true;

            try
            {
                LogInfo("开始节点距离检查...");

                await QueuedTask.Run(async () =>
                {
                    try
                    {
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

                        // 处理面要素，检查节点距离
                        await ProcessNodeDistanceCheck(outputFeatureClassPath);

                        if (!CancelRequested)
                        {
                            LogInfo("节点距离检查完成！");

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

                // 使用通用工具解析输出路径
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_节点距离检查" : "节点距离检查结果";
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
                var featureClassPath = await OutputDatasetUtils.CreateFeatureClassAsync(info, "POLYLINE", spatialReference);
                LogInfo($"成功创建输出要素类: {featureClassPath}");

                // 添加字段
                var addFieldParams1 = Geoprocessing.MakeValueArray(
                    featureClassPath,
                    "源要素ID",
                    "LONG"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams1);

                var addFieldParams2 = Geoprocessing.MakeValueArray(
                    featureClassPath,
                    "节点距离",
                    "DOUBLE"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams2);

                var addFieldParams3 = Geoprocessing.MakeValueArray(
                    featureClassPath,
                    "检查条件",
                    "TEXT",
                    null, null, 20
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
        /// 处理节点距离检查
        /// </summary>
        private async Task ProcessNodeDistanceCheck(string outputFeatureClassPath)
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

                    var linesToCreate = new List<(Polyline line, Dictionary<string, object> attributes)>();
                    int totalFeatures = 0;
                    int processedFeatures = 0;

                    // 计算总数
                    using (var countCursor = inputTable.Search())
                    {
                        while (countCursor.MoveNext())
                        {
                            totalFeatures++;
                        }
                    }

                    LogInfo($"共找到 {totalFeatures} 个面要素");

                    // 处理每个要素
                    using (var cursor = inputTable.Search())
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
                                var polygon = feature.GetShape() as Polygon;
                                var featureOID = feature.GetObjectID();

                                if (polygon != null)
                                {
                                    // 检查节点距离
                                    var resultLines = CheckNodeDistances(polygon, featureOID, feature);
                                    linesToCreate.AddRange(resultLines);

                                    LogInfo($"面要素 {featureOID} 检查完成，找到 {resultLines.Count} 条符合条件的线段");
                                }
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
                        }
                    }

                    // 插入线要素
                    if (!CancelRequested && linesToCreate.Count > 0)
                    {
                        LogInfo($"正在插入 {linesToCreate.Count} 条线要素...");
                        await InsertLineFeatures(outputFeatureClassPath, linesToCreate);
                    }
                    else if (linesToCreate.Count == 0)
                    {
                        LogInfo("未找到符合条件的节点距离");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"处理节点距离检查时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查节点距离
        /// </summary>
        private List<(Polyline line, Dictionary<string, object> attributes)> CheckNodeDistances(Polygon polygon, long featureOID, Feature feature)
        {
            var resultLines = new List<(Polyline line, Dictionary<string, object> attributes)>();

            try
            {
                // 获取多边形的所有顶点
                var points = new List<MapPoint>();
                foreach (var part in polygon.Parts)
                {
                    foreach (var segment in part)
                    {
                        points.Add(segment.StartPoint);
                    }
                    // 添加最后一个点
                    if (part.Count > 0)
                    {
                        points.Add(part[part.Count - 1].EndPoint);
                    }
                }

                // 检查相邻节点之间的距离
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var point1 = points[i];
                    var point2 = points[i + 1];
                    
                    double distance = GeometryEngine.Instance.Distance(point1, point2);
                    
                    bool meetsCriteria = false;
                    switch (SelectedCheckOption)
                    {
                        case "小于等于":
                            meetsCriteria = distance <= CheckDistance;
                            break;
                        case "小于":
                            meetsCriteria = distance < CheckDistance;
                            break;
                        case "大于等于":
                            meetsCriteria = distance >= CheckDistance;
                            break;
                        case "大于":
                            meetsCriteria = distance > CheckDistance;
                            break;
                        case "等于":
                            meetsCriteria = Math.Abs(distance - CheckDistance) < 0.001; // 允许小的浮点误差
                            break;
                    }

                    if (meetsCriteria)
                    {
                        // 创建连接两个节点的线
                        var linePoints = new List<MapPoint> { point1, point2 };
                        var line = PolylineBuilderEx.CreatePolyline(linePoints, polygon.SpatialReference);

                        var attributes = new Dictionary<string, object>
                        {
                            ["源要素ID"] = featureOID,
                            ["节点距离"] = Math.Round(distance, 3),
                            ["检查条件"] = $"{SelectedCheckOption} {CheckDistance}m"
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

                        resultLines.Add((line, attributes));
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"检查要素 {featureOID} 的节点距离时发生错误: {ex.Message}");
            }

            return resultLines;
        }

        /// <summary>
        /// 插入线要素
        /// </summary>
        private async Task InsertLineFeatures(string outputFeatureClassPath, List<(Polyline line, Dictionary<string, object> attributes)> linesToCreate)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var workspace = Path.GetDirectoryName(outputFeatureClassPath);
                    var featureClassName = Path.GetFileNameWithoutExtension(outputFeatureClassPath);

                    if (string.IsNullOrEmpty(workspace) || string.IsNullOrEmpty(featureClassName))
                    {
                        LogError("无效的输出要素类路径");
                        return;
                    }

                    using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace))))
                    using (var featureClass = geodatabase.OpenDataset<FeatureClass>(featureClassName))
                    {
                        int insertedCount = 0;
                        
                        foreach (var (line, attributes) in linesToCreate)
                        {
                            if (CancelRequested) break;

                            try
                            {
                                using (var rowBuffer = featureClass.CreateRowBuffer())
                                {
                                    // 设置几何
                                    rowBuffer[featureClass.GetDefinition().GetShapeField()] = line;

                                    // 设置属性
                                    foreach (var attr in attributes)
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
                                LogError($"插入要素失败: {ex.Message}");
                            }
                        }

                        LogInfo($"成功插入 {insertedCount} 条线要素");
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"插入线要素时发生错误: {ex.Message}");
            }
        }



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
                LogError($"选择字段时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "节点距离检查工具使用说明\n\n" +
                "功能描述：\n" +
                "检查面要素图层中相邻节点之间的距离，输出符合指定条件的线要素。\n\n" +
                "参数说明：\n" +
                "• 面要素图层：选择要检查的面要素图层\n" +
                "• 节点检查选项：选择距离比较条件（小于等于、小于、大于等于、大于、等于）\n" +
                "• 节点检查距离：设置距离阈值（单位：米）\n" +
                "• 输出线要素图层：指定输出结果的保存位置\n\n" +
                "操作步骤：\n" +
                "1. 选择要检查的面要素图层\n" +
                "2. 选择节点检查选项（如\"小于等于\"）\n" +
                "3. 设置节点检查距离（如1.0米）\n" +
                "4. 指定输出线要素图层路径\n" +
                "5. 点击\"开始\"按钮执行检查\n\n" +
                "输出结果：\n" +
                "• 源要素ID：原始面要素的ID\n" +
                "• 节点距离：相邻节点之间的实际距离\n" +
                "• 检查条件：使用的检查条件和阈值\n\n" +
                "注意事项：\n" +
                "• 只检查相邻节点之间的距离\n" +
                "• 输出的线要素连接符合条件的相邻节点对\n" +
                "• 距离单位为米\n" +
                "• 等于条件允许0.001米的浮点误差";

            MessageBox.Show(helpContent, "节点距离检查工具帮助", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        #region 日志方法

        /// <summary>
        /// 记录信息日志
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
        /// 记录警告日志
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
        /// 记录错误日志
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

        /// <summary>
        /// 清除日志
        /// </summary>
        private void ClearLog()
        {
            _logBuilder.Clear();
            LogContent = "";
        }

        #endregion
    }
}