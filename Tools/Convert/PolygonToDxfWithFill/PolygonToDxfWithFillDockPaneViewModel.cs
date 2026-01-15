using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Drawing;
using ArcGIS.Core.Data;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;

// netDxf 写DXF所需
using netDxf;
using netDxf.Tables;
using netDxf.Entities;
using netDxf.Header;
using DxfLayer = netDxf.Tables.Layer;

namespace XIAOFUTools.Tools.PolygonToDxfWithFill
{
    // 组合键内部固定分隔符，避免依赖 SDK 的 FieldDelimiter 属性
    internal static class CompositeKey
    {
        public const string Delim = "\u001F"; // Unit Separator，不会出现在普通字段中
    }

    // 供 UI 勾选的字段项
    public class FieldOption
    {
        // 字段真实名称（用于读取属性值）
        public string Name { get; set; }
        // 字段别名（用于显示）
        public string Alias { get; set; }
        public bool IsSelected { get; set; }
        // UI 显示名称：优先别名
        public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? Name : Alias;
        // 完整显示：别名 [字段名]（当有别名且不同于字段名时），否则仅字段名
        public string FullDisplayName =>
            string.IsNullOrWhiteSpace(Alias) || string.Equals(Alias, Name, StringComparison.OrdinalIgnoreCase)
                ? Name
                : $"{Alias} [{Name}]";
        public override string ToString() => DisplayName;
    }
    /// <summary>
    /// 面转DXF[带填充] DockPane视图模型
    /// </summary>
    internal class PolygonToDxfWithFillDockPaneViewModel : PropertyChangedBase
    {
        #region 构造与初始化
        public PolygonToDxfWithFillDockPaneViewModel()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            NamingFields = new ObservableCollection<FieldOption>();

            RefreshLayersCommand = new RelayCommand(RefreshLayers);
            BrowseOutputPathCommand = new RelayCommand(BrowseOutputPath);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            CancelCommand = new RelayCommand(RequestCancel, () => IsProcessing);
            RunCommand = new RelayCommand(ExecuteAsyncSafe, () => CanProcess);

            ExportBoundary = true;
            ExportHatch = true;
            LineWidth = 0.25;           // mm
            HatchTransparency = 0;      // 0-100

            // 默认导出路径：桌面
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrWhiteSpace(desktop) && Directory.Exists(desktop))
                {
                    var name = SelectedPolygonLayer != null ? SanitizeFileName(SelectedPolygonLayer.Name) : "output";
                    OutputPath = Path.Combine(desktop, name + ".dxf");
                }
            }
            catch { }

            // 字段命名相关默认值
            UseFieldNaming = false;
            FieldNamingSeparator = "_";

            // DXF版本选项
            DxfVersions = new ObservableCollection<DxfVersionOption>(new[]
            {
                new DxfVersionOption { Name = "AutoCAD 2018 (R2018)", Version = DxfVersion.AutoCad2018 },
                new DxfVersionOption { Name = "AutoCAD 2013 (R2013)", Version = DxfVersion.AutoCad2013 },
                new DxfVersionOption { Name = "AutoCAD 2010 (R2010)", Version = DxfVersion.AutoCad2010 },
                new DxfVersionOption { Name = "AutoCAD 2007 (R2007)", Version = DxfVersion.AutoCad2007 },
                new DxfVersionOption { Name = "AutoCAD 2004 (R2004)", Version = DxfVersion.AutoCad2004 },
                new DxfVersionOption { Name = "AutoCAD 2000 (R2000)", Version = DxfVersion.AutoCad2000 },
            });
            SelectedDxfVersion = DxfVersions.FirstOrDefault();

            StatusMessage = "初始化完成";
            // 确保初始化后命令可用状态立刻刷新
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
        #endregion

        #region 公开属性
        public ObservableCollection<FeatureLayer> PolygonLayers { get; }

        // 命名字段选项（使用复选框多选）
        public ObservableCollection<FieldOption> NamingFields { get; }

        private bool _useFieldNaming;
        public bool UseFieldNaming
        {
            get => _useFieldNaming;
            set => SetProperty(ref _useFieldNaming, value);
        }

        private string _fieldNamingSeparator;
        public string FieldNamingSeparator
        {
            get => _fieldNamingSeparator;
            set => SetProperty(ref _fieldNamingSeparator, value);
        }

        private FeatureLayer _selectedPolygonLayer;
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                if (SetProperty(ref _selectedPolygonLayer, value))
                {
                    try
                    {
                        // 仅当当前未指定输出路径时，按所选图层名补全默认输出路径
                        if (string.IsNullOrWhiteSpace(OutputPath))
                        {
                            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                            if (!string.IsNullOrWhiteSpace(desktop) && Directory.Exists(desktop))
                            {
                                var name = value != null ? SanitizeFileName(value.Name) : "output";
                                OutputPath = Path.Combine(desktop, name + ".dxf");
                            }
                        }

                        // 切换图层时，刷新命名字段列表
                        LoadNamingFieldsAsync(value);
                    }
                    catch { }
                    NotifyPropertyChanged(nameof(CanProcess));
                    // 立刻刷新按钮可用性
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _outputPath;
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                if (SetProperty(ref _outputPath, value))
                {
                    NotifyPropertyChanged(nameof(CanProcess));
                    // 立刻刷新按钮可用性
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ObservableCollection<DxfVersionOption> DxfVersions { get; }

        private DxfVersionOption _selectedDxfVersion;
        public DxfVersionOption SelectedDxfVersion
        {
            get => _selectedDxfVersion;
            set => SetProperty(ref _selectedDxfVersion, value);
        }

        private bool _exportBoundary;
        public bool ExportBoundary
        {
            get => _exportBoundary;
            set => SetProperty(ref _exportBoundary, value);
        }

        private bool _exportHatch;
        public bool ExportHatch
        {
            get => _exportHatch;
            set => SetProperty(ref _exportHatch, value);
        }

        private double _lineWidth;
        public double LineWidth
        {
            get => _lineWidth;
            set => SetProperty(ref _lineWidth, Math.Max(0.0, value));
        }

        private int _hatchTransparency;
        public int HatchTransparency
        {
            get => _hatchTransparency;
            set => SetProperty(ref _hatchTransparency, Math.Clamp(value, 0, 100));
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private bool _isProgressIndeterminate;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrWhiteSpace(OutputPath);

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    NotifyPropertyChanged(nameof(CanProcess));
                    // 立刻刷新按钮可用性
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool _cancelRequested;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
        }

        private string _logContent = string.Empty;
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }
        #endregion

        #region 命令
        public ICommand RefreshLayersCommand { get; }
        public ICommand BrowseOutputPathCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand RunCommand { get; }
        #endregion

        #region 命令实现
        public void RefreshLayers()
        {
            StatusMessage = "正在刷新图层列表...";
            LogInfo("开始刷新图层列表");

            Task.Run(async () =>
            {
                try
                {
                    var temp = new List<FeatureLayer>();

                    await QueuedTask.Run(() =>
                    {
                        var map = MapView.Active?.Map;
                        if (map == null)
                            return;

                        foreach (var fl in map.Layers.OfType<FeatureLayer>())
                        {
                            if (fl.ShapeType == esriGeometryType.esriGeometryPolygon)
                                temp.Add(fl);
                        }
                    });

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var fl in temp)
                            PolygonLayers.Add(fl);

                        StatusMessage = $"已加载 {PolygonLayers.Count} 个面图层";
                        LogInfo(StatusMessage);

                        if (SelectedPolygonLayer == null && PolygonLayers.Any())
                            SelectedPolygonLayer = PolygonLayers.First();
                    });
                }
                catch (Exception ex)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        StatusMessage = $"刷新图层失败: {ex.Message}";
                        LogError(StatusMessage);
                    });
                }
            });
        }

        private void BrowseOutputPath()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "AutoCAD DXF (*.dxf)|*.dxf",
                DefaultExt = ".dxf",
                FileName = SelectedPolygonLayer != null ? SanitizeFileName(SelectedPolygonLayer.Name) + ".dxf" : "output.dxf"
            };

            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                if (!string.IsNullOrWhiteSpace(desktop) && Directory.Exists(desktop))
                    sfd.InitialDirectory = desktop;
            }
            catch { }

            if (sfd.ShowDialog() == true)
            {
                OutputPath = sfd.FileName;
                StatusMessage = $"输出路径: {OutputPath}";
            }
        }

        private void ShowHelp()
        {
            LogInfo("使用说明：选择面图层，设置输出DXF路径，点击生成DXF。");
        }

        private void RequestCancel()
        {
            if (!IsProcessing) return;
            CancelRequested = true;
            StatusMessage = "已请求取消...";
            LogInfo(StatusMessage);
        }

        private void ExecuteAsyncSafe()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    IsProcessing = true;
                    Progress = 0;
                    IsProgressIndeterminate = true;
                    CancelRequested = false;
                    StatusMessage = "正在生成DXF...";

                    await ExecuteAsync();
                }
                catch (Exception ex)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        StatusMessage = $"执行失败: {ex.Message}";
                        LogError(StatusMessage);
                    });
                }
                finally
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        IsProcessing = false;
                        IsProgressIndeterminate = false;
                        Progress = CancelRequested ? 0 : 100;
                        NotifyPropertyChanged(nameof(CanProcess));
                    });
                }
            });
        }
        #endregion

        #region 主体逻辑
        private async Task ExecuteAsync()
        {
            if (SelectedPolygonLayer == null)
            {
                StatusMessage = "请选择面图层";
                return;
            }
            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                StatusMessage = "请选择输出DXF路径";
                return;
            }

            // 1) 构建颜色与图层名获取器
            Func<Row, System.Drawing.Color?> colorGetter = await BuildColorGetterAsync(SelectedPolygonLayer);
            if (colorGetter == null)
            {
                StatusMessage = "无法解析图层渲染符号，请检查图层符号是否为面填充";
                return;
            }
            Func<Row, string> layerNameGetter = await BuildLayerNameGetterAsync(SelectedPolygonLayer, colorGetter);

            // 2) 抽取面几何
            List<PolygonItem> polygons = await ExtractPolygonsAsync(SelectedPolygonLayer, colorGetter, layerNameGetter);
            if (polygons == null || polygons.Count == 0)
            {
                StatusMessage = "未提取到任何面要素";
                return;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsProgressIndeterminate = false;
                Progress = 0;
                StatusMessage = $"开始写入DXF，共 {polygons.Count} 个要素...";
            });

            // 3) 写DXF
            var version = SelectedDxfVersion?.Version ?? DxfVersion.AutoCad2018;
            var dxf = new DxfDocument(version);

            // 全局范围（可选，不强制设置）
            double gminx = double.PositiveInfinity, gminy = double.PositiveInfinity;
            double gmaxx = double.NegativeInfinity, gmaxy = double.NegativeInfinity;

            int total = polygons.Count;
            int index = 0;

            foreach (var item in polygons)
            {
                if (CancelRequested)
                {
                    LogInfo("用户取消，停止导出");
                    break;
                }

                var col = item.Color;
                var layer = GetOrCreateLayer(dxf, item.LayerName, col);

                // 每个要素：可选添加边界线 + 面填充
                var hatchPaths = new List<netDxf.Entities.HatchBoundaryPath>();

                foreach (var ring in item.Rings)
                {
                    // 构建用于 Hatch 边界的 Polyline2D（不加入文档）
                    var plBoundary = new netDxf.Entities.Polyline2D();
                    plBoundary.IsClosed = true;

                    // 构建用于可选显示的 Polyline2D（仅在 ExportBoundary=true 时加入文档）
                    netDxf.Entities.Polyline2D plDisplay = null;
                    if (ExportBoundary)
                    {
                        plDisplay = new netDxf.Entities.Polyline2D
                        {
                            IsClosed = true,
                            Color = AciColor.ByLayer,
                            Layer = layer,
                            Lineweight = ToNearestLineweight(LineWidth)
                        };
                    }

                    foreach (var pt in EnumerateRingCore(ring))
                    {
                        // 更新全局包络
                        if (pt.X < gminx) gminx = pt.X;
                        if (pt.Y < gminy) gminy = pt.Y;
                        if (pt.X > gmaxx) gmaxx = pt.X;
                        if (pt.Y > gmaxy) gmaxy = pt.Y;

                        var v = new netDxf.Entities.Polyline2DVertex(new netDxf.Vector2(pt.X, pt.Y));
                        plBoundary.Vertexes.Add(v);
                        if (plDisplay != null)
                            plDisplay.Vertexes.Add(new netDxf.Entities.Polyline2DVertex(new netDxf.Vector2(pt.X, pt.Y)));
                    }

                    // 为该闭合环单独创建一个 HatchBoundaryPath（更符合 netDxf 多环/孔洞表达）
                    hatchPaths.Add(new netDxf.Entities.HatchBoundaryPath(new List<netDxf.Entities.EntityObject> { plBoundary }));

                    // 仅当导出边界时，才将显示用多段线加入文档
                    if (plDisplay != null)
                        dxf.Entities.Add(plDisplay);
                }

                if (ExportHatch)
                {
                    var solid = HatchPattern.Solid;
                    // 使用非关联式填充，避免自动将边界实体加入文档导致冲突
                    var hatch = new Hatch(solid, hatchPaths, false)
                    {
                        Color = AciColor.ByLayer,
                        Layer = layer
                    };

                    // 透明度（0-90）
                    byte tr = (byte)Math.Clamp((int)Math.Round(HatchTransparency * 0.9), 0, 90);
                    hatch.Transparency = new netDxf.Transparency(tr);

                    dxf.Entities.Add(hatch);
                }

                index++;
                if (index % 10 == 0 || index == total)
                {
                    int p = (int)Math.Round(100.0 * index / total);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Progress = p;
                        StatusMessage = $"正在写入DXF: {index}/{total}";
                    });
                }
            }

            // 4) 设置图形范围与默认视图为图层范围
            if (polygons.Count > 0)
            {
                try
                {
                    // 注：当前项目引用的 netDxf 版本不支持通过 HeaderVariables 设置 ExtMin/ExtMax/LimMin/LimMax
                    // 为确保兼容性，这里仅设置 *Active 视口的中心与高度来控制默认视图范围。

                    var cx = 0.5 * (gminx + gmaxx);
                    var cy = 0.5 * (gminy + gmaxy);
                    var height = (gmaxy - gminy);
                    if (height <= 0) height = 1.0;
                    height *= 1.05; // 少量留白

                    netDxf.Tables.VPort vp = null;
                    try { vp = dxf.VPorts["*Active"]; } catch { vp = null; }
                    if (vp == null)
                    {
                        vp = new netDxf.Tables.VPort("*Active");
                        dxf.VPorts.Add(vp);
                    }
                    vp.ViewCenter = new netDxf.Vector2(cx, cy);
                    vp.ViewHeight = height;
                }
                catch { /* 忽略视图设置失败，不影响导出 */ }
            }

            // 5) 保存
            dxf.Save(OutputPath);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusMessage = CancelRequested ? "已取消" : $"完成: {OutputPath}";
                LogInfo(StatusMessage);
            });
        }
        #endregion

        #region 工具方法
        private static IEnumerable<MapPoint> EnumerateRingCore(List<MapPoint> ring)
        {
            if (ring == null || ring.Count == 0)
                yield break;

            int n = ring.Count;
            bool hasDuplicateClose = n >= 2 && ring[0].IsEqual(ring[n - 1]);
            int len = hasDuplicateClose ? n - 1 : n;
            for (int i = 0; i < len; i++)
                yield return ring[i];
        }

        private static DxfLayer GetOrCreateLayer(DxfDocument doc, string layerName, System.Drawing.Color color)
        {
            var safe = SanitizeLayerName(layerName);
            DxfLayer layer = null;
            try { layer = doc.Layers[safe]; } catch { layer = null; }
            if (layer == null)
            {
                layer = new DxfLayer(safe)
                {
                    Color = ToAciColor(color)
                };
                doc.Layers.Add(layer);
            }
            return layer;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private static string SanitizeLayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Layer0";
            var invalid = new char[] { '<','>','/','\\',':','\"','?','*','|',',',';','=','[',']','{','}','(',')' };
            foreach (var c in invalid)
                name = name.Replace(c, '_');
            name = name.Replace(' ', '_');
            if (name.Length > 60)
                name = name.Substring(0, 60);
            return name;
        }

        private static string Canonicalize(object v)
        {
            if (v == null) return "null";
            switch (v)
            {
                case string s:
                    return s.Trim();
                case IFormattable f:
                    return f.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? "null";
                default:
                    return v.ToString()?.Trim() ?? "null";
            }
        }

        private static string BuildCompositeKey(IEnumerable<object> values, string delimiter)
        {
            var arr = values == null ? Array.Empty<object>() : values.ToArray();
            if (arr.Length == 0) return "null";
            return string.Join(delimiter, arr.Select(Canonicalize));
        }

        private static string BuildCompositeKeyFromRow(Row row, string[] fields, string delimiter)
        {
            var list = new List<string>(fields.Length);
            foreach (var f in fields)
            {
                var v = GetFieldValue(row, f);
                list.Add(Canonicalize(v));
            }
            return string.Join(delimiter, list);
        }

        private static Lineweight ToNearestLineweight(double mm)
        {
            // DXF lineweight 以 1/100 mm 计。标准序列如下（摘取常用）：
            int[] std = new int[] { 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211 };
            int val = (int)Math.Round(mm * 100.0);
            int nearest = std.OrderBy(x => Math.Abs(x - val)).First();
            return (Lineweight)nearest;
        }

        // System.Drawing.Color -> netDxf AciColor（真彩色）
        private static AciColor ToAciColor(System.Drawing.Color c)
        {
            try { return new AciColor(c.R, c.G, c.B); }
            catch { return AciColor.ByLayer; }
        }

        // 安全按字段名读取值（避免依赖不存在的 FindField 扩展）
        private static object GetFieldValue(Row row, string fieldName)
        {
            if (row == null || string.IsNullOrWhiteSpace(fieldName)) return null;
            var fields = row.GetFields();
            if (fields == null) return null;
            for (int i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                if (f != null && string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    try { return row[i]; } catch { return null; }
                }
            }
            return null;
        }

        private async Task<List<PolygonItem>> ExtractPolygonsAsync(FeatureLayer fl, Func<Row, System.Drawing.Color?> colorGetter, Func<Row, string> layerNameGetter = null)
        {
            var results = new List<PolygonItem>();
            await QueuedTask.Run(() =>
            {
                using var table = fl.GetTable();
                using var cursor = table.Search(null, false);
                while (cursor.MoveNext())
                {
                    using var row = cursor.Current as Row;
                    if (row == null) continue;

                    // 获取几何
                    var shape = (row as Feature)?.GetShape();
                    if (shape is not Polygon polygon) continue;

                    var rings = new List<List<MapPoint>>();
                    foreach (var part in polygon.Parts)
                    {
                        var pts = new List<MapPoint>();
                        foreach (var seg in part)
                        {
                            if (pts.Count == 0)
                                pts.Add(seg.StartPoint);
                            pts.Add(seg.EndPoint);
                        }
                        if (pts.Count >= 4)
                        {
                            if (!pts[0].IsEqual(pts[^1]))
                                pts.Add(pts[0]);
                            rings.Add(pts);
                        }
                    }

                    if (rings.Count == 0) continue;
                    var colNullable = colorGetter(row);
                    if (!colNullable.HasValue)
                        throw new InvalidOperationException("存在要素颜色无法从符号系统解析，请检查渲染配置。");
                    var col = colNullable.Value;
                    string lname = null;
                    try { lname = layerNameGetter?.Invoke(row); } catch { lname = null; }
                    if (string.IsNullOrWhiteSpace(lname))
                        lname = $"Fill_{col.R:X2}{col.G:X2}{col.B:X2}";
                    lname = SanitizeLayerName(lname);
                    results.Add(new PolygonItem { Rings = rings, Color = col, LayerName = lname });
                }
            });
            return results;
        }

        private async Task<Func<Row, System.Drawing.Color?>> BuildColorGetterAsync(FeatureLayer fl)
        {
            Func<Row, System.Drawing.Color?> result = null;
            await QueuedTask.Run(() =>
            {
                var renderer = fl.GetRenderer();
                if (renderer == null)
                {
                    result = null;
                    return;
                }

                // 简单渲染
                if (renderer is CIMSimpleRenderer simple)
                {
                    var sym = simple.Symbol?.Symbol as CIMPolygonSymbol;
                    var baseColor = GetFillColor(sym);
                    result = _ => baseColor;
                    return;
                }

                // 唯一值（支持多字段组合键，基于 CIMUniqueValue.FieldValues 构建键）
                if (renderer is CIMUniqueValueRenderer uv)
                {
                    var dict = new Dictionary<string, System.Drawing.Color?>(StringComparer.OrdinalIgnoreCase);
                    var fields = (uv.Fields ?? Array.Empty<string>()).ToArray();
                    var delim = CompositeKey.Delim;

                    foreach (var group in uv.Groups ?? Array.Empty<CIMUniqueValueGroup>())
                    {
                        foreach (var cls in group.Classes ?? Array.Empty<CIMUniqueValueClass>())
                        {
                            var sym = cls.Symbol?.Symbol as CIMPolygonSymbol;
                            var col = GetFillColor(sym);
                            foreach (var val in cls.Values ?? Array.Empty<CIMUniqueValue>())
                            {
                                var key = BuildCompositeKey(val?.FieldValues, delim);
                                dict[key] = col;
                            }
                        }
                    }

                    var defaultCol = uv.UseDefaultSymbol ? GetFillColor((uv.DefaultSymbol?.Symbol as CIMPolygonSymbol)) : null;

                    result = row =>
                    {
                        try
                        {
                            if (fields.Length == 0)
                                return defaultCol; // 无字段时返回默认符号颜色（若有）
                            var key = BuildCompositeKeyFromRow(row, fields, delim);
                            if (dict.TryGetValue(key, out var c)) return c;
                            return defaultCol; // 未匹配时使用默认符号颜色（若有），否则 null
                        }
                        catch { return defaultCol; }
                    };
                    return;
                }

                // 分级渲染
                if (renderer is CIMClassBreaksRenderer cb)
                {
                    var list = new List<(double? min, double? max, System.Drawing.Color? col)>();
                    double? lastMax = null;
                    foreach (var info in cb.Breaks ?? Array.Empty<CIMClassBreak>())
                    {
                        var sym = info.Symbol?.Symbol as CIMPolygonSymbol;
                        var col = GetFillColor(sym);
                        double? maxv = info.UpperBound;
                        list.Add((lastMax, maxv, col));
                        lastMax = maxv;
                    }
                    string field = cb.Field ?? string.Empty;
                    result = row =>
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(field)) return null;
                            var valObj = GetFieldValue(row, field);
                            if (valObj == null) return null;
                            if (!double.TryParse(valObj.ToString(), out var d)) return null;
                            foreach (var (min, max, col) in list)
                            {
                                var okMin = !min.HasValue || d >= min.Value;
                                var okMax = !max.HasValue || d < max.Value;
                                if (okMin && okMax) return col;
                            }
                            return null;
                        }
                        catch { return null; }
                    };
                    return;
                }

                result = null;
            });
            return result;
        }

        private async Task<Func<Row, string>> BuildLayerNameGetterAsync(FeatureLayer fl, Func<Row, System.Drawing.Color?> colorGetter)
        {
            Func<Row, string> result = null;
            // 快照 UI 线程上的用户设置，避免跨线程读取集合
            bool useFieldNaming = UseFieldNaming;
            string sep = FieldNamingSeparator ?? string.Empty;
            string[] selectedFields = NamingFields?.Where(o => o.IsSelected)
                                                   .Select(o => o.Name)
                                                   .Where(n => !string.IsNullOrWhiteSpace(n))
                                                   .ToArray() ?? Array.Empty<string>();

            await QueuedTask.Run(() =>
            {
                // 优先：使用用户选定字段进行命名（支持多字段组合）
                if (useFieldNaming && selectedFields.Length > 0)
                {
                    result = row => SanitizeLayerName(BuildCompositeKeyFromRow(row, selectedFields, sep));
                    return;
                }

                var renderer = fl.GetRenderer();
                if (renderer == null)
                {
                    result = _ => SanitizeLayerName(fl.Name);
                    return;
                }

                // Simple => 图层名
                if (renderer is CIMSimpleRenderer)
                {
                    result = _ => SanitizeLayerName(fl.Name);
                    return;
                }

                // UniqueValue => 类标签或组合值（支持多字段组合键，未匹配返回 null 以走颜色回退分层）
                if (renderer is CIMUniqueValueRenderer uv)
                {
                    var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var fields = (uv.Fields ?? Array.Empty<string>()).ToArray();
                    var delim = CompositeKey.Delim;

                    foreach (var group in uv.Groups ?? Array.Empty<CIMUniqueValueGroup>())
                    {
                        foreach (var cls in group.Classes ?? Array.Empty<CIMUniqueValueClass>())
                        {
                            foreach (var val in cls.Values ?? Array.Empty<CIMUniqueValue>())
                            {
                                var key = BuildCompositeKey(val?.FieldValues, delim);
                                var nm = string.IsNullOrWhiteSpace(cls.Label) ? key : cls.Label;
                                nameMap[key] = SanitizeLayerName(nm);
                            }
                        }
                    }

                    var defaultName = uv.UseDefaultSymbol ? "Default" : null;

                    result = row =>
                    {
                        try
                        {
                            if (fields.Length == 0) return defaultName; // 无字段时返回默认名或 null
                            var key = BuildCompositeKeyFromRow(row, fields, delim);
                            return nameMap.TryGetValue(key, out var nm) ? nm : defaultName;
                        }
                        catch { return defaultName; }
                    };
                    return;
                }

                // ClassBreaks => 标签或“<=上界”
                if (renderer is CIMClassBreaksRenderer cb)
                {
                    var list = new List<(double upper, string name)>();
                    foreach (var info in cb.Breaks ?? Array.Empty<CIMClassBreak>())
                    {
                        string nm = string.IsNullOrWhiteSpace(info.Label) ? $"<= {info.UpperBound}" : info.Label;
                        list.Add((info.UpperBound, SanitizeLayerName(nm)));
                    }

                    string field = cb.Field ?? string.Empty;
                    result = row =>
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(field)) return SanitizeLayerName(fl.Name);
                            var valObj = GetFieldValue(row, field);
                            if (valObj == null) return SanitizeLayerName(fl.Name);
                            if (!double.TryParse(valObj.ToString(), out var d)) return SanitizeLayerName(fl.Name);
                            foreach (var (ub, nm) in list)
                            {
                                if (d < ub) return nm;
                            }
                            return SanitizeLayerName(fl.Name);
                        }
                        catch { return SanitizeLayerName(fl.Name); }
                    };
                    return;
                }

                result = _ => SanitizeLayerName(fl.Name);
            });
            return result;
        }

        // 加载当前图层的字段列表，供命名字段多选
        private void LoadNamingFieldsAsync(FeatureLayer fl)
        {
            NamingFields.Clear();
            if (fl == null) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var items = await QueuedTask.Run(() =>
                    {
                        try
                        {
                            using var table = fl.GetTable();
                            if (table == null) return new List<(string Name, string Alias)>();
                            var def = table.GetDefinition();
                            var fields = def.GetFields();
                            var list = new List<(string Name, string Alias)>();
                            foreach (var f in fields)
                            {
                                if (f == null) continue;
                                // 过滤几何OID等字段
                                var ft = f.FieldType;
                                if (ft == FieldType.Geometry || ft == FieldType.OID || ft == FieldType.GlobalID || ft == FieldType.GUID)
                                    continue;
                                string alias = null;
                                try { alias = f.AliasName; } catch { alias = null; }
                                if (string.IsNullOrWhiteSpace(alias)) alias = f.Name;
                                list.Add((f.Name, alias));
                            }
                            return list;
                        }
                        catch { return new List<(string Name, string Alias)>(); }
                    });

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        NamingFields.Clear();
                        foreach (var it in items)
                            NamingFields.Add(new FieldOption { Name = it.Name, Alias = it.Alias, IsSelected = false });
                    });
                }
                catch { }
            });
        }

        private static System.Drawing.Color? GetFillColor(CIMPolygonSymbol sym)
        {
            if (sym?.SymbolLayers == null) return null;
            foreach (var sl in sym.SymbolLayers)
            {
                if (sl is CIMSolidFill sf)
                {
                    var c = sf.Color;
                    if (c is CIMRGBColor rgb)
                    {
                        int a = (int)Math.Round(rgb.Alpha * 255.0 / 100.0);
                        int r = Math.Clamp((int)Math.Round(rgb.R), 0, 255);
                        int g = Math.Clamp((int)Math.Round(rgb.G), 0, 255);
                        int b = Math.Clamp((int)Math.Round(rgb.B), 0, 255);
                        return System.Drawing.Color.FromArgb(a, r, g, b);
                    }
                    if (c is CIMCMYKColor cmyk)
                    {
                        // 简易CMYK转RGB
                        double c1 = cmyk.C / 100.0, m = cmyk.M / 100.0, y = cmyk.Y / 100.0, k = cmyk.K / 100.0;
                        int r = (int)Math.Round(255 * (1 - c1) * (1 - k));
                        int g = (int)Math.Round(255 * (1 - m) * (1 - k));
                        int b = (int)Math.Round(255 * (1 - y) * (1 - k));
                        int a = (int)Math.Round(cmyk.Alpha * 255.0 / 100.0);
                        return System.Drawing.Color.FromArgb(a, Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
                    }
                }
            }
            return null;
        }

        private void LogInfo(string message)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{ts}] {message}" + Environment.NewLine;
        }

        private void LogError(string message)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{ts}] 错误: {message}" + Environment.NewLine;
        }

        private class PolygonItem
        {
            public List<List<MapPoint>> Rings { get; set; } = new();
            public System.Drawing.Color Color { get; set; }
            public string LayerName { get; set; }
        }
        #endregion
    }

    /// <summary>
    /// 简易RelayCommand（与DWG工具一致）
    /// </summary>
    internal class RelayCommand : ICommand
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

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
    }

    public class DxfVersionOption
    {
        public string Name { get; set; }
        public DxfVersion Version { get; set; }
        public override string ToString() => Name;
    }
}
