using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.IO;
using System.Linq;

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.OverlapCheck
{
    /// <summary>
    /// 图形重叠检查工具停靠窗格视图模型
    /// </summary>
    internal class OverlapCheckDockPaneViewModel : PropertyChangedBase
    {
        private const string _dockPaneID = "XIAOFUTools_OverlapCheckDockPane";

        #region 私有字段
        private ObservableCollection<FeatureLayer> _polygonLayers;
        private FeatureLayer _selectedPolygonLayer;
        private string _tolerance = "0.001";
        private string _outputPath = "";
        private string _logContent = "";
        private string _statusMessage = "准备就绪";
        private int _progress = 0;
        private bool _isProgressIndeterminate = false;
        private bool _isProcessing = false;
        private CancellationTokenSource _cancellationTokenSource;
        #endregion

        #region 构造函数
        public OverlapCheckDockPaneViewModel()
        {
            InitializeCommands();
            InitializeData();
        }
        #endregion

        #region 属性
        /// <summary>
        /// 面要素图层列表
        /// </summary>
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }

        /// <summary>
        /// 选中的面要素图层
        /// </summary>
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                UpdateOutputPath();
                NotifyPropertyChanged(nameof(CanProcess));
                NotifyPropertyChanged(nameof(HasSelectedLayer));
            }
        }

        /// <summary>
        /// 容差值
        /// </summary>
        public string Tolerance
        {
            get => _tolerance;
            set => SetProperty(ref _tolerance, value);
        }

        /// <summary>
        /// 输出路径
        /// </summary>
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                // 使用通用工具规范化输出路径（自动识别 GDB/SHP）
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_重叠区域" : "重叠区域";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPath, normalized);
            }
        }

        /// <summary>
        /// 日志内容
        /// </summary>
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// 进度值
        /// </summary>
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        /// <summary>
        /// 进度条是否为不确定状态
        /// </summary>
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        /// <summary>
        /// 是否可以开始处理
        /// </summary>
        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrWhiteSpace(OutputPath);

        /// <summary>
        /// 是否有选中图层
        /// </summary>
        public bool HasSelectedLayer => SelectedPolygonLayer != null;

        /// <summary>
        /// 选中的保留字段
        /// </summary>
        private List<string> _selectedFields = new List<string>();
        public List<string> SelectedFields
        {
            get => _selectedFields;
            set
            {
                _selectedFields = value ?? new List<string>();
                SetProperty(ref _selectedFields, value);
                NotifyPropertyChanged(nameof(SelectedFieldsDisplayText));
            }
        }

        /// <summary>
        /// 选中字段的显示文本
        /// </summary>
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
        public ICommand BrowseOutputCommand { get; private set; }
        public ICommand RunCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand ShowHelpCommand { get; private set; }
        public ICommand SelectFieldsCommand { get; private set; }
        public ICommand RefreshLayersCommand { get; private set; }
        #endregion

        #region 初始化方法
        private void InitializeCommands()
        {
            BrowseOutputCommand = new RelayCommand(BrowseOutput);
            RunCommand = new RelayCommand(async () => await RunOverlapCheck(), () => CanProcess);
            CancelCommand = new RelayCommand(CancelProcess);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            SelectFieldsCommand = new RelayCommand(SelectFields);
            RefreshLayersCommand = new RelayCommand(RefreshLayers);
        }

        private void InitializeData()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            AddLog("图形重叠检查工具已启动");
        }

        /// <summary>
        /// 初始化界面
        /// </summary>
        public void Initialize()
        {
            LoadPolygonLayers();
        }

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            AddLog("正在刷新图层列表...");
            LoadPolygonLayers();
            AddLog("图层列表刷新完成");
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 加载面要素图层
        /// </summary>
        private void LoadPolygonLayers()
        {
            try
            {
                PolygonLayers.Clear();

                var map = MapView.Active?.Map;
                if (map == null)
                {
                    AddLog("当前没有活动地图");
                    return;
                }

                var featureLayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>();
                foreach (var layer in featureLayers)
                {
                    if (layer.ShapeType == esriGeometryType.esriGeometryPolygon)
                    {
                        PolygonLayers.Add(layer);
                    }
                }

                AddLog($"已加载 {PolygonLayers.Count} 个面要素图层");

                // 如果有图层，默认选择第一个
                if (PolygonLayers.Count > 0)
                {
                    SelectedPolygonLayer = PolygonLayers[0];
                }

                // 设置默认输出路径
                UpdateOutputPath();
            }
            catch (Exception ex)
            {
                AddLog($"加载图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 浏览输出路径
        /// </summary>
        private void BrowseOutput()
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
                AddLog($"选择输出位置出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取项目默认地理数据库路径
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
            }
            catch (Exception ex)
            {
                AddLog($"获取项目地理数据库路径失败: {ex.Message}");
            }
            return null;
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
                outputName = $"{SelectedPolygonLayer.Name}_重叠区域";
            }
            else
            {
                outputName = "重叠区域";
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
        /// 添加日志
        /// </summary>
        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] {message}\r\n";
        }

        /// <summary>
        /// 执行重叠检查
        /// </summary>
        private async Task RunOverlapCheck()
        {
            if (SelectedPolygonLayer == null)
            {
                AddLog("请选择面要素图层");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                AddLog("请选择输出路径");
                return;
            }

            if (!double.TryParse(Tolerance, out double tolerance) || tolerance < 0)
            {
                AddLog("请输入有效的容差值");
                return;
            }

            try
            {
                IsProcessing = true;
                IsProgressIndeterminate = true;
                StatusMessage = "正在检查重叠...";
                _cancellationTokenSource = new CancellationTokenSource();

                AddLog("开始执行图形重叠检查...");
                AddLog($"输入图层: {SelectedPolygonLayer.Name}");
                AddLog($"容差值: {tolerance} 米");
                AddLog($"输出路径: {OutputPath}");

                await QueuedTask.Run(async () =>
                {
                    await ProcessOverlapCheck(tolerance, _cancellationTokenSource.Token);
                });

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusMessage = "重叠检查完成";
                    AddLog("图形重叠检查完成！");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
                AddLog("操作已被用户取消");
            }
            catch (Exception ex)
            {
                StatusMessage = "处理失败";
                AddLog($"处理过程中出错: {ex.Message}");
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

        /// <summary>
        /// 重叠信息类
        /// </summary>
        private class OverlapInfo
        {
            public Geometry Geometry { get; set; }
            public long Feature1ObjectID { get; set; }
            public long Feature2ObjectID { get; set; }
            public Dictionary<string, string> Feature1Fields { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, string> Feature2Fields { get; set; } = new Dictionary<string, string>();
        }

        /// <summary>
        /// 处理重叠检查核心逻辑
        /// </summary>
        private async Task ProcessOverlapCheck(double tolerance, CancellationToken cancellationToken)
        {
            using (var table = SelectedPolygonLayer.GetTable())
            {
                // 获取所有要素及其字段值
                var features = new List<(long ObjectID, Geometry Geometry, Dictionary<string, object> Fields)>();

                using (var cursor = table.Search())
                {
                    while (cursor.MoveNext())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        using (var feature = cursor.Current as Feature)
                        {
                            if (feature?.GetShape() != null)
                            {
                                var fieldValues = new Dictionary<string, object>();

                                // 如果有选中的保留字段，读取字段值
                                if (SelectedFields != null && SelectedFields.Count > 0)
                                {
                                    foreach (var fieldName in SelectedFields)
                                    {
                                        try
                                        {
                                            var value = feature[fieldName];
                                            fieldValues[fieldName] = value;
                                        }
                                        catch (Exception ex)
                                        {
                                            AddLog($"读取字段 {fieldName} 失败: {ex.Message}");
                                            fieldValues[fieldName] = null;
                                        }
                                    }
                                }

                                features.Add((feature.GetObjectID(), feature.GetShape(), fieldValues));
                            }
                        }
                    }
                }

                AddLog($"共读取 {features.Count} 个要素");

                // 检查重叠
                var overlaps = new List<OverlapInfo>();
                int totalComparisons = features.Count * (features.Count - 1) / 2;
                int currentComparison = 0;

                for (int i = 0; i < features.Count; i++)
                {
                    for (int j = i + 1; j < features.Count; j++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        currentComparison++;
                        if (currentComparison % 100 == 0)
                        {
                            Progress = (int)((double)currentComparison / totalComparisons * 100);
                            await Task.Delay(1, cancellationToken); // 让UI有机会更新
                        }

                        var feature1 = features[i];
                        var feature2 = features[j];

                        if (GeometryEngine.Instance.Intersects(feature1.Geometry, feature2.Geometry))
                        {
                            var intersection = GeometryEngine.Instance.Intersection(feature1.Geometry, feature2.Geometry);
                            if (intersection != null && intersection.IsEmpty == false)
                            {
                                // 检查重叠面积是否大于容差
                                if (intersection is Polygon polygon && polygon.Area > tolerance)
                                {
                                    var overlapInfo = new OverlapInfo
                                    {
                                        Geometry = intersection,
                                        Feature1ObjectID = feature1.ObjectID,
                                        Feature2ObjectID = feature2.ObjectID
                                    };

                                    // 合并字段值
                                    if (SelectedFields != null && SelectedFields.Count > 0)
                                    {
                                        foreach (var fieldName in SelectedFields)
                                        {
                                            var value1 = feature1.Fields.ContainsKey(fieldName) ? feature1.Fields[fieldName]?.ToString() ?? "" : "";
                                            var value2 = feature2.Fields.ContainsKey(fieldName) ? feature2.Fields[fieldName]?.ToString() ?? "" : "";

                                            // 合并字段值，使用 / 分隔
                                            var combinedValue = $"{value1}/{value2}";
                                            overlapInfo.Feature1Fields[fieldName] = combinedValue;
                                        }
                                    }

                                    overlaps.Add(overlapInfo);
                                    AddLog($"发现重叠: 要素 {feature1.ObjectID} 与要素 {feature2.ObjectID}");
                                }
                            }
                        }
                    }
                }

                AddLog($"共发现 {overlaps.Count} 个重叠区域");

                // 创建输出要素类
                if (overlaps.Count > 0)
                {
                    await CreateOutputFeatureClass(overlaps, cancellationToken);
                }
                else
                {
                    AddLog("未发现重叠区域");
                }
            }
        }

        /// <summary>
        /// 创建输出要素类
        /// </summary>
        private async Task CreateOutputFeatureClass(List<OverlapInfo> overlaps, CancellationToken cancellationToken)
        {
            try
            {
                if (overlaps.Count == 0)
                {
                    AddLog("未发现重叠区域");
                    return;
                }

                AddLog("正在创建输出要素类...");

                string outputPath = OutputPath;
                if (string.IsNullOrEmpty(outputPath))
                {
                    AddLog("输出路径为空");
                    return;
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
                        AddLog($"获取空间参考失败: {ex.Message}");
                        throw;
                    }
                }

                // 使用通用工具解析输出路径
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_重叠区域" : "重叠区域";
                var info = OutputDatasetUtils.ParseOutputPath(outputPath, defName);

                // 覆盖处理
                if (OutputDatasetUtils.Exists(info))
                {
                    bool overwrite = false;
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        var msg = info.IsGdb
                            ? $"目标要素类已存在：{info.CatalogPath}。是否覆盖？"
                            : $"目标Shapefile已存在：{info.CatalogPath}。是否覆盖？";
                        var result = System.Windows.MessageBox.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                        overwrite = result == System.Windows.MessageBoxResult.Yes;
                    });
                    if (!overwrite)
                    {
                        AddLog("用户取消覆盖，操作已中止。");
                        return;
                    }

                    try
                    {
                        await OutputDatasetUtils.DeleteIfExistsAsync(info);
                        AddLog($"删除已存在的数据集: {info.CatalogPath}");
                    }
                    catch (Exception delEx)
                    {
                        AddLog($"删除已有数据集失败: {delEx.Message}");
                    }
                }

                // 创建要素类
                var fullFeatureClassPath = await OutputDatasetUtils.CreateFeatureClassAsync(info, "POLYGON", spatialReference);
                AddLog($"成功创建输出要素类: {fullFeatureClassPath}");
                var featureClassName = info.OutNameNoExt;

                // 添加字段
                var addFieldParams1 = Geoprocessing.MakeValueArray(
                    fullFeatureClassPath,
                    "OVERLAP_ID",
                    "LONG"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams1);

                var addFieldParams2 = Geoprocessing.MakeValueArray(
                    fullFeatureClassPath,
                    "AREA",
                    "DOUBLE"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams2);

                var addFieldParams3 = Geoprocessing.MakeValueArray(
                    fullFeatureClassPath,
                    "TOLERANCE",
                    "DOUBLE"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams3);

                // 添加选中的保留字段（全部作为文本字段）
                if (SelectedFields != null && SelectedFields.Count > 0)
                {
                    try
                    {
                        foreach (var fieldName in SelectedFields)
                        {
                            var addFieldParams = Geoprocessing.MakeValueArray(
                                fullFeatureClassPath,
                                fieldName,
                                "TEXT",
                                null, null, 255  // 设置为255字符长度的文本字段
                            );
                            await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams);
                            AddLog($"添加保留字段: {fieldName} (文本)");
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"添加保留字段失败: {ex.Message}");
                        throw;
                    }
                }

                AddLog($"成功创建输出要素类: {fullFeatureClassPath}");

                // 插入重叠区域要素
                await InsertOverlapFeatures(fullFeatureClassPath, overlaps, cancellationToken);

                AddLog($"重叠检查完成，发现 {overlaps.Count} 个重叠区域");
            }
            catch (Exception ex)
            {
                AddLog($"创建输出要素类时发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 插入重叠区域要素
        /// </summary>
        private async Task InsertOverlapFeatures(string outputFeatureClassPath, List<OverlapInfo> overlaps, CancellationToken cancellationToken)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var workspace = Path.GetDirectoryName(outputFeatureClassPath);
                    var featureClassName = Path.GetFileNameWithoutExtension(outputFeatureClassPath);

                    if (string.IsNullOrEmpty(workspace) || string.IsNullOrEmpty(featureClassName))
                    {
                        AddLog("无效的输出要素类路径");
                        return;
                    }

                    using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace))))
                    using (var featureClass = geodatabase.OpenDataset<FeatureClass>(featureClassName))
                    {
                        int insertedCount = 0;
                        double tolerance = double.TryParse(Tolerance, out double t) ? t : 0.001;

                        for (int i = 0; i < overlaps.Count; i++)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            try
                            {
                                var overlapInfo = overlaps[i];
                                if (overlapInfo.Geometry is Polygon polygon)
                                {
                                    using (var rowBuffer = featureClass.CreateRowBuffer())
                                    {
                                        // 设置几何
                                        rowBuffer[featureClass.GetDefinition().GetShapeField()] = polygon;

                                        // 设置基本属性
                                        try
                                        {
                                            rowBuffer["OVERLAP_ID"] = i + 1;
                                            rowBuffer["AREA"] = polygon.Area;
                                            rowBuffer["TOLERANCE"] = tolerance;

                                            // 设置保留字段的值
                                            if (SelectedFields != null && SelectedFields.Count > 0)
                                            {
                                                foreach (var fieldName in SelectedFields)
                                                {
                                                    try
                                                    {
                                                        if (overlapInfo.Feature1Fields.ContainsKey(fieldName))
                                                        {
                                                            rowBuffer[fieldName] = overlapInfo.Feature1Fields[fieldName];
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        AddLog($"设置保留字段 {fieldName} 失败: {ex.Message}");
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            AddLog($"设置字段值失败: {ex.Message}");
                                        }

                                        using (var feature = featureClass.CreateRow(rowBuffer))
                                        {
                                            feature.Store();
                                            insertedCount++;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                AddLog($"插入要素 {i + 1} 失败: {ex.Message}");
                            }

                            // 更新进度
                            if (i % 10 == 0)
                            {
                                Progress = (int)((double)(i + 1) / overlaps.Count * 100);
                            }
                        }

                        AddLog($"成功插入 {insertedCount} 个重叠区域要素");
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog($"插入重叠区域要素失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 取消处理
        /// </summary>
        private void CancelProcess()
        {
            _cancellationTokenSource?.Cancel();
            AddLog("正在取消操作...");
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
                                    AddLog($"已选择 {SelectedFields.Count} 个保留字段");
                                }
                            });
                        }
                        else
                        {
                            AddLog("无法获取图层字段信息");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                AddLog($"选择字段时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            var helpText = @"图形重叠检查工具使用说明：

1. 选择面要素图层：选择需要检查重叠的面要素图层
2. 设置容差值：设置重叠检查的容差值（默认0.001米）
3. 选择输出路径：选择重叠区域输出的Shapefile路径
4. 选择保留字段：选择要保留到输出图层的原始字段
5. 点击开始按钮执行重叠检查

工具将检查选中图层中所有面要素之间的重叠情况，
并将重叠区域输出为新的面要素图层。
保留字段的值将以 '值1/值2/值3' 的格式合并。";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpText, "使用说明");
        }
        #endregion


    }
}
