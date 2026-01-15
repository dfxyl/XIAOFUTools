using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
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

namespace XIAOFUTools.Tools.MapSheetsSmall
{
    internal class GenerateSmallMapSheetsDockPaneViewModel : PropertyChangedBase
    {
        public ObservableCollection<FeatureLayer> PolygonLayers { get; private set; } = new ObservableCollection<FeatureLayer>();
        private FeatureLayer _selectedPolygonLayer;
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set { SetProperty(ref _selectedPolygonLayer, value); NotifyPropertyChanged(() => CanRun); }
        }

        private async void OnExtentCreated(Envelope env)
        {
            _drawnExtent = env;
            NotifyPropertyChanged(() => DrawnExtentText);
            NotifyPropertyChanged(() => CanRun);
            await FrameworkApplication.SetCurrentToolAsync("esri_mapping_exploreTool");
        }

        public ObservableCollection<string> ScaleNames { get; } = new ObservableCollection<string>
        {
            "100万","50万","25万","10万","5万","2.5万","1万","5千"
        };
        private string _selectedScaleName = "10万";
        public string SelectedScaleName { get => _selectedScaleName; set => SetProperty(ref _selectedScaleName, value); }

        private string _outputFeatureClassPath = "";
        public string OutputFeatureClassPath
        {
            get => _outputFeatureClassPath;
            set
            {
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, "MapSheets");
                SetProperty(ref _outputFeatureClassPath, normalized);
                NotifyPropertyChanged(() => CanRun);
            }
        }

        private bool _useLayerExtent = true;
        public bool UseLayerExtent { get => _useLayerExtent; set => SetProperty(ref _useLayerExtent, value); }

        private bool _isProcessing;
        public bool IsProcessing { get => _isProcessing; set { SetProperty(ref _isProcessing, value); NotifyPropertyChanged(() => CanRun); } }

        private string _logContent = "";
        public string LogContent { get => _logContent; set => SetProperty(ref _logContent, value); }

        private Envelope _drawnExtent; // 在当前地图坐标系下
        public string DrawnExtentText { get => _drawnExtent == null ? "未选择" : $"X:[{_drawnExtent.XMin:F4},{_drawnExtent.XMax:F4}] Y:[{_drawnExtent.YMin:F4},{_drawnExtent.YMax:F4}]"; }
        public bool HasDrawnExtent => _drawnExtent != null && !_drawnExtent.IsEmpty;

        // 范围模式：Layer / Map / Custom
        private string _selectedRangeMode = "Layer";
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

        public GenerateSmallMapSheetsDockPaneViewModel()
        {
            RefreshLayers();
            if (string.IsNullOrWhiteSpace(OutputFeatureClassPath))
            {
                var gdb = Project.Current?.DefaultGeodatabasePath;
                if (!string.IsNullOrEmpty(gdb))
                    OutputFeatureClassPath = Path.Combine(gdb, $"MapSheets_{DateTime.Now:yyyyMMdd_HHmmss}");
            }
            // 订阅框选事件
            CustomExtentTool.ExtentCreatedStatic -= OnExtentCreated;
            CustomExtentTool.ExtentCreatedStatic += OnExtentCreated;
        }

        private void RefreshLayers()
        {
            PolygonLayers.Clear();
            var map = MapView.Active?.Map;
            if (map == null) return;
            foreach (var fl in map.Layers.OfType<FeatureLayer>())
            {
                // 仅面图层
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

        private async Task RunAsync()
        {
            if (!CanRun) return;
            IsProcessing = true;
            AppendLog("开始生成图幅…");
            try
            {
                await QueuedTask.Run(() =>
                {
                    // 根据模式获取范围
                    Envelope extent = null;
                    SpatialReference sr = null;
                    if (IsCustomMode)
                    {
                        if (!HasDrawnExtent)
                            throw new InvalidOperationException("请选择自定义范围（框选）后再运行。");
                        extent = _drawnExtent;
                        sr = _drawnExtent.SpatialReference;
                    }
                    else if (IsLayerMode)
                    {
                        if (SelectedPolygonLayer == null)
                            throw new InvalidOperationException("请选择一个面图层以使用图层范围。");
                        extent = SelectedPolygonLayer.QueryExtent();
                        sr = SelectedPolygonLayer.GetSpatialReference();
                    }
                    else // Map 视图范围
                    {
                        var mv = MapView.Active;
                        extent = mv?.Extent;
                        sr = extent?.SpatialReference;
                    }
                    if (extent == null || sr == null)
                        throw new InvalidOperationException("无法获取范围或空间参考。");

                    // 统一到 CGCS2000（EPSG:4490）下进行分幅与编号计算
                    var cgcs2000 = SpatialReferenceBuilder.CreateSpatialReference(4490);
                    var extentWgs = (Envelope)GeometryEngine.Instance.Project(extent, cgcs2000);

                    // 计算覆盖的百万分幅编码
                    var xmin = extentWgs.XMin; var xmax = extentWgs.XMax; var ymin = extentWgs.YMin; var ymax = extentWgs.YMax;

                    // 创建输出要素类（GP创建 + 添加字段），然后打开以写入
                    var fc = CreateOutputFeatureClass(OutputFeatureClassPath, sr);

                    // 生成
                    var scaleCode = GetScaleCode(SelectedScaleName);
                    var mapCodes = Compute100kCodes(xmin, xmax, ymin, ymax);

                    // 计算裁剪几何：
                    // 图层模式使用图层联合，其余使用矩形范围
                    Geometry clipGeom = IsLayerMode
                        ? BuildLayerUnionInCGCS2000(SelectedPolygonLayer, cgcs2000, extentWgs)
                        : PolygonBuilderEx.CreatePolygon(extentWgs);

                    int created = 0;
                    foreach (var code in mapCodes)
                    {
                        if (scaleCode == null)
                        {
                            var extent100k = Get100kMapExtent(code);
                            // 百万（100k 基图幅）邻接
                            var neighbors100k = GetNeighborsFor100k(code);
                            if (InsertPolygon(extent100k, sr, fc, code,
                                neighbors100k.left, neighbors100k.right, neighbors100k.up, neighbors100k.low,
                                neighbors100k.upl, neighbors100k.upr, neighbors100k.lowl, neighbors100k.lowr,
                                clipGeom))
                                created++;
                        }
                        else
                        {
                            var (rows, cols) = GetRowColCount(scaleCode);
                            for (int r = 1; r <= rows; r++)
                            for (int c = 1; c <= cols; c++)
                            {
                                var e = GetExtentByScale(code, scaleCode, r, c);
                                // 仅插入与原范围相交的图幅
                                var eEnv = EnvelopeBuilderEx.CreateEnvelope(e.xmin, e.ymin, e.xmax, e.ymax, cgcs2000);
                                if (!eEnv.IsEmpty && eEnv.Intersects(extentWgs))
                                {
                                    var full = $"{code}{scaleCode}{r:000}{c:000}";
                                    var n = GetNeighborsForScale(code, scaleCode, r, c);
                                    if (InsertPolygon((e.xmin, e.xmax, e.ymin, e.ymax), sr, fc, full,
                                        n.left, n.right, n.up, n.low, n.upl, n.upr, n.lowl, n.lowr,
                                        clipGeom))
                                        created++;
                                }
                            }
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

        private void MessageBox(string msg) => ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(msg, "提示");
        private void AppendLog(string msg) => LogContent += (LogContent.Length > 0 ? "\n" : string.Empty) + msg;

        #region 数据结构与写入
        private FeatureClass CreateOutputFeatureClass(string path, SpatialReference targetSR)
        {
            // 使用OutputDatasetUtils自动识别GDB或SHP
            var info = OutputDatasetUtils.ParseOutputPath(path, "MapSheets");
            
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
            string[] textFields = { "TFH","LeftMap","RightMap","UpMap","LowMap","UpLMap","UpRMap","LowLMap","LowRMap" };
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

        private bool InsertPolygon((double xmin, double xmax, double ymin, double ymax) e, SpatialReference targetSR, FeatureClass fc, string tfh,
            string left = null, string right = null, string up = null, string low = null,
            string upl = null, string upr = null, string lowl = null, string lowr = null,
            Geometry clipInCGCS2000 = null)
        {
            // 在 CGCS2000 构建，然后投影到目标SR
            var cgcs2000 = SpatialReferenceBuilder.CreateSpatialReference(4490);
            var env = EnvelopeBuilderEx.CreateEnvelope(e.xmin, e.ymin, e.xmax, e.ymax, cgcs2000);
            var poly = PolygonBuilderEx.CreatePolygon(env);
            if (clipInCGCS2000 != null)
            {
                var inter = GeometryEngine.Instance.Intersection(poly, clipInCGCS2000);
                if (inter == null || inter.IsEmpty)
                    return false; // 仅作为“压盖筛选”，保留整幅图幅矩形
                // 不替换 poly，保持输出为标准图幅矩形
            }
            if (targetSR != null && targetSR.Wkid != cgcs2000.Wkid)
                poly = (Polygon)GeometryEngine.Instance.Project(poly, targetSR);

            using (var rb = fc.CreateRowBuffer())
            {
                rb["SHAPE"] = poly;
                rb["TFH"] = tfh;
                rb["XMin"] = e.xmin; rb["XMax"] = e.xmax; rb["YMin"] = e.ymin; rb["YMax"] = e.ymax;
                if (!string.IsNullOrEmpty(left)) rb["LeftMap"] = left;
                if (!string.IsNullOrEmpty(right)) rb["RightMap"] = right;
                if (!string.IsNullOrEmpty(up)) rb["UpMap"] = up;
                if (!string.IsNullOrEmpty(low)) rb["LowMap"] = low;
                if (!string.IsNullOrEmpty(upl)) rb["UpLMap"] = upl;
                if (!string.IsNullOrEmpty(upr)) rb["UpRMap"] = upr;
                if (!string.IsNullOrEmpty(lowl)) rb["LowLMap"] = lowl;
                if (!string.IsNullOrEmpty(lowr)) rb["LowRMap"] = lowr;
                using (var row = fc.CreateRow(rb)) { }
            }
            return true;
        }
        #endregion

        #region 分幅算法（WGS84）
        private string GetScaleCode(string scaleName)
        {
            return scaleName switch
            {
                "100万" => null,
                "50万" => "B",
                "25万" => "C",
                "10万" => "D",
                "5万"  => "E",
                "2.5万"=> "F",
                "1万"  => "G",
                "5千"  => "H",
                _ => null
            };
        }
        private (int rows, int cols) GetRowColCount(string code)
        {
            return code switch
            {
                "B" => (2,2),
                "C" => (4,4),
                "D" => (12,12),
                "E" => (24,24),
                "F" => (48,48),
                "G" => (96,96),
                "H" => (192,192),
                _ => (0,0)
            };
        }
        private (double xmin, double xmax, double ymin, double ymax) Get100kMapExtent(string mapCode)
        {
            char rowLetter = mapCode[0];
            int colNumber = int.Parse(mapCode.Substring(1));
            // 仅实现北半球（中国范围）标准：A 行起算于 0°N，向北每行 4°
            int rowIndex = rowLetter - 'A';
            if (rowIndex < 0) rowIndex = 0; // 防止非法字母
            double baseLat = rowIndex * 4.0;
            // 经差阈值：0–60°:6°，60–76°:12°，≥76°:24°
            double bandCenterLat = baseLat + 2.0;
            double lonStep = (Math.Abs(bandCenterLat) < 60.0) ? 6.0 : (Math.Abs(bandCenterLat) < 76.0 ? 12.0 : 24.0);
            double baseLon = (colNumber - 1) * lonStep - 180.0;
            double width = lonStep;
            double xmin = baseLon;
            double xmax = baseLon + width;
            double ymin = baseLat;
            double ymax = baseLat + 4.0;
            if (Math.Abs(baseLat) >= 88)
            {
                xmax = 180.0;
                ymax = baseLat >= 0 ? 90.0 : -90.0;
            }
            return (xmin, xmax, ymin, ymax);
        }
        private (double xmin, double xmax, double ymin, double ymax) GetExtentByScale(string mapCode, string scaleCode, int row, int col)
        {
            var e = Get100kMapExtent(mapCode);
            var (rows, cols) = GetRowColCount(scaleCode);
            if (rows <= 0 || cols <= 0)
                throw new ArgumentException($"不支持的比例尺代码: {scaleCode}");
            double parentLat = e.ymax - e.ymin;  // 通常为 4°
            double parentLon = e.xmax - e.xmin;  // 依纬带为 6°/12°/24°
            double latStep = parentLat / rows;
            double lonStep = parentLon / cols;
            double map_xmin = e.xmin + (col - 1) * lonStep;
            double map_xmax = e.xmin + col * lonStep;
            double map_ymin = e.ymax - row * latStep;
            double map_ymax = e.ymax - (row - 1) * latStep;
            return (map_xmin, map_xmax, map_ymin, map_ymax);
        }
        private string[] Compute100kCodes(double xmin, double xmax, double ymin, double ymax)
        {
            var codes = new System.Collections.Generic.HashSet<string>();
            if (ymin < 0) ymin = 0; // 中国范围假定北半球
            int yStart = (int)Math.Floor(ymin / 4.0);
            int yEnd = (int)Math.Floor(ymax / 4.0);
            for (int y = yStart; y <= yEnd; y++)
            {
                double bandLat = y * 4.0 + 2.0; // 中纬
                double lonStep = (bandLat < 60.0) ? 6.0 : (bandLat < 76.0 ? 12.0 : 24.0);
                int xStart = (int)Math.Floor((xmin + 180.0) / lonStep);
                int xEnd = (int)Math.Floor((xmax + 180.0) / lonStep);
                for (int x = xStart; x <= xEnd; x++)
                {
                    char rowLetter = (char)('A' + y);
                    string code = $"{rowLetter}{x + 1}";
                    codes.Add(code);
                }
            }
            return codes.ToArray();
        }

        // 计算选中图层在 CGCS2000 下的联合多边形（仅限与范围相交部分）
        private Geometry BuildLayerUnionInCGCS2000(FeatureLayer fl, SpatialReference cgcs2000, Envelope extentCgcs)
        {
            if (fl == null) return null;
            var fc = fl.GetFeatureClass();
            var layerSR = fl.GetSpatialReference();
            var extentInLayer = (Envelope)GeometryEngine.Instance.Project(extentCgcs, layerSR);
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
                        var g2 = GeometryEngine.Instance.Project(g, cgcs2000);
                        geoms.Add(g2);
                    }
                }
            }
            if (geoms.Count == 0) return null;
            return GeometryEngine.Instance.Union(geoms);
        }

        // 计算同基图幅（100k）的邻接TFH：用于scaleCode==null的情况
        private (string left, string right, string up, string low, string upl, string upr, string lowl, string lowr) GetNeighborsFor100k(string mapCode)
        {
            string left = GetAdjacent100k(mapCode, -1, 0);
            string right = GetAdjacent100k(mapCode, 1, 0);
            string up = GetAdjacent100k(mapCode, 0, 1);
            string low = GetAdjacent100k(mapCode, 0, -1);
            string upl = GetAdjacent100k(GetAdjacent100k(mapCode, -1, 0), 0, 1);
            string upr = GetAdjacent100k(GetAdjacent100k(mapCode, 1, 0), 0, 1);
            string lowl = GetAdjacent100k(GetAdjacent100k(mapCode, -1, 0), 0, -1);
            string lowr = GetAdjacent100k(GetAdjacent100k(mapCode, 1, 0), 0, -1);
            return (left, right, up, low, upl, upr, lowl, lowr);
        }

        // 计算任意比例尺下的邻接TFH（同尺度）。当越界时切换到相邻100k并做行列回卷
        private (string left, string right, string up, string low, string upl, string upr, string lowl, string lowr) GetNeighborsForScale(string base100k, string scaleCode, int r, int c)
        {
            var (rows, cols) = GetRowColCount(scaleCode);
            // 左右
            string leftBase = base100k; int lc = c - 1; int lr = r;
            if (lc < 1) { lc = cols; leftBase = GetAdjacent100k(base100k, -1, 0); }
            string rightBase = base100k; int rc = c + 1; int rr0 = r;
            if (rc > cols) { rc = 1; rightBase = GetAdjacent100k(base100k, 1, 0); }
            // 上下
            string upBase = base100k; int ur = r - 1; int uc = c;
            if (ur < 1) { ur = rows; upBase = GetAdjacent100k(base100k, 0, 1); }
            string lowBase = base100k; int dr = r + 1; int dc = c;
            if (dr > rows) { dr = 1; lowBase = GetAdjacent100k(base100k, 0, -1); }

            string left = $"{leftBase}{scaleCode}{lr:000}{lc:000}";
            string right = $"{rightBase}{scaleCode}{rr0:000}{rc:000}";
            string up = $"{upBase}{scaleCode}{ur:000}{uc:000}";
            string low = $"{lowBase}{scaleCode}{dr:000}{dc:000}";

            // 角
            var (ulBase, ulr, ulc) = (upBase, ur, uc - 1); if (ulc < 1) { ulc = cols; ulBase = GetAdjacent100k(upBase, -1, 0); }
            var (urBase, urr, urc) = (upBase, ur, uc + 1); if (urc > cols) { urc = 1; urBase = GetAdjacent100k(upBase, 1, 0); }
            var (dlBase, dlr, dlc) = (lowBase, dr, dc - 1); if (dlc < 1) { dlc = cols; dlBase = GetAdjacent100k(lowBase, -1, 0); }
            var (drBase, drr, drc) = (lowBase, dr, dc + 1); if (drc > cols) { drc = 1; drBase = GetAdjacent100k(lowBase, 1, 0); }

            string upl = $"{ulBase}{scaleCode}{ulr:000}{ulc:000}";
            string upr = $"{urBase}{scaleCode}{urr:000}{urc:000}";
            string lowl = $"{dlBase}{scaleCode}{dlr:000}{dlc:000}";
            string lowr = $"{drBase}{scaleCode}{drr:000}{drc:000}";

            return (left, right, up, low, upl, upr, lowl, lowr);
        }

        // 求相邻100k图幅代码，dx: -1左/1右, dy: -1下/1上
        private string GetAdjacent100k(string mapCode, int dx, int dy)
        {
            var e = Get100kMapExtent(mapCode);
            double width = e.xmax - e.xmin;
            double height = e.ymax - e.ymin; // 应为4°
            double cx = (e.xmin + e.xmax) / 2.0 + dx * width;
            double cy = (e.ymin + e.ymax) / 2.0 + dy * height;
            cx = NormalizeLon(cx);
            cy = Math.Max(-90, Math.Min(90, cy));
            return Get100kCodeByLatLon(cy, cx);
        }

        private static double NormalizeLon(double lon)
        {
            while (lon < -180) lon += 360;
            while (lon >= 180) lon -= 360;
            return lon;
        }

        // 由纬度/经度返回100k图幅代码
        private string Get100kCodeByLatLon(double lat, double lon)
        {
            if (lat < 0) lat = 0; // 假定北半球
            int rowIndex = (int)Math.Floor(lat / 4.0);
            char rowLetter = (char)('A' + rowIndex);
            double stepLon = (Math.Abs(lat) < 60.0) ? 6.0 : (Math.Abs(lat) < 76.0 ? 12.0 : 24.0);
            int colIndex = (int)Math.Floor((lon + 180.0) / stepLon);
            int colNumber = colIndex + 1;
            return $"{rowLetter}{colNumber}";
        }
        #endregion
    }
}
