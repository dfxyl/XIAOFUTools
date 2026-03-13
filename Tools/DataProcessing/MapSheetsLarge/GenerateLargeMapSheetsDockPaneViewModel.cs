using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using XIAOFUTools.Tools.OvertureLoader.Views;
using XIAOFUTools.Common;
using ArcGIS.Desktop.Framework.Dialogs;

namespace XIAOFUTools.Tools.MapSheetsLarge
{
    internal class GenerateLargeMapSheetsDockPaneViewModel : PropertyChangedBase
    {
        public ObservableCollection<FeatureLayer> PolygonLayers { get; private set; } = new ObservableCollection<FeatureLayer>();
        private FeatureLayer _selectedPolygonLayer;
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set { SetProperty(ref _selectedPolygonLayer, value); NotifyPropertyChanged(() => CanRun); }
        }

        public ObservableCollection<string> ScaleNames { get; } = new ObservableCollection<string>
        {
            "1:5000/40*40","1:2000/50*50","1:1000/50*50","1:500/50*50",
            "1:5000/50*40","1:2000/50*40","1:1000/50*40","1:500/50*40",
            "自定义尺寸"
        };
        private string _selectedScaleName = "1:2000/50*50";
        public string SelectedScaleName { get => _selectedScaleName; set { SetProperty(ref _selectedScaleName, value); NotifyPropertyChanged(() => IsCustomSize); } }

        public ObservableCollection<string> NamingConventions { get; } = new ObservableCollection<string> { "X-Y", "Y-X" };
        private string _namingConvention = "X-Y";
        public string NamingConvention { get => _namingConvention; set => SetProperty(ref _namingConvention, value); }

        private int _decimalPlaces = 3;
        public int DecimalPlaces { get => _decimalPlaces; set => SetProperty(ref _decimalPlaces, value); }

        private double _customWidth = 500; // 米
        public double CustomWidth { get => _customWidth; set => SetProperty(ref _customWidth, value); }
        private double _customHeight = 500; // 米
        public double CustomHeight { get => _customHeight; set => SetProperty(ref _customHeight, value); }
        public bool IsCustomSize => SelectedScaleName == "自定义尺寸";

        private string _outputFeatureClassPath = string.Empty;
        public string OutputFeatureClassPath
        {
            get => _outputFeatureClassPath;
            set
            {
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, "LargeMapSheets");
                SetProperty(ref _outputFeatureClassPath, normalized);
                NotifyPropertyChanged(() => CanRun);
            }
        }

        private bool _isProcessing;
        public bool IsProcessing { get => _isProcessing; set { SetProperty(ref _isProcessing, value); NotifyPropertyChanged(() => CanRun); } }

        private string _logContent = "";
        public string LogContent { get => _logContent; set => SetProperty(ref _logContent, value); }

        private Envelope _drawnExtent; // 在当前地图坐标系下
        public string DrawnExtentText { get => _drawnExtent == null ? "未选择" : $"X:[{_drawnExtent.XMin:F4},{_drawnExtent.XMax:F4}] Y:[{_drawnExtent.YMin:F4},{_drawnExtent.YMax:F4}]"; }
        public bool HasDrawnExtent => _drawnExtent != null && !_drawnExtent.IsEmpty;

        private string _selectedRangeMode = "Layer"; // Layer / Map / Custom
        public string SelectedRangeMode
        {
            get => _selectedRangeMode;
            set
            {
                SetProperty(ref _selectedRangeMode, value);
                NotifyPropertyChanged(() => IsLayerMode);
                NotifyPropertyChanged(() => IsMapMode);
                NotifyPropertyChanged(() => IsCustomMode);
                NotifyPropertyChanged(() => CanRun);
            }
        }
        public bool IsLayerMode { get => SelectedRangeMode == "Layer"; set { if (value) SelectedRangeMode = "Layer"; } }
        public bool IsMapMode { get => SelectedRangeMode == "Map"; set { if (value) SelectedRangeMode = "Map"; } }
        public bool IsCustomMode { get => SelectedRangeMode == "Custom"; set { if (value) SelectedRangeMode = "Custom"; } }

        public bool CanRun =>
            !IsProcessing &&
            !string.IsNullOrWhiteSpace(OutputFeatureClassPath) &&
            (
                (IsLayerMode && SelectedPolygonLayer != null) ||
                (IsCustomMode && HasDrawnExtent) ||
                IsMapMode
            );

        public ICommand RefreshLayersCommand => new RelayCommand(() => RefreshLayers());
        public ICommand BrowseOutputPathCommand => new RelayCommand(() => BrowseOutputPath());
        public ICommand RunCommand => new RelayCommand(async () => await RunAsync(), () => CanRun);
        public ICommand DrawExtentCommand => new RelayCommand(async () => await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_CustomExtentTool"));
        public ICommand ClearExtentCommand => new RelayCommand(() => { _drawnExtent = null; NotifyPropertyChanged(() => DrawnExtentText); NotifyPropertyChanged(() => CanRun); });
        public ICommand CancelCommand => new RelayCommand(() => { /* 可扩展取消逻辑 */ }, () => IsProcessing);

        public GenerateLargeMapSheetsDockPaneViewModel()
        {
            RefreshLayers();
            if (string.IsNullOrWhiteSpace(OutputFeatureClassPath))
            {
                var gdb = Project.Current?.DefaultGeodatabasePath;
                if (!string.IsNullOrEmpty(gdb))
                    OutputFeatureClassPath = Path.Combine(gdb, $"LargeMapSheets_{DateTime.Now:yyyyMMdd_HHmmss}");
            }
            CustomExtentTool.ExtentCreatedStatic -= OnExtentCreated;
            CustomExtentTool.ExtentCreatedStatic += OnExtentCreated;
        }

        private async void OnExtentCreated(Envelope env)
        {
            try
            {
                _drawnExtent = env;
                NotifyPropertyChanged(() => DrawnExtentText);
                NotifyPropertyChanged(() => CanRun);
                await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

        private void RefreshLayers()
        {
            PolygonLayers.Clear();
            var map = MapView.Active?.Map;
            if (map == null) return;
            foreach (var fl in map.Layers.OfType<FeatureLayer>())
            {
                if (fl.ShapeType == esriGeometryType.esriGeometryPolygon)
                    PolygonLayers.Add(fl);
            }
            if (PolygonLayers.Count > 0 && SelectedPolygonLayer == null)
                SelectedPolygonLayer = PolygonLayers[0];
        }

        private void BrowseOutputPath()
        {
            try
            {
                var initialLocation = PathDialogUtils.GetProjectDefaultGdb();
                var pickedPath = PathDialogUtils.PickSaveFeatureClassPath("选择输出位置", initialLocation);
                
                if (!string.IsNullOrEmpty(pickedPath))
                {
                    OutputFeatureClassPath = pickedPath;
                    AppendLog($"已设置输出: {OutputFeatureClassPath}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"选择输出位置出错: {ex.Message}");
            }
        }

        private (double width, double height)? GetScaleDimensions(string scaleName)
        {
            switch (scaleName)
            {
                case "1:5000/40*40": return (2000, 2000);
                case "1:2000/50*50": return (1000, 1000);
                case "1:1000/50*50": return (500, 500);
                case "1:500/50*50": return (250, 250);
                case "1:5000/50*40": return (2500, 2000);
                case "1:2000/50*40": return (1000, 800);
                case "1:1000/50*40": return (500, 400);
                case "1:500/50*40": return (250, 200);
                case "自定义尺寸": return null;
                default: return null;
            }
        }

        private async Task RunAsync()
        {
            if (!CanRun) return;
            IsProcessing = true;
            AppendLog("开始生成大比例尺图幅…");
            try
            {
                await QueuedTask.Run(() =>
                {
                    // 获取范围与目标SR
                    Envelope extent = null;
                    SpatialReference targetSR = null;
                    if (IsCustomMode)
                    {
                        if (!HasDrawnExtent)
                            throw new InvalidOperationException("请选择自定义范围（框选）后再运行。");
                        extent = _drawnExtent;
                        targetSR = _drawnExtent.SpatialReference;
                    }
                    else if (IsLayerMode)
                    {
                        if (SelectedPolygonLayer == null)
                            throw new InvalidOperationException("请选择一个面图层以使用图层范围。");
                        extent = SelectedPolygonLayer.QueryExtent();
                        targetSR = SelectedPolygonLayer.GetSpatialReference();
                    }
                    else // Map 视图范围
                    {
                        var mv = MapView.Active;
                        extent = mv?.Extent;
                        targetSR = extent?.SpatialReference;
                    }
                    if (extent == null || targetSR == null)
                        throw new InvalidOperationException("无法获取范围或空间参考。");

                    // 必须为投影坐标系
                    if (targetSR.IsProjected == false)
                        throw new InvalidOperationException("输入要素/地图的坐标系必须为投影坐标系。");

                    // 输出FC
                    var fc = CreateOutputFeatureClass(OutputFeatureClassPath, targetSR);

                    // clip几何
                    Geometry clipGeom = IsLayerMode
                        ? BuildLayerUnionInTargetSR(SelectedPolygonLayer, targetSR, extent)
                        : PolygonBuilderEx.CreatePolygon(extent);

                    var coverageExtent = clipGeom?.Extent ?? extent;

                    // 网格尺寸
                    double width, height;
                    var dims = GetScaleDimensions(SelectedScaleName);
                    if (dims == null)
                    {
                        if (CustomWidth <= 0 || CustomHeight <= 0)
                            throw new InvalidOperationException("自定义尺寸的宽度和高度必须为正数。");
                        width = CustomWidth; height = CustomHeight;
                    }
                    else { width = dims.Value.width; height = dims.Value.height; }

                    // 对齐到网格
                    var (x_start, x_end) = MapSheetCoverageUtils.AlignToGrid(coverageExtent.XMin, coverageExtent.XMax, width);
                    var (y_start, y_end) = MapSheetCoverageUtils.AlignToGrid(coverageExtent.YMin, coverageExtent.YMax, height);

                    int created = 0;
                    for (double x = x_start; x < x_end; x += width)
                    {
                        for (double y = y_start; y < y_end; y += height)
                        {
                            // 判断与范围相交
                            var cellEnv = EnvelopeBuilderEx.CreateEnvelope(x, y, x + width, y + height, targetSR);
                            if (clipGeom != null && !cellEnv.IsEmpty && !GeometryEngine.Instance.Intersects(cellEnv, clipGeom))
                                continue;

                            // 生成TFH
                            string fmt = "F" + Math.Max(0, DecimalPlaces);
                            string xName = (x / 1000.0).ToString(fmt);
                            string yName = (y / 1000.0).ToString(fmt);
                            string tfh = NamingConvention == "Y-X" ? $"{yName}-{xName}" : $"{xName}-{yName}";

                            if (InsertPolygon((x, x + width, y, y + height), targetSR, fc, tfh))
                                created++;
                        }
                    }

                    fc?.Dispose();
                    AppendLog($"完成，生成图幅 {created} 个。");
                });
            }
            catch (Exception ex)
            {
                AppendLog($"错误: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private FeatureClass CreateOutputFeatureClass(string path, SpatialReference targetSR)
        {
            // 使用OutputDatasetUtils自动识别GDB或SHP
            var info = OutputDatasetUtils.ParseOutputPath(path, "LargeMapSheets");
            
            // 检查是否存在,并提示覆盖
            if (OutputDatasetUtils.Exists(info))
            {
                bool overwrite = false;
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    var msg = $"输出要素{(info.IsGdb ? "类" : "")}已存在:{info.CatalogPath}。是否覆盖?";
                    var result = ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                    overwrite = result == System.Windows.MessageBoxResult.Yes;
                });
                if (!overwrite)
                    throw new OperationCanceledException("用户取消覆盖,操作已中止。");

                var deleteParams = Geoprocessing.MakeValueArray(info.CatalogPath);
                Geoprocessing.ExecuteToolAsync("Delete_management", deleteParams).Wait();
            }

            // 创建面要素类(自动支持GDB和SHP)
            var outName = info.IsGdb ? info.OutNameNoExt : info.OutNameNoExt + ".shp";
            var createParams = Geoprocessing.MakeValueArray(info.OutPathWorkspace, outName, "POLYGON", "#", "DISABLED", "DISABLED", targetSR);
            var r = Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", createParams).Result;
            if (r == null || r.IsFailed)
                throw new InvalidOperationException("创建要素类失败");

            // 追加字段
            string[] textFields = { "TFH" };
            foreach (var f in textFields)
            {
                var addParams = Geoprocessing.MakeValueArray(info.CatalogPath, f, "TEXT", "#", "#", 64);
                var rr = Geoprocessing.ExecuteToolAsync("AddField_management", addParams).Result;
                if (rr == null || rr.IsFailed) throw new InvalidOperationException($"添加字段失败: {f}");
            }
            string[] dblFields = { "XMin","XMax","YMin","YMax" };
            foreach (var f in dblFields)
            {
                var addParams = Geoprocessing.MakeValueArray(info.CatalogPath, f, "DOUBLE");
                var rr = Geoprocessing.ExecuteToolAsync("AddField_management", addParams).Result;
                if (rr == null || rr.IsFailed) throw new InvalidOperationException($"添加字段失败: {f}");
            }

            // 打开要素类(GDB和SHP使用不同方式)
            if (info.IsGdb)
            {
                var gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(info.GdbRoot)));
                return gdb.OpenDataset<FeatureClass>(info.RelativePathInGdb);
            }
            else
            {
                var conn = new FileSystemConnectionPath(new Uri(info.OutPathWorkspace), FileSystemDatastoreType.Shapefile);
                var fsds = new FileSystemDatastore(conn);
                return fsds.OpenDataset<FeatureClass>(info.OutNameNoExt);
            }
        }

        private bool InsertPolygon((double xmin, double xmax, double ymin, double ymax) e, SpatialReference targetSR, FeatureClass fc, string tfh)
        {
            var env = EnvelopeBuilderEx.CreateEnvelope(e.xmin, e.ymin, e.xmax, e.ymax, targetSR);
            var poly = PolygonBuilderEx.CreatePolygon(env);
            using (var rb = fc.CreateRowBuffer())
            {
                rb["SHAPE"] = poly;
                rb["TFH"] = tfh;
                rb["XMin"] = e.xmin; rb["XMax"] = e.xmax; rb["YMin"] = e.ymin; rb["YMax"] = e.ymax;
                using (var row = fc.CreateRow(rb)) { }
            }
            return true;
        }

        private Geometry BuildLayerUnionInTargetSR(FeatureLayer fl, SpatialReference targetSR, Envelope extent)
        {
            if (fl == null) return null;
            var fc = fl.GetFeatureClass();
            var layerSR = fl.GetSpatialReference();
            var extentInLayer = (Envelope)GeometryEngine.Instance.Project(extent, layerSR);
            var sq = new SpatialQueryFilter
            {
                FilterGeometry = extentInLayer,
                SpatialRelationship = SpatialRelationship.Intersects
            };
            var geoms = new System.Collections.Generic.List<Geometry>();
            using (var cursor = fc.Search(sq, false))
            {
                while (cursor.MoveNext())
                {
                    using (var row = cursor.Current as Feature)
                    {
                        var g = row.GetShape();
                        if (g == null || g.IsEmpty) continue;
                        if (g.SpatialReference == null || g.SpatialReference.Wkid != layerSR.Wkid)
                            g = GeometryEngine.Instance.Project(g, layerSR);
                        var g2 = GeometryEngine.Instance.Project(g, targetSR);
                        geoms.Add(g2);
                    }
                }
            }
            if (geoms.Count == 0) return PolygonBuilderEx.CreatePolygon(extent);
            return GeometryEngine.Instance.Union(geoms);
        }

        private void MessageBox(string msg) => ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(msg, "提示");
        private void AppendLog(string msg) => LogContent += (LogContent.Length > 0 ? "\n" : string.Empty) + msg;
    }
}
