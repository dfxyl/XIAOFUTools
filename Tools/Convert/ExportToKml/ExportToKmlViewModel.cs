using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml;
using System.IO.Compression;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
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

        // 图层样式缓存
        private KmlStyle _layerStyle;

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

        private void LoadFeatureLayers()
        {
            try
            {
                FeatureLayers.Clear();

                if (MapView.Active?.Map == null)
                {
                    AddLog("当前没有活动地图");
                    return;
                }

                var layers = MapView.Active.Map.GetLayersAsFlattenedList().OfType<FeatureLayer>();

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
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await QueuedTask.Run(async () =>
                {
                    await PerformExport(_cancellationTokenSource.Token);
                });

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
                "- 坐标将自动转换为 WGS84 经纬度坐标\n" +
                "- 分组导出时，每个分组值将生成一个独立的文件\n" +
                "- 文件名基于图层名称和分组字段值生成\n" +
                "- 支持点、线、面要素导出";

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

            AddLog($"开始导出图层: {inputLayer.Name}");
            AddLog($"分组字段: {SelectedGroupField}");
            AddLog($"导出格式: {SelectedExportFormat}");
            AddLog($"输出文件夹: {OutputFolder}");

            // 读取图层符号化样式
            _layerStyle = GetLayerStyle(inputLayer);
            if (_layerStyle != null)
            {
                AddLog("已读取图层符号化样式");
            }

            // 获取输入要素类
            using var table = inputLayer.GetTable();
            var featureClass = table as FeatureClass;
            if (featureClass == null)
            {
                throw new InvalidOperationException("无法获取要素类");
            }

            // 获取空间参考，用于转换到 WGS84
            var featureClassDefinition = featureClass.GetDefinition();
            var sourceSpatialReference = featureClassDefinition.GetSpatialReference();
            var wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);

            AddLog($"源坐标系: {sourceSpatialReference.Name}");

            IsProgressIndeterminate = false;

            if (SelectedGroupField == NoGroupFieldOption)
            {
                // 不分组，直接导出全部
                await ExportAllFeatures(featureClass, inputLayer.Name, wgs84, cancellationToken);
            }
            else
            {
                // 按分组字段导出
                await ExportByGroup(featureClass, inputLayer.Name, wgs84, cancellationToken);
            }
        }

        /// <summary>
        /// 读取图层的符号化样式
        /// </summary>
        private KmlStyle GetLayerStyle(FeatureLayer featureLayer)
        {
            try
            {
                var renderer = featureLayer.GetRenderer();
                if (renderer == null) return GetDefaultStyle();

                // 简单渲染器
                if (renderer is CIMSimpleRenderer simpleRenderer)
                {
                    return GetStyleFromSymbol(simpleRenderer.Symbol?.Symbol);
                }

                // 唯一值渲染器 - 取第一个符号
                if (renderer is CIMUniqueValueRenderer uniqueRenderer)
                {
                    if (uniqueRenderer.DefaultSymbol?.Symbol != null)
                    {
                        return GetStyleFromSymbol(uniqueRenderer.DefaultSymbol.Symbol);
                    }
                    if (uniqueRenderer.Groups?.Length > 0 && uniqueRenderer.Groups[0].Classes?.Length > 0)
                    {
                        return GetStyleFromSymbol(uniqueRenderer.Groups[0].Classes[0].Symbol?.Symbol);
                    }
                }

                // 分级渲染器 - 取第一个符号
                if (renderer is CIMClassBreaksRenderer classBreaksRenderer)
                {
                    if (classBreaksRenderer.Breaks?.Length > 0)
                    {
                        return GetStyleFromSymbol(classBreaksRenderer.Breaks[0].Symbol?.Symbol);
                    }
                }

                return GetDefaultStyle();
            }
            catch (Exception ex)
            {
                AddLog($"读取样式时出错: {ex.Message}");
                return GetDefaultStyle();
            }
        }

        /// <summary>
        /// 从符号中提取样式
        /// </summary>
        private KmlStyle GetStyleFromSymbol(CIMSymbol symbol)
        {
            var style = new KmlStyle();

            if (symbol == null) return GetDefaultStyle();

            // 点符号
            if (symbol is CIMPointSymbol pointSymbol)
            {
                var layer = pointSymbol.SymbolLayers?.FirstOrDefault();
                if (layer is CIMVectorMarker vectorMarker)
                {
                    var graphic = vectorMarker.MarkerGraphics?.FirstOrDefault();
                    if (graphic?.Symbol is CIMPolygonSymbol markerPolygon)
                    {
                        var fillLayer = markerPolygon.SymbolLayers?.OfType<CIMSolidFill>().FirstOrDefault();
                        if (fillLayer != null)
                        {
                            style.IconColor = ColorToKmlColor(fillLayer.Color);
                        }
                    }
                }
                else if (layer is CIMCharacterMarker charMarker)
                {
                    style.IconColor = ColorToKmlColor(charMarker.Symbol?.SymbolLayers?.OfType<CIMSolidFill>().FirstOrDefault()?.Color);
                }
                style.IconScale = pointSymbol.SymbolLayers?.OfType<CIMMarker>().FirstOrDefault()?.Size / 10.0 ?? 1.0;
            }

            // 线符号
            if (symbol is CIMLineSymbol lineSymbol)
            {
                var strokeLayer = lineSymbol.SymbolLayers?.OfType<CIMSolidStroke>().FirstOrDefault();
                if (strokeLayer != null)
                {
                    style.LineColor = ColorToKmlColor(strokeLayer.Color);
                    style.LineWidth = strokeLayer.Width;
                }
            }

            // 面符号
            if (symbol is CIMPolygonSymbol polygonSymbol)
            {
                var fillLayer = polygonSymbol.SymbolLayers?.OfType<CIMSolidFill>().FirstOrDefault();
                if (fillLayer != null)
                {
                    style.FillColor = ColorToKmlColor(fillLayer.Color);
                }

                var strokeLayer = polygonSymbol.SymbolLayers?.OfType<CIMSolidStroke>().FirstOrDefault();
                if (strokeLayer != null)
                {
                    style.LineColor = ColorToKmlColor(strokeLayer.Color);
                    style.LineWidth = strokeLayer.Width;
                }
            }

            // 如果没有获取到任何颜色，使用默认值
            if (string.IsNullOrEmpty(style.FillColor) && string.IsNullOrEmpty(style.LineColor) && string.IsNullOrEmpty(style.IconColor))
            {
                return GetDefaultStyle();
            }

            return style;
        }

        /// <summary>
        /// 将 CIMColor 转换为 KML 颜色格式 (AABBGGRR)
        /// </summary>
        private string ColorToKmlColor(CIMColor color)
        {
            if (color == null) return null;

            byte r = 255, g = 255, b = 255, a = 255;

            if (color is CIMRGBColor rgbColor)
            {
                r = (byte)Math.Min(255, Math.Max(0, rgbColor.R));
                g = (byte)Math.Min(255, Math.Max(0, rgbColor.G));
                b = (byte)Math.Min(255, Math.Max(0, rgbColor.B));
                a = (byte)Math.Min(255, Math.Max(0, (rgbColor.Alpha / 100.0) * 255));
            }
            else if (color is CIMHSVColor hsvColor)
            {
                // 转换 HSV 到 RGB
                HsvToRgb(hsvColor.H, hsvColor.S, hsvColor.V, out r, out g, out b);
                a = (byte)Math.Min(255, Math.Max(0, (hsvColor.Alpha / 100.0) * 255));
            }
            else if (color is CIMCMYKColor cmykColor)
            {
                // 转换 CMYK 到 RGB
                CmykToRgb(cmykColor.C, cmykColor.M, cmykColor.Y, cmykColor.K, out r, out g, out b);
                a = (byte)Math.Min(255, Math.Max(0, (cmykColor.Alpha / 100.0) * 255));
            }

            // KML 颜色格式: AABBGGRR
            return $"{a:X2}{b:X2}{g:X2}{r:X2}";
        }

        private void HsvToRgb(double h, double s, double v, out byte r, out byte g, out byte b)
        {
            double hh = h / 60.0;
            int i = (int)hh;
            double ff = hh - i;
            double p = v * (1.0 - s / 100.0);
            double q = v * (1.0 - (s / 100.0) * ff);
            double t = v * (1.0 - (s / 100.0) * (1.0 - ff));

            double rr, gg, bb;
            switch (i)
            {
                case 0: rr = v; gg = t; bb = p; break;
                case 1: rr = q; gg = v; bb = p; break;
                case 2: rr = p; gg = v; bb = t; break;
                case 3: rr = p; gg = q; bb = v; break;
                case 4: rr = t; gg = p; bb = v; break;
                default: rr = v; gg = p; bb = q; break;
            }

            r = (byte)(rr * 255 / 100);
            g = (byte)(gg * 255 / 100);
            b = (byte)(bb * 255 / 100);
        }

        private void CmykToRgb(double c, double m, double y, double k, out byte r, out byte g, out byte b)
        {
            r = (byte)(255 * (1 - c / 100) * (1 - k / 100));
            g = (byte)(255 * (1 - m / 100) * (1 - k / 100));
            b = (byte)(255 * (1 - y / 100) * (1 - k / 100));
        }

        /// <summary>
        /// 获取默认样式
        /// </summary>
        private KmlStyle GetDefaultStyle()
        {
            return new KmlStyle
            {
                FillColor = "7F0000FF",  // 半透明红色
                LineColor = "FF0000FF",  // 红色
                LineWidth = 1.5,
                IconColor = "FF0000FF",
                IconScale = 1.0
            };
        }

        private async Task ExportAllFeatures(FeatureClass featureClass, string layerName, SpatialReference wgs84, CancellationToken cancellationToken)
        {
            var features = new List<KmlFeature>();
            var totalCount = featureClass.GetCount();
            var processedCount = 0;

            using var cursor = featureClass.Search();
            while (cursor.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (cursor.Current is Feature feature)
                {
                    var kmlFeature = CreateKmlFeature(feature, wgs84);
                    if (kmlFeature != null)
                    {
                        features.Add(kmlFeature);
                    }

                    processedCount++;
                    Progress = (double)processedCount / totalCount * 100;
                }
            }

            // 导出到文件
            var fileName = SanitizeFileName(layerName);
            await ExportToFile(features, fileName, cancellationToken);
            AddLog($"导出 {features.Count} 个要素到 {fileName}");
        }

        private async Task ExportByGroup(FeatureClass featureClass, string layerName, SpatialReference wgs84, CancellationToken cancellationToken)
        {
            // 首先获取所有分组值
            var groupValues = new Dictionary<string, List<KmlFeature>>();
            var totalCount = featureClass.GetCount();
            var processedCount = 0;

            using var cursor = featureClass.Search();
            while (cursor.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (cursor.Current is Feature feature)
                {
                    var groupValue = feature[SelectedGroupField]?.ToString() ?? "未知";
                    
                    if (!groupValues.ContainsKey(groupValue))
                    {
                        groupValues[groupValue] = new List<KmlFeature>();
                    }

                    var kmlFeature = CreateKmlFeature(feature, wgs84);
                    if (kmlFeature != null)
                    {
                        groupValues[groupValue].Add(kmlFeature);
                    }

                    processedCount++;
                    Progress = (double)processedCount / totalCount * 50; // 前50%用于读取
                }
            }

            AddLog($"发现 {groupValues.Count} 个分组");

            // 导出每个分组
            var groupIndex = 0;
            foreach (var group in groupValues)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = SanitizeFileName($"{layerName}_{group.Key}");
                await ExportToFile(group.Value, fileName, cancellationToken);
                AddLog($"导出分组 [{group.Key}]: {group.Value.Count} 个要素");

                groupIndex++;
                Progress = 50 + (double)groupIndex / groupValues.Count * 50; // 后50%用于导出
            }
        }

        private KmlFeature CreateKmlFeature(Feature feature, SpatialReference wgs84)
        {
            try
            {
                var geometry = feature.GetShape();
                if (geometry == null || geometry.IsEmpty) return null;

                // 投影到 WGS84
                var projectedGeometry = GeometryEngine.Instance.Project(geometry, wgs84);

                // 确定要素名称：如果启用标注且选择了字段，使用字段值；否则使用ObjectID
                string featureName = feature.GetObjectID().ToString();
                if (EnableLabel && !string.IsNullOrEmpty(SelectedLabelField))
                {
                    try
                    {
                        var labelValue = feature[SelectedLabelField];
                        if (labelValue != null)
                        {
                            featureName = labelValue.ToString();
                        }
                    }
                    catch { }
                }

                var kmlFeature = new KmlFeature
                {
                    Name = featureName,
                    Geometry = projectedGeometry,
                    Style = _layerStyle ?? GetDefaultStyle()
                };

                // 获取属性
                var featureClass = feature.GetTable() as FeatureClass;
                if (featureClass != null)
                {
                    var definition = featureClass.GetDefinition();
                    var fields = definition.GetFields();

                    foreach (var field in fields)
                    {
                        if (field.FieldType != FieldType.Geometry && field.FieldType != FieldType.Blob)
                        {
                            try
                            {
                                var value = feature[field.Name];
                                kmlFeature.Attributes[field.Name] = value?.ToString() ?? "";
                            }
                            catch { }
                        }
                    }
                }

                return kmlFeature;
            }
            catch
            {
                return null;
            }
        }

        private async Task ExportToFile(List<KmlFeature> features, string fileName, CancellationToken cancellationToken)
        {
            var kmlContent = GenerateKmlContent(features, fileName);

            if (SelectedExportFormat == "KMZ")
            {
                var kmzPath = Path.Combine(OutputFolder, $"{fileName}.kmz");
                await WriteKmzFile(kmzPath, kmlContent, cancellationToken);
            }
            else
            {
                var kmlPath = Path.Combine(OutputFolder, $"{fileName}.kml");
                await File.WriteAllTextAsync(kmlPath, kmlContent, Encoding.UTF8, cancellationToken);
            }
        }

        private string GenerateKmlContent(List<KmlFeature> features, string documentName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<kml xmlns=\"http://www.opengis.net/kml/2.2\">");
            sb.AppendLine("  <Document>");
            sb.AppendLine($"    <name>{EscapeXml(documentName)}</name>");

            // 生成共享样式定义
            var style = _layerStyle ?? GetDefaultStyle();
            
            // 默认样式（不带标注）
            sb.AppendLine("    <Style id=\"defaultStyle\">");
            
            // 图标样式（点）
            sb.AppendLine("      <IconStyle>");
            sb.AppendLine($"        <color>{style.IconColor ?? "FF0000FF"}</color>");
            sb.AppendLine($"        <scale>{style.IconScale}</scale>");
            sb.AppendLine("        <Icon>");
            sb.AppendLine("          <href>http://maps.google.com/mapfiles/kml/shapes/placemark_circle.png</href>");
            sb.AppendLine("        </Icon>");
            sb.AppendLine("      </IconStyle>");
            
            // 线样式
            sb.AppendLine("      <LineStyle>");
            sb.AppendLine($"        <color>{style.LineColor ?? "FF0000FF"}</color>");
            sb.AppendLine($"        <width>{style.LineWidth}</width>");
            sb.AppendLine("      </LineStyle>");
            
            // 面样式
            sb.AppendLine("      <PolyStyle>");
            sb.AppendLine($"        <color>{style.FillColor ?? "7F0000FF"}</color>");
            sb.AppendLine("        <outline>1</outline>");
            sb.AppendLine("      </PolyStyle>");
            
            // 标注样式（默认不显示）
            sb.AppendLine("      <LabelStyle>");
            sb.AppendLine("        <scale>0</scale>");
            sb.AppendLine("      </LabelStyle>");
            
            sb.AppendLine("    </Style>");

            // 带标注的样式（纯白色文字）
            if (EnableLabel)
            {
                sb.AppendLine("    <Style id=\"labelStyle\">");
                
                // 图标样式（点）- 隐藏
                sb.AppendLine("      <IconStyle>");
                sb.AppendLine("        <scale>0</scale>");
                sb.AppendLine("      </IconStyle>");
                
                // 标注样式 - 白色文字，KML格式为 AABBGGRR
                sb.AppendLine("      <LabelStyle>");
                sb.AppendLine("        <color>FFFFFFFF</color>");  // 白色
                sb.AppendLine("        <scale>1.0</scale>");
                sb.AppendLine("      </LabelStyle>");
                
                sb.AppendLine("    </Style>");
            }

            foreach (var feature in features)
            {
                sb.AppendLine("    <Placemark>");
                sb.AppendLine($"      <name>{EscapeXml(feature.Name)}</name>");
                sb.AppendLine("      <styleUrl>#defaultStyle</styleUrl>");

                // 添加扩展数据（属性）
                if (feature.Attributes.Count > 0)
                {
                    sb.AppendLine("      <ExtendedData>");
                    foreach (var attr in feature.Attributes)
                    {
                        sb.AppendLine($"        <Data name=\"{EscapeXml(attr.Key)}\">");
                        sb.AppendLine($"          <value>{EscapeXml(attr.Value)}</value>");
                        sb.AppendLine("        </Data>");
                    }
                    sb.AppendLine("      </ExtendedData>");
                }

                // 添加几何
                var geometryKml = GeometryToKml(feature.Geometry);
                if (!string.IsNullOrEmpty(geometryKml))
                {
                    sb.AppendLine(geometryKml);
                }

                sb.AppendLine("    </Placemark>");
            }

            // 如果启用标注，生成独立的标注层（白色文字）
            if (EnableLabel && !string.IsNullOrEmpty(SelectedLabelField))
            {
                sb.AppendLine("    <!-- 文字标注层 -->");
                sb.AppendLine($"    <Folder>");
                sb.AppendLine($"      <name>标注</name>");
                
                foreach (var feature in features)
                {
                    // 获取标注值
                    var labelValue = "";
                    if (feature.Attributes.TryGetValue(SelectedLabelField, out var value))
                    {
                        labelValue = value;
                    }
                    
                    if (string.IsNullOrEmpty(labelValue)) continue;

                    // 获取几何中心点
                    var centerPoint = GetGeometryCenter(feature.Geometry);
                    if (centerPoint == null) continue;

                    // 生成白色文字标注
                    sb.AppendLine("      <Placemark>");
                    sb.AppendLine($"        <name>{EscapeXml(labelValue)}</name>");
                    sb.AppendLine("        <styleUrl>#labelStyle</styleUrl>");
                    sb.AppendLine("        <Point>");
                    sb.AppendLine($"          <coordinates>{centerPoint.X},{centerPoint.Y},0</coordinates>");
                    sb.AppendLine("        </Point>");
                    sb.AppendLine("      </Placemark>");
                }
                
                sb.AppendLine("    </Folder>");
            }

            sb.AppendLine("  </Document>");
            sb.AppendLine("</kml>");

            return sb.ToString();
        }

        /// <summary>
        /// 获取几何的中心点
        /// </summary>
        private MapPoint GetGeometryCenter(Geometry geometry)
        {
            if (geometry == null) return null;

            try
            {
                switch (geometry.GeometryType)
                {
                    case GeometryType.Point:
                        return geometry as MapPoint;
                    case GeometryType.Polyline:
                        var polyline = geometry as Polyline;
                        if (polyline != null && polyline.PointCount > 0)
                        {
                            // 取中点
                            var midIndex = polyline.PointCount / 2;
                            return polyline.Points[midIndex];
                        }
                        break;
                    case GeometryType.Polygon:
                        var polygon = geometry as Polygon;
                        if (polygon != null)
                        {
                            // 使用质心
                            var centroid = GeometryEngine.Instance.Centroid(polygon);
                            return centroid;
                        }
                        break;
                    case GeometryType.Multipoint:
                        var multipoint = geometry as Multipoint;
                        if (multipoint != null && multipoint.PointCount > 0)
                        {
                            return multipoint.Points[0];
                        }
                        break;
                }
            }
            catch { }

            return null;
        }

        private string GeometryToKml(Geometry geometry)
        {
            if (geometry == null) return "";

            switch (geometry.GeometryType)
            {
                case GeometryType.Point:
                    return PointToKml(geometry as MapPoint);
                case GeometryType.Polyline:
                    return PolylineToKml(geometry as Polyline);
                case GeometryType.Polygon:
                    return PolygonToKml(geometry as Polygon);
                case GeometryType.Multipoint:
                    return MultipointToKml(geometry as Multipoint);
                default:
                    return "";
            }
        }

        private string PointToKml(MapPoint point)
        {
            if (point == null) return "";
            return $"      <Point>\n        <coordinates>{point.X},{point.Y},0</coordinates>\n      </Point>";
        }

        private string MultipointToKml(Multipoint multipoint)
        {
            if (multipoint == null || multipoint.PointCount == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("      <MultiGeometry>");
            
            foreach (var point in multipoint.Points)
            {
                sb.AppendLine("        <Point>");
                sb.AppendLine($"          <coordinates>{point.X},{point.Y},0</coordinates>");
                sb.AppendLine("        </Point>");
            }
            
            sb.Append("      </MultiGeometry>");
            return sb.ToString();
        }

        private string PolylineToKml(Polyline polyline)
        {
            if (polyline == null) return "";

            var sb = new StringBuilder();
            sb.AppendLine("      <LineString>");
            sb.AppendLine("        <coordinates>");

            var coords = new List<string>();
            foreach (var part in polyline.Parts)
            {
                foreach (var segment in part)
                {
                    coords.Add($"{segment.StartPoint.X},{segment.StartPoint.Y},0");
                }
                if (part.Count > 0)
                {
                    var lastSegment = part.Last();
                    coords.Add($"{lastSegment.EndPoint.X},{lastSegment.EndPoint.Y},0");
                }
            }

            sb.AppendLine($"          {string.Join(" ", coords)}");
            sb.AppendLine("        </coordinates>");
            sb.Append("      </LineString>");
            return sb.ToString();
        }

        private string PolygonToKml(Polygon polygon)
        {
            if (polygon == null) return "";

            var sb = new StringBuilder();
            sb.AppendLine("      <Polygon>");
            
            var isFirst = true;
            foreach (var part in polygon.Parts)
            {
                if (isFirst)
                {
                    sb.AppendLine("        <outerBoundaryIs>");
                    isFirst = false;
                }
                else
                {
                    sb.AppendLine("        <innerBoundaryIs>");
                }

                sb.AppendLine("          <LinearRing>");
                sb.AppendLine("            <coordinates>");

                var coords = new List<string>();
                foreach (var segment in part)
                {
                    coords.Add($"{segment.StartPoint.X},{segment.StartPoint.Y},0");
                }
                // 闭合环
                if (part.Count > 0)
                {
                    var firstSegment = part.First();
                    coords.Add($"{firstSegment.StartPoint.X},{firstSegment.StartPoint.Y},0");
                }

                sb.AppendLine($"              {string.Join(" ", coords)}");
                sb.AppendLine("            </coordinates>");
                sb.AppendLine("          </LinearRing>");

                if (isFirst == false && sb.ToString().Contains("outerBoundaryIs"))
                {
                    sb.AppendLine("        </outerBoundaryIs>");
                }
                else
                {
                    sb.AppendLine("        </innerBoundaryIs>");
                }
            }

            // 修正闭合标签
            var result = sb.ToString();
            if (result.Contains("<outerBoundaryIs>") && !result.Contains("</outerBoundaryIs>"))
            {
                result = result.TrimEnd() + "\n        </outerBoundaryIs>\n";
            }

            sb.Clear();
            sb.Append(result);
            sb.Append("      </Polygon>");
            return sb.ToString();
        }

        private async Task WriteKmzFile(string kmzPath, string kmlContent, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                using var kmzStream = new FileStream(kmzPath, FileMode.Create);
                using var archive = new ZipArchive(kmzStream, ZipArchiveMode.Create);
                
                var entry = archive.CreateEntry("doc.kml");
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write(kmlContent);
            }, cancellationToken);
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            return string.IsNullOrEmpty(sanitized) ? "export" : sanitized;
        }

        private string EscapeXml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        #endregion
    }

    /// <summary>
    /// KML要素类
    /// </summary>
    internal class KmlFeature
    {
        public string Name { get; set; }
        public Geometry Geometry { get; set; }
        public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
        public KmlStyle Style { get; set; }
    }

    /// <summary>
    /// KML样式类
    /// </summary>
    internal class KmlStyle
    {
        public string FillColor { get; set; }  // AABBGGRR 格式
        public string LineColor { get; set; }  // AABBGGRR 格式
        public double LineWidth { get; set; } = 1.0;
        public string IconColor { get; set; }  // AABBGGRR 格式
        public double IconScale { get; set; } = 1.0;
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
