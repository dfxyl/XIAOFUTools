using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO.Compression;
using System.Xml;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Tools.ExportToKml
{
    /// <summary>
    /// 要素图层分组导出KML/KMZ视图模型
    /// </summary>
    internal class ExportToKmlViewModel : PropertyChangedBase
    {
        #region 常量

        private const string NoGroupFieldOption = "不分组（直接导出）";
        private const string LayerToKmlToolName = "conversion.LayerToKML";
        private const string LayerToKmlLegacyToolName = "LayerToKML_conversion";
        private const string SelectLayerByAttributeToolName = "management.SelectLayerByAttribute";
        private const string SelectLayerByAttributeLegacyToolName = "SelectLayerByAttribute_management";
        private const string LabelStyleId = "xft_label_style";

        #endregion

        #region 字段

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isProcessing = false;
        private double _progress = 0;
        private bool _isProgressIndeterminate = false;
        private string _statusMessage = "准备就绪";
        private string _logContent = "";

        private ObservableCollection<Layer> _featureLayers = new ObservableCollection<Layer>();
        private Layer _selectedInputLayer;

        private ObservableCollection<string> _groupFields = new ObservableCollection<string>();
        private string _selectedGroupField;

        private ObservableCollection<string> _exportFormats = new ObservableCollection<string>();
        private string _selectedExportFormat;

        private string _outputFolder = "";

        // 标注相关
        private bool _enableLabel = false;
        private ObservableCollection<string> _labelFields = new ObservableCollection<string>();
        private string _selectedLabelField;

        #endregion

        #region 属性

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
                // 当选择图层改变时，加载字段列表
                LoadGroupFields();
            }
        }

        public ObservableCollection<string> GroupFields
        {
            get => _groupFields;
            set => SetProperty(ref _groupFields, value);
        }

        public string SelectedGroupField
        {
            get => _selectedGroupField;
            set
            {
                SetProperty(ref _selectedGroupField, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        public ObservableCollection<string> ExportFormats
        {
            get => _exportFormats;
            set => SetProperty(ref _exportFormats, value);
        }

        public string SelectedExportFormat
        {
            get => _selectedExportFormat;
            set
            {
                SetProperty(ref _selectedExportFormat, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                SetProperty(ref _outputFolder, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        public bool EnableLabel
        {
            get => _enableLabel;
            set
            {
                SetProperty(ref _enableLabel, value);
                NotifyPropertyChanged(nameof(ShowLabelField));
            }
        }

        public bool ShowLabelField => EnableLabel;

        public ObservableCollection<string> LabelFields
        {
            get => _labelFields;
            set => SetProperty(ref _labelFields, value);
        }

        public string SelectedLabelField
        {
            get => _selectedLabelField;
            set => SetProperty(ref _selectedLabelField, value);
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

        public bool CanProcess => !IsProcessing && 
                                  SelectedInputLayer != null && 
                                  !string.IsNullOrEmpty(OutputFolder) && 
                                  !string.IsNullOrEmpty(SelectedExportFormat) &&
                                  !string.IsNullOrEmpty(SelectedGroupField);

        #endregion

        #region 命令

        public ICommand BrowseOutputFolderCommand { get; }
        public ICommand RunCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand RefreshLayersCommand { get; }

        #endregion

        #region 构造函数

        public ExportToKmlViewModel()
        {
            // 初始化集合
            FeatureLayers = new ObservableCollection<Layer>();
            GroupFields = new ObservableCollection<string>();
            LabelFields = new ObservableCollection<string>();
            ExportFormats = new ObservableCollection<string> { "KML", "KMZ" };

            // 初始化命令
            BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
            RunCommand = new RelayCommand(async () => await RunExportAsync(), () => CanProcess);
            CancelCommand = new RelayCommand(CancelExport, () => IsProcessing);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            RefreshLayersCommand = new RelayCommand(RefreshLayers);

            // 默认选择 KML 格式
            SelectedExportFormat = "KML";

            // 加载图层
            LoadFeatureLayers();
        }

        #endregion

        #region 公共方法

        public void RefreshLayers()
        {
            LoadFeatureLayers();
        }

        #endregion

        #region 私有方法

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
                }

                NotifyPropertyChanged(nameof(FeatureLayers));
                NotifyPropertyChanged(nameof(CanProcess));
            }
            catch (Exception ex)
            {
                AddLog($"加载图层时出错: {ex.Message}");
            }
        }

        private async void LoadGroupFields()
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    GroupFields.Clear();
                    LabelFields.Clear();
                    
                    // 添加"不分组"选项
                    GroupFields.Add(NoGroupFieldOption);
                });

                var featureLayer = SelectedInputLayer as FeatureLayer;
                if (featureLayer == null)
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        SelectedGroupField = NoGroupFieldOption;
                        SelectedLabelField = null;
                    });
                    return;
                }

                await QueuedTask.Run(() =>
                {
                    using var table = featureLayer.GetTable();
                    var tableDefinition = table.GetDefinition();
                    var fields = tableDefinition.GetFields();

                    // 分组字段：仅支持字符串和整数类型
                    var groupFieldNames = fields
                        .Where(f => f.FieldType == FieldType.String || 
                                   f.FieldType == FieldType.Integer || 
                                   f.FieldType == FieldType.SmallInteger ||
                                   f.FieldType == FieldType.BigInteger)
                        .Select(f => f.Name)
                        .ToList();

                    // 标注字段：支持所有非几何/Blob类型
                    var labelFieldNames = fields
                        .Where(f => f.FieldType != FieldType.Geometry && 
                                   f.FieldType != FieldType.Blob &&
                                   f.FieldType != FieldType.Raster &&
                                   f.FieldType != FieldType.OID)
                        .Select(f => f.Name)
                        .ToList();

                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        foreach (var fieldName in groupFieldNames)
                        {
                            GroupFields.Add(fieldName);
                        }
                        
                        foreach (var fieldName in labelFieldNames)
                        {
                            LabelFields.Add(fieldName);
                        }
                        
                        // 默认选择"不分组"
                        SelectedGroupField = NoGroupFieldOption;
                        
                        // 默认选择第一个标注字段
                        if (LabelFields.Count > 0)
                        {
                            SelectedLabelField = LabelFields[0];
                        }
                        
                        // 通知属性更新
                        NotifyPropertyChanged(nameof(GroupFields));
                        NotifyPropertyChanged(nameof(LabelFields));
                        NotifyPropertyChanged(nameof(SelectedLabelField));
                    });
                });
            }
            catch (Exception ex)
            {
                AddLog($"加载字段时出错: {ex.Message}");
            }
        }

        private void BrowseOutputFolder()
        {
            try
            {
                using var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择输出文件夹",
                    ShowNewFolderButton = true
                };

                if (!string.IsNullOrEmpty(OutputFolder) && Directory.Exists(OutputFolder))
                {
                    dialog.SelectedPath = OutputFolder;
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    OutputFolder = dialog.SelectedPath;
                    AddLog($"选择输出文件夹: {OutputFolder}");
                }
            }
            catch (Exception ex)
            {
                AddLog($"选择输出文件夹出错: {ex.Message}");
            }
        }

        private async Task RunExportAsync()
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
                await PerformExport(_cancellationTokenSource.Token);

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusMessage = "导出完成";
                    AddLog("KML/KMZ 导出完成！");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
                AddLog("操作被用户取消");
            }
            catch (Exception ex)
            {
                StatusMessage = $"导出失败: {ex.Message}";
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

        private void CancelExport()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void ShowHelp()
        {
            var helpMessage = "要素图层分组导出KML/KMZ工具使用说明：\n\n" +
                "1. 选择输入图层：从下拉列表中选择需要导出的要素图层\n" +
                "2. 选择分组字段：\n" +
                "   - 选择一个字段按其值进行分组导出\n" +
                "   - 选择[不分组]则将全部要素导出为单个文件\n" +
                "3. 选择导出格式：\n" +
                "   - KML：标准的 Keyhole 标记语言格式(XML格式，文件较大)\n" +
                "   - KMZ：压缩的 KML 格式(文件较小)\n" +
                "4. 选择输出文件夹：导出文件将保存到此文件夹\n" +
                "5. 点击导出按钮开始导出\n\n" +
                "注意：\n" +
                "- 导出调用 ArcGIS Pro 内置 Layer To KML 工具，自动处理坐标转换和符号样式\n" +
                "- 分组导出时，每个分组值将生成一个独立的文件\n" +
                "- 文件名基于图层名称和分组字段值生成\n" +
                "- 支持点、线、面要素导出\n" +
                "- 勾选生成文字标注层后，将按所选标注字段直接生成并合并标注点";

            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(helpMessage, "使用帮助");
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}\n";

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

        private async Task PerformExport(CancellationToken cancellationToken)
        {
            var inputLayer = SelectedInputLayer as FeatureLayer;
            if (inputLayer == null)
            {
                throw new InvalidOperationException("输入图层无效");
            }

            if (!Directory.Exists(OutputFolder))
            {
                Directory.CreateDirectory(OutputFolder);
            }

            AddLog($"开始导出图层: {inputLayer.Name}");
            AddLog($"分组字段: {SelectedGroupField}");
            AddLog($"导出格式: {SelectedExportFormat}");
            AddLog($"输出文件夹: {OutputFolder}");

            if (SelectedExportFormat == "KML")
            {
                AddLog("KML格式将先由内置工具输出为KMZ，再自动提取为KML");
            }

            if (EnableLabel)
            {
                AddLog($"提示: 将按字段 [{SelectedLabelField}] 直接生成标注点");
            }

            var sourceSpatialReferenceName = await QueuedTask.Run(() =>
            {
                using var table = inputLayer.GetTable();
                var featureClass = table as FeatureClass;
                if (featureClass == null)
                {
                    return "未知";
                }

                var sourceSpatialReference = featureClass.GetDefinition().GetSpatialReference();
                return sourceSpatialReference?.Name ?? "未知";
            });

            AddLog($"源坐标系: {sourceSpatialReferenceName}");

            IsProgressIndeterminate = false;

            if (SelectedGroupField == NoGroupFieldOption)
            {
                await ExportAllFeatures(inputLayer, inputLayer.Name, cancellationToken);
            }
            else
            {
                await ExportByGroup(inputLayer, inputLayer.Name, cancellationToken);
            }
        }

        private async Task ExportAllFeatures(FeatureLayer inputLayer, string layerName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = SanitizeFileName(layerName);
            Progress = 20;

            await ExportLayerWithBuiltInToolAsync(inputLayer, fileName, cancellationToken);

            Progress = 100;
            AddLog($"导出完成: {fileName}.{GetOutputExtension()}");
        }

        private async Task ExportByGroup(FeatureLayer inputLayer, string layerName, CancellationToken cancellationToken)
        {
            var groupInfo = await CollectGroupInfoAsync(inputLayer, SelectedGroupField, cancellationToken);
            AddLog($"发现 {groupInfo.Groups.Count} 个分组");

            if (groupInfo.Groups.Count == 0)
            {
                Progress = 100;
                AddLog("没有可导出的分组记录");
                return;
            }

            for (int i = 0; i < groupInfo.Groups.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var group = groupInfo.Groups[i];
                var whereClause = BuildGroupWhereClause(SelectedGroupField, groupInfo.FieldType, group.RawValue);
                var fileName = SanitizeFileName($"{layerName}_{group.DisplayValue}");

                await ExportLayerWithBuiltInToolAsync(inputLayer, fileName, cancellationToken, whereClause);
                AddLog($"导出分组 [{group.DisplayValue}]: {fileName}.{GetOutputExtension()}");

                Progress = (double)(i + 1) / groupInfo.Groups.Count * 100;
            }
        }

        private async Task ExportLayerWithBuiltInToolAsync(
            object layerInput,
            string fileName,
            CancellationToken cancellationToken,
            string whereClause = null)
        {
            if (SelectedExportFormat == "KMZ")
            {
                var kmzPath = Path.Combine(OutputFolder, $"{fileName}.kmz");
                await ExecuteLayerToKmlAsync(layerInput, kmzPath, cancellationToken, whereClause);

                if (ShouldExportLabelsFromField())
                {
                    await AppendFieldLabelLayerToKmzAsync(layerInput, kmzPath, cancellationToken, whereClause);
                }

                return;
            }

            var kmlPath = Path.Combine(OutputFolder, $"{fileName}.kml");
            var tempKmzPath = Path.Combine(Path.GetTempPath(), $"xft_export_{Guid.NewGuid():N}.kmz");

            try
            {
                await ExecuteLayerToKmlAsync(layerInput, tempKmzPath, cancellationToken, whereClause);

                if (ShouldExportLabelsFromField())
                {
                    await AppendFieldLabelLayerToKmzAsync(layerInput, tempKmzPath, cancellationToken, whereClause);
                }

                await ExtractKmlFromKmzAsync(tempKmzPath, kmlPath, cancellationToken);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempKmzPath))
                    {
                        File.Delete(tempKmzPath);
                    }
                }
                catch
                {
                    // 临时文件删除失败不影响主流程
                }
            }
        }

        private string GetOutputExtension()
        {
            return SelectedExportFormat == "KMZ" ? "kmz" : "kml";
        }

        private bool ShouldExportLabelsFromField()
        {
            return EnableLabel && !string.IsNullOrWhiteSpace(SelectedLabelField);
        }

        private async Task ExecuteLayerToKmlAsync(
            object layerInput,
            string outputKmzPath,
            CancellationToken cancellationToken,
            string whereClause = null)
        {
            var env = Geoprocessing.MakeEnvironmentArray("overwriteoutput", "True", "addOutputsToMap", "False");
            var primaryParams = Geoprocessing.MakeValueArray(layerInput, outputKmzPath);

            var featureLayer = layerInput as FeatureLayer;
            if (featureLayer != null && !string.IsNullOrWhiteSpace(whereClause))
            {
                await SelectSourceLayerByWhereAsync(featureLayer, whereClause, cancellationToken);
            }

            try
            {
                var primaryResult = await Geoprocessing.ExecuteToolAsync(
                    LayerToKmlToolName,
                    primaryParams,
                    env,
                    cancellationToken,
                    null,
                    GPExecuteToolFlags.None);

                if (primaryResult != null && !primaryResult.IsFailed)
                {
                    return;
                }

                AddLog($"工具 {LayerToKmlToolName} 调用失败，尝试兼容名称 {LayerToKmlLegacyToolName}");

                var fallbackParams = Geoprocessing.MakeValueArray(layerInput, outputKmzPath);
                var fallbackResult = await Geoprocessing.ExecuteToolAsync(
                    LayerToKmlLegacyToolName,
                    fallbackParams,
                    env,
                    cancellationToken,
                    null,
                    GPExecuteToolFlags.None);

                if (fallbackResult == null || fallbackResult.IsFailed)
                {
                    var message = BuildGpErrorMessage(fallbackResult ?? primaryResult);
                    throw new InvalidOperationException($"内置Layer To KML执行失败: {message}");
                }
            }
            finally
            {
                if (featureLayer != null && !string.IsNullOrWhiteSpace(whereClause))
                {
                    await ClearSourceLayerSelectionAsync(featureLayer);
                }
            }
        }

        private async Task SelectSourceLayerByWhereAsync(FeatureLayer featureLayer, string whereClause, CancellationToken cancellationToken)
        {
            var env = Geoprocessing.MakeEnvironmentArray("addOutputsToMap", "False");
            var primaryParams = Geoprocessing.MakeValueArray(featureLayer, "NEW_SELECTION", whereClause);

            var primaryResult = await Geoprocessing.ExecuteToolAsync(
                SelectLayerByAttributeToolName,
                primaryParams,
                env,
                cancellationToken,
                null,
                GPExecuteToolFlags.None);

            if (primaryResult != null && !primaryResult.IsFailed)
            {
                return;
            }

            var fallbackParams = Geoprocessing.MakeValueArray(featureLayer, "NEW_SELECTION", whereClause);
            var fallbackResult = await Geoprocessing.ExecuteToolAsync(
                SelectLayerByAttributeLegacyToolName,
                fallbackParams,
                env,
                cancellationToken,
                null,
                GPExecuteToolFlags.None);

            if (fallbackResult == null || fallbackResult.IsFailed)
            {
                var message = BuildGpErrorMessage(fallbackResult ?? primaryResult);
                throw new InvalidOperationException($"按分组筛选源图层失败: {message}");
            }
        }

        private async Task ClearSourceLayerSelectionAsync(FeatureLayer featureLayer)
        {
            if (featureLayer == null)
            {
                return;
            }

            try
            {
                await QueuedTask.Run(() => featureLayer.ClearSelection());
            }
            catch
            {
                // 清理选择失败不影响主流程
            }
        }

        private async Task ExtractKmlFromKmzAsync(string kmzPath, string kmlPath, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var archive = ZipFile.OpenRead(kmzPath);
                var docEntry = archive.GetEntry("doc.kml");
                if (docEntry == null)
                {
                    throw new InvalidOperationException("KMZ中未找到doc.kml");
                }

                using var kmlStream = docEntry.Open();
                using var outputStream = new FileStream(kmlPath, FileMode.Create, FileAccess.Write, FileShare.None);
                kmlStream.CopyTo(outputStream);
            }, cancellationToken);
        }

        private async Task AppendFieldLabelLayerToKmzAsync(
            object sourceLayerInput,
            string targetKmzPath,
            CancellationToken cancellationToken,
            string whereClause = null)
        {
            var labelItems = await BuildLabelItemsAsync(sourceLayerInput, SelectedLabelField, cancellationToken, whereClause);
            if (labelItems.Count == 0)
            {
                AddLog($"字段 [{SelectedLabelField}] 没有可用标注，已跳过标注层");
                return;
            }

            var targetKml = await ReadDocKmlFromKmzAsync(targetKmzPath, cancellationToken);
            var mergedKml = MergeKmlWithLabelItems(targetKml, labelItems, SelectedLabelField);
            await WriteDocKmlToKmzAsync(targetKmzPath, mergedKml, cancellationToken);

            AddLog($"已按字段 [{SelectedLabelField}] 追加 {labelItems.Count} 个标注点");
        }

        private async Task<List<KmlLabelItem>> BuildLabelItemsAsync(
            object sourceLayerInput,
            string labelFieldName,
            CancellationToken cancellationToken,
            string whereClause = null)
        {
            return await QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var featureLayer = ResolveFeatureLayer(sourceLayerInput);
                if (featureLayer == null)
                {
                    return new List<KmlLabelItem>();
                }

                using var table = featureLayer.GetTable();
                var definition = table.GetDefinition();
                var labelField = definition.GetFields().FirstOrDefault(f => f.Name == labelFieldName);
                if (labelField == null)
                {
                    throw new InvalidOperationException($"标注字段不存在: {labelFieldName}");
                }

                if (table is not FeatureClass featureClass)
                {
                    return new List<KmlLabelItem>();
                }

                var wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);
                var items = new List<KmlLabelItem>();

                var queryFilter = string.IsNullOrWhiteSpace(whereClause)
                    ? null
                    : new QueryFilter { WhereClause = whereClause };
                using var cursor = queryFilter == null ? featureClass.Search() : featureClass.Search(queryFilter);
                while (cursor.MoveNext())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var row = cursor.Current;
                    if (row is not Feature feature)
                    {
                        continue;
                    }

                    var rawLabel = row[labelFieldName];
                    var labelText = rawLabel?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(labelText))
                    {
                        continue;
                    }

                    var labelPoint = GetGeometryLabelPoint(feature.GetShape());
                    if (labelPoint == null || labelPoint.IsEmpty)
                    {
                        continue;
                    }

                    var wgs84Point = labelPoint;
                    if (wgs84Point.SpatialReference == null || wgs84Point.SpatialReference.Wkid != 4326)
                    {
                        wgs84Point = GeometryEngine.Instance.Project(wgs84Point, wgs84) as MapPoint;
                    }

                    if (wgs84Point == null || wgs84Point.IsEmpty)
                    {
                        continue;
                    }

                    items.Add(new KmlLabelItem
                    {
                        Text = labelText,
                        X = wgs84Point.X,
                        Y = wgs84Point.Y
                    });
                }

                return items;
            });
        }

        private static FeatureLayer ResolveFeatureLayer(object input)
        {
            if (input is FeatureLayer featureLayer)
            {
                return featureLayer;
            }

            return input as Layer as FeatureLayer;
        }

        private static MapPoint GetGeometryLabelPoint(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty)
            {
                return null;
            }

            switch (geometry)
            {
                case MapPoint point:
                    return point;

                case Multipoint multipoint when multipoint.PointCount > 0:
                    return multipoint.Points[0];

                case Polygon polygon:
                    {
                        var centroid = GeometryEngine.Instance.Centroid(polygon) as MapPoint;
                        if (centroid != null && !centroid.IsEmpty && GeometryEngine.Instance.Contains(polygon, centroid))
                        {
                            return centroid;
                        }

                        try
                        {
                            var labelPoint = GeometryEngine.Instance.LabelPoint(polygon);
                            if (labelPoint != null && !labelPoint.IsEmpty)
                            {
                                return labelPoint;
                            }
                        }
                        catch
                        {
                            // 忽略异常，使用兜底点
                        }

                        return centroid ?? polygon.Extent?.Center;
                    }

                case Polyline polyline:
                    return polyline.Extent?.Center;

                default:
                    return geometry.Extent?.Center;
            }
        }

        private async Task<string> ReadDocKmlFromKmzAsync(string kmzPath, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var archive = ZipFile.OpenRead(kmzPath);
                var docEntry = archive.GetEntry("doc.kml");
                if (docEntry == null)
                {
                    throw new InvalidOperationException($"KMZ中未找到doc.kml: {Path.GetFileName(kmzPath)}");
                }

                using var stream = docEntry.Open();
                using var reader = new StreamReader(stream, Encoding.UTF8, true);
                return reader.ReadToEnd();
            }, cancellationToken);
        }

        private async Task WriteDocKmlToKmzAsync(string kmzPath, string kmlContent, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var archive = ZipFile.Open(kmzPath, ZipArchiveMode.Update);
                var existingEntry = archive.GetEntry("doc.kml");
                existingEntry?.Delete();

                var newEntry = archive.CreateEntry("doc.kml");
                using var entryStream = newEntry.Open();
                using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
                writer.Write(kmlContent);
            }, cancellationToken);
        }

        private static string MergeKmlWithLabelItems(string targetKml, IReadOnlyList<KmlLabelItem> labelItems, string labelFieldName)
        {
            const string kmlNs = "http://www.opengis.net/kml/2.2";

            var targetDoc = new XmlDocument();
            targetDoc.LoadXml(targetKml);

            var targetNsManager = new XmlNamespaceManager(targetDoc.NameTable);
            targetNsManager.AddNamespace("kml", kmlNs);

            var targetDocument = targetDoc.SelectSingleNode("/kml:kml/kml:Document", targetNsManager) as XmlElement;
            if (targetDocument == null)
            {
                return targetKml;
            }

            if (labelItems == null || labelItems.Count == 0)
            {
                return targetKml;
            }

            UpdatePlacemarkNamesFromField(targetDoc, targetNsManager, kmlNs, labelFieldName);

            EnsureLabelStyle(targetDoc, targetDocument, targetNsManager, kmlNs);

            var labelFolder = targetDoc.CreateElement("Folder", kmlNs);
            var nameElement = targetDoc.CreateElement("name", kmlNs);
            nameElement.InnerText = "标注";
            labelFolder.AppendChild(nameElement);

            foreach (var item in labelItems)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Text))
                {
                    continue;
                }

                var placemarkElement = targetDoc.CreateElement("Placemark", kmlNs);

                var placemarkName = targetDoc.CreateElement("name", kmlNs);
                placemarkName.InnerText = item.Text;
                placemarkElement.AppendChild(placemarkName);

                var styleUrl = targetDoc.CreateElement("styleUrl", kmlNs);
                styleUrl.InnerText = $"#{LabelStyleId}";
                placemarkElement.AppendChild(styleUrl);

                var pointElement = targetDoc.CreateElement("Point", kmlNs);
                var coordinateElement = targetDoc.CreateElement("coordinates", kmlNs);
                coordinateElement.InnerText =
                    $"{item.X.ToString(CultureInfo.InvariantCulture)},{item.Y.ToString(CultureInfo.InvariantCulture)},0";
                pointElement.AppendChild(coordinateElement);
                placemarkElement.AppendChild(pointElement);

                labelFolder.AppendChild(placemarkElement);
            }

            if (labelFolder.ChildNodes.Count > 1)
            {
                targetDocument.AppendChild(labelFolder);
            }

            return targetDoc.OuterXml;
        }

        private static void UpdatePlacemarkNamesFromField(
            XmlDocument doc,
            XmlNamespaceManager nsManager,
            string kmlNs,
            string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return;
            }

            var placemarks = doc.SelectNodes("//kml:Placemark", nsManager);
            if (placemarks == null || placemarks.Count == 0)
            {
                return;
            }

            foreach (XmlNode node in placemarks)
            {
                if (node is not XmlElement placemark)
                {
                    continue;
                }

                var value = ExtractFieldValueFromPlacemark(placemark, fieldName, kmlNs);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var nameElement = placemark["name", kmlNs];
                if (nameElement == null)
                {
                    nameElement = doc.CreateElement("name", kmlNs);
                    if (placemark.HasChildNodes)
                    {
                        placemark.InsertBefore(nameElement, placemark.FirstChild);
                    }
                    else
                    {
                        placemark.AppendChild(nameElement);
                    }
                }

                nameElement.InnerText = value;
            }
        }

        private static string ExtractFieldValueFromPlacemark(XmlElement placemark, string fieldName, string kmlNs)
        {
            var dataNodes = placemark.GetElementsByTagName("Data", kmlNs);
            for (int i = 0; i < dataNodes.Count; i++)
            {
                if (dataNodes[i] is not XmlElement dataElement)
                {
                    continue;
                }

                var nameAttr = dataElement.GetAttribute("name");
                if (!string.Equals(nameAttr, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var valueElement = dataElement["value", kmlNs];
                if (valueElement != null)
                {
                    return valueElement.InnerText;
                }
            }

            var simpleDataNodes = placemark.GetElementsByTagName("SimpleData", kmlNs);
            for (int i = 0; i < simpleDataNodes.Count; i++)
            {
                if (simpleDataNodes[i] is not XmlElement simpleDataElement)
                {
                    continue;
                }

                var nameAttr = simpleDataElement.GetAttribute("name");
                if (string.Equals(nameAttr, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return simpleDataElement.InnerText;
                }
            }

            return string.Empty;
        }

        private static void EnsureLabelStyle(XmlDocument targetDoc, XmlElement targetDocument, XmlNamespaceManager nsManager, string kmlNs)
        {
            var existing = targetDocument.SelectSingleNode($"kml:Style[@id='{LabelStyleId}']", nsManager);
            if (existing != null)
            {
                return;
            }

            var styleElement = targetDoc.CreateElement("Style", kmlNs);
            var idAttribute = targetDoc.CreateAttribute("id");
            idAttribute.Value = LabelStyleId;
            styleElement.Attributes.Append(idAttribute);

            var iconStyle = targetDoc.CreateElement("IconStyle", kmlNs);
            var iconScale = targetDoc.CreateElement("scale", kmlNs);
            iconScale.InnerText = "0";
            iconStyle.AppendChild(iconScale);

            var labelStyle = targetDoc.CreateElement("LabelStyle", kmlNs);
            var labelColor = targetDoc.CreateElement("color", kmlNs);
            labelColor.InnerText = "FFFFFFFF";
            var labelScale = targetDoc.CreateElement("scale", kmlNs);
            labelScale.InnerText = "1";
            labelStyle.AppendChild(labelColor);
            labelStyle.AppendChild(labelScale);

            styleElement.AppendChild(iconStyle);
            styleElement.AppendChild(labelStyle);
            targetDocument.AppendChild(styleElement);
        }

        private async Task<(FieldType FieldType, List<GroupExportItem> Groups)> CollectGroupInfoAsync(
            FeatureLayer inputLayer,
            string groupField,
            CancellationToken cancellationToken)
        {
            return await QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var table = inputLayer.GetTable();
                var definition = table.GetDefinition();
                var field = definition.GetFields().FirstOrDefault(f => f.Name == groupField);
                if (field == null)
                {
                    throw new InvalidOperationException($"分组字段不存在: {groupField}");
                }

                var uniqueGroups = new Dictionary<string, GroupExportItem>(StringComparer.Ordinal);

                using var cursor = table.Search();
                while (cursor.MoveNext())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var row = cursor.Current;
                    var rawValue = row[groupField];
                    var isNull = rawValue == null || rawValue == DBNull.Value;
                    var displayValue = isNull
                        ? "空值"
                        : (rawValue.ToString() ?? string.Empty);

                    if (string.IsNullOrWhiteSpace(displayValue))
                    {
                        displayValue = "空字符串";
                    }

                    var key = isNull ? "__NULL__" : $"VALUE::{rawValue}";
                    if (!uniqueGroups.ContainsKey(key))
                    {
                        uniqueGroups[key] = new GroupExportItem
                        {
                            RawValue = isNull ? null : rawValue,
                            DisplayValue = displayValue
                        };
                    }
                }

                var orderedGroups = uniqueGroups.Values
                    .OrderBy(g => g.DisplayValue)
                    .ToList();

                return (field.FieldType, orderedGroups);
            });
        }

        private string BuildGroupWhereClause(string fieldName, FieldType fieldType, object rawValue)
        {
            var escapedFieldName = $"\"{fieldName.Replace("\"", "\"\"")}\"";
            if (rawValue == null)
            {
                return $"{escapedFieldName} IS NULL";
            }

            switch (fieldType)
            {
                case FieldType.String:
                    return $"{escapedFieldName} = '{EscapeSqlValue(rawValue.ToString())}'";

                case FieldType.Integer:
                case FieldType.SmallInteger:
                case FieldType.BigInteger:
                    return $"{escapedFieldName} = {Convert.ToString(rawValue, CultureInfo.InvariantCulture)}";

                default:
                    return $"{escapedFieldName} = '{EscapeSqlValue(rawValue.ToString())}'";
            }
        }

        private static string EscapeSqlValue(string input)
        {
            return string.IsNullOrEmpty(input) ? string.Empty : input.Replace("'", "''");
        }

        private static string BuildGpErrorMessage(IGPResult result)
        {
            if (result?.Messages == null)
            {
                return "未返回详细错误信息";
            }

            var messages = result.Messages
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Text))
                .Select(m => m.Text)
                .ToList();

            return messages.Count == 0 ? "未返回详细错误信息" : string.Join(" | ", messages);
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            return string.IsNullOrEmpty(sanitized) ? "export" : sanitized;
        }

        #endregion
    }

    /// <summary>
    /// 分组导出项
    /// </summary>
    internal class GroupExportItem
    {
        public object RawValue { get; set; }
        public string DisplayValue { get; set; }
    }

    /// <summary>
    /// KML标注点项
    /// </summary>
    internal class KmlLabelItem
    {
        public string Text { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
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
