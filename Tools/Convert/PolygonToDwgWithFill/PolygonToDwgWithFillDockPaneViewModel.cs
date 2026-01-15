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

// ACadSharp 写DWG所需
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;
using CSMath;
using CadLayer = ACadSharp.Tables.Layer;

namespace XIAOFUTools.Tools.PolygonToDwgWithFill
{
    // 组合键内部固定分隔符，保证多字段值唯一
    internal static class CompositeKey
    {
        public const string Delim = "\u001F"; // Unit Separator
    }

    // 供 UI 勾选的字段项
    public class FieldOption
    {
        public string Name { get; set; }
        public string Alias { get; set; }
        public bool IsSelected { get; set; }
        public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? Name : Alias;
        public string FullDisplayName => string.IsNullOrWhiteSpace(Alias) || string.Equals(Alias, Name, StringComparison.OrdinalIgnoreCase)
            ? Name
            : $"{Alias} [{Name}]";
        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// 面要素图层转DWG[带色块填充] DockPane视图模型
    /// </summary>
    internal class PolygonToDwgWithFillDockPaneViewModel : PropertyChangedBase
    {
        #region 构造与初始化
        public PolygonToDwgWithFillDockPaneViewModel()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();

            RefreshLayersCommand = new RelayCommand(RefreshLayers);
            BrowseOutputPathCommand = new RelayCommand(BrowseOutputPath);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            CancelCommand = new RelayCommand(RequestCancel, () => IsProcessing);
            RunCommand = new RelayCommand(ExecuteAsyncSafe, () => CanProcess);

            ExportBoundary = true;
            ExportHatch = true;
            LineWidth = 0.25;
            HatchTransparency = 0; // 0-100

            // 图层命名：默认关闭按字段命名，初始化集合
            UseFieldNaming = false;
            FieldNamingSeparator = "_";
            NamingFields = new ObservableCollection<FieldOption>();

            // 默认导出路径：桌面
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                OutputPath = System.IO.Path.Combine(desktop, "output.dwg");
            }
            catch { /* 容错 */ }

            // 初始化 DWG 版本选项
            DwgVersions = new ObservableCollection<DwgVersionOption>(new[]
            {
                new DwgVersionOption { Name = "AutoCAD 2018 (AC1032)", Version = ACadVersion.AC1032 },
                new DwgVersionOption { Name = "AutoCAD 2013 (AC1027)", Version = ACadVersion.AC1027 },
                new DwgVersionOption { Name = "AutoCAD 2010 (AC1024)", Version = ACadVersion.AC1024 },
                new DwgVersionOption { Name = "AutoCAD 2007 (AC1021)", Version = ACadVersion.AC1021 },
                new DwgVersionOption { Name = "AutoCAD 2004 (AC1018)", Version = ACadVersion.AC1018 },
                new DwgVersionOption { Name = "AutoCAD 2000 (AC1015)", Version = ACadVersion.AC1015 },
            });
            SelectedDwgVersion = DwgVersions.FirstOrDefault();

            StatusMessage = "初始化完成";
        }
        #endregion

        #region 绑定属性
        public ObservableCollection<FeatureLayer> PolygonLayers { get; }

        private FeatureLayer _selectedPolygonLayer;
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                if (SetProperty(ref _selectedPolygonLayer, value))
                {
                    NotifyPropertyChanged(nameof(CanProcess));
                    StatusMessage = value == null ? "未选择图层" : $"已选择图层: {value.Name}";
                    // 载入可用字段供命名多选
                    if (value != null) LoadNamingFieldsAsync(value);
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
                }
            }
        }

        // DWG 版本选择
        public ObservableCollection<DwgVersionOption> DwgVersions { get; }

        private DwgVersionOption _selectedDwgVersion;
        public DwgVersionOption SelectedDwgVersion
        {
            get => _selectedDwgVersion;
            set => SetProperty(ref _selectedDwgVersion, value);
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
            set => SetProperty(ref _lineWidth, value);
        }

        private int _hatchTransparency;
        public int HatchTransparency
        {
            get => _hatchTransparency;
            set => SetProperty(ref _hatchTransparency, Math.Clamp(value, 0, 90)); // DWG常用0-90
        }

        // 图层命名：是否按字段命名
        private bool _useFieldNaming;
        public bool UseFieldNaming
        {
            get => _useFieldNaming;
            set => SetProperty(ref _useFieldNaming, value);
        }

        // 图层命名：分隔符
        private string _fieldNamingSeparator;
        public string FieldNamingSeparator
        {
            get => _fieldNamingSeparator;
            set => SetProperty(ref _fieldNamingSeparator, value);
        }

        // 图层命名：可选字段集合（UI 多选）
        public ObservableCollection<FieldOption> NamingFields { get; }

        private int _progress;
        public int Progress
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

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    NotifyPropertyChanged(nameof(CanProcess));
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

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrWhiteSpace(OutputPath);
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
                    var temp = new System.Collections.Generic.List<FeatureLayer>();

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
                Filter = "AutoCAD DWG (*.dwg)|*.dwg",
                DefaultExt = ".dwg",
                FileName = SelectedPolygonLayer != null ? SanitizeFileName(SelectedPolygonLayer.Name) + ".dwg" : "output.dwg"
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
            LogInfo("使用说明：选择面图层，设置输出DWG路径，点击生成DWG。");
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
                    StatusMessage = "正在生成DWG...";

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

        private async Task ExecuteAsync()
        {
            if (SelectedPolygonLayer == null)
            {
                StatusMessage = "请选择面图层";
                return;
            }
            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                StatusMessage = "请选择输出DWG路径";
                return;
            }

            // 1) 构建颜色获取器（支持简单渲染/唯一值/分级渲染）
            Func<Row, System.Drawing.Color?> colorGetter = await BuildColorGetterAsync(SelectedPolygonLayer);
            if (colorGetter == null)
            {
                StatusMessage = "无法解析图层渲染符号，请检查图层符号是否为面填充";
                return;
            }
            // 1.1) 构建图层名称获取器（与渲染器一致）
            Func<Row, string> layerNameGetter = await BuildLayerNameGetterAsync(SelectedPolygonLayer, colorGetter);
            if (layerNameGetter == null)
            {
                // 回退使用要素类名
                layerNameGetter = _ => SelectedPolygonLayer.Name;
            }

            // 2) 抽取面几何环与颜色
            StatusMessage = "正在提取要素与符号颜色...";
            var items = await ExtractPolygonsAsync(SelectedPolygonLayer, colorGetter, layerNameGetter);
            if (items.Count == 0)
            {
                StatusMessage = "所选图层无有效面要素";
                return;
            }
            LogInfo($"已提取 {items.Count} 个面（含内外环），准备生成DWG...");

            // 3) 生成DWG
            // 路径与权限检查
            try
            {
                var dir = Path.GetDirectoryName(OutputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }
            catch (Exception ioex)
            {
                StatusMessage = $"输出路径不可用: {ioex.Message}";
                LogError(StatusMessage);
                return;
            }

            // 创建文档
            var doc = new CadDocument();

            // 应用 DWG 版本（写入 Header）
            try
            {
                var ver = SelectedDwgVersion?.Version ?? ACadVersion.AC1032;
                doc.Header.Version = ver;
            }
            catch { /* 若版本设置异常则沿用库默认值 */ }

            // 进度
            int total = items.Count;
            int processed = 0;

            // 全局包络（用于初始视图定位）
            double gminx = double.PositiveInfinity, gminy = double.PositiveInfinity;
            double gmaxx = double.NegativeInfinity, gmaxy = double.NegativeInfinity;

            foreach (var item in items)
            {
                if (CancelRequested)
                {
                    StatusMessage = "已取消";
                    LogInfo(StatusMessage);
                    return;
                }

                // 目标图层（按渲染分类）
                var targetLayer = GetOrCreateLayer(doc, item.LayerName, item.Color);

                // 边界多段线（使用 Polyline2D）
                if (ExportBoundary)
                {
                    foreach (var ring in item.Rings)
                    {
                        // 更新全局包络
                        foreach (var pt in EnumerateRingCore(ring))
                        {
                            if (pt.X < gminx) gminx = pt.X;
                            if (pt.Y < gminy) gminy = pt.Y;
                            if (pt.X > gmaxx) gmaxx = pt.X;
                            if (pt.Y > gmaxy) gmaxy = pt.Y;
                        }

                        var vertices = BuildPolyline2DVertices(ring);
                        if (vertices.Count >= 2)
                        {
                            var pl = new Polyline2D(vertices, true);
                            pl.Color = ToAcadColor(item.Color);
                            pl.Layer = targetLayer;
                            // 线宽暂不强制设置，保持 ByLayer，避免与枚举值不匹配
                            doc.ModelSpace.Entities.Add(pl);
                        }
                    }
                }

                // 实体填充（SOLID Hatch）
                if (ExportHatch)
                {
                    // 透明度：结合符号Alpha与用户设置（取更透明者），并限制在0-90
                    var alphaPercent = 100 - (int)Math.Round(item.Color.A * 100.0 / 255.0); // 0=不透明,100=全透明
                    var userPercent = Math.Clamp(HatchTransparency, 0, 100);
                    var finalPercent = Math.Clamp(Math.Max(alphaPercent, userPercent), 0, 90);

                    // 统一创建一个 Hatch，并保留路径对应的多段线以供关联/Properties 面积
                    var hatch = new Hatch
                    {
                        IsSolid = true,
                        IsAssociative = true,
                        Color = ACadSharp.Color.ByLayer,
                        Transparency = new Transparency((short)finalPercent),
                        Layer = targetLayer
                    };

                    foreach (var ring in item.Rings)
                    {
                        var vertices = BuildPolyline2DVertices(ring);
                        if (vertices.Count < 2)
                            continue;

                        var boundary = new Polyline2D(vertices, true)
                        {
                            Color = ToAcadColor(item.Color),
                            Layer = targetLayer
                        };

                        // 更新全局包络
                        foreach (var v in vertices)
                        {
                            if (v.Location.X < gminx) gminx = v.Location.X;
                            if (v.Location.Y < gminy) gminy = v.Location.Y;
                            if (v.Location.X > gmaxx) gmaxx = v.Location.X;
                            if (v.Location.Y > gmaxy) gmaxy = v.Location.Y;
                        }

                        doc.ModelSpace.Entities.Add(boundary);

                        var bp = new Hatch.BoundaryPath
                        {
                            Flags = BoundaryPathFlags.External | BoundaryPathFlags.Polyline
                        };

                        var edge = new Hatch.BoundaryPath.Polyline
                        {
                            IsClosed = true
                        };

                        foreach (var v in vertices)
                            edge.Vertices.Add(new XYZ(v.Location.X, v.Location.Y, 0));

                        bp.Edges.Add(edge);
                        bp.Entities.Add(boundary);
                        hatch.Paths.Add(bp);
                    }

                    doc.ModelSpace.Entities.Add(hatch);
                }

                processed++;
                if (total > 0)
                    Progress = Math.Clamp((int)(processed * 100.0 / total), 0, 95);
            }

            // 设定初始视图为模型空间并定位到要素范围
            ApplyInitialModelView(doc, gminx, gminy, gmaxx, gmaxy);

            // 写入DWG
            try
            {
                using (var writer = new DwgWriter(OutputPath, doc))
                {
                    writer.Write();
                }
                StatusMessage = $"已生成DWG：{OutputPath}";
                LogInfo(StatusMessage);
                Progress = 100;
            }
            catch (Exception ex)
            {
                StatusMessage = $"写入DWG失败：{ex.Message}";
                LogError(StatusMessage);
            }
        }
        #endregion

        #region 工具方法与日志
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
        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
        
        // System.Drawing.Color -> ACadSharp.Color（TrueColor）
        private static ACadSharp.Color ToAcadColor(System.Drawing.Color c)
        {
            return new ACadSharp.Color(c.R, c.G, c.B);
        }

        // 构建 Polyline2D 顶点列表（去除重复闭合点）
        private static List<Vertex2D> BuildPolyline2DVertices(List<MapPoint> ring)
        {
            var verts = new List<Vertex2D>();
            foreach (var pt in EnumerateRingCore(ring))
            {
                verts.Add(new Vertex2D(new XYZ(pt.X, pt.Y, 0))); // bulge=0
            }
            return verts;
        }

        // 遍历环点序列且去掉尾部与首点重复
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
        
        /// <summary>
        /// 读取图层渲染，返回按行取填充色的方法（支持Simple/UniqueValue/ClassBreaks）。
        /// </summary>
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
                    var fill = GetFillColor(sym);
                    if (fill == null)
                    {
                        result = null;
                        return;
                    }
                    result = _ => fill;
                    return;
                }

                // 唯一值渲染
                if (renderer is CIMUniqueValueRenderer unique)
                {
                    if (unique.Fields == null || unique.Fields.Length == 0)
                    {
                        result = null;
                        return;
                    }
                    string field = unique.Fields[0];
                    var map = new Dictionary<string, System.Drawing.Color?>();
                    if (unique.Groups != null)
                    {
                        foreach (var g in unique.Groups)
                        {
                            if (g?.Classes == null) continue;
                            foreach (var cls in g.Classes)
                            {
                                var usym = cls.Symbol?.Symbol as CIMPolygonSymbol;
                                var c = GetFillColor(usym);
                                if (cls?.Values == null) continue;
                                foreach (var v in cls.Values)
                                {
                                    if (v?.FieldValues == null || v.FieldValues.Length == 0) continue;
                                    map[v.FieldValues[0]] = c;
                                }
                            }
                        }
                    }
                    result = row =>
                    {
                        try
                        {
                            var idx = row.FindField(field);
                            if (idx < 0) return null;
                            var val = row[idx]?.ToString();
                            if (val == null) return null;
                            if (map.TryGetValue(val, out var col)) return col;
                            return null;
                        }
                        catch { return null; }
                    };
                    return;
                }

                // 分级渲染
                if (renderer is CIMClassBreaksRenderer br)
                {
                    string field = br.Field;
                    var list = new List<(double? min, double? max, System.Drawing.Color? color)>();
                    if (br.Breaks != null)
                    {
                        foreach (var b in br.Breaks)
                        {
                            var bsym = b.Symbol?.Symbol as CIMPolygonSymbol;
                            var col = GetFillColor(bsym);
                            // ArcGIS Pro SDK: 使用上界作为分级阈值
                            list.Add((null, b.UpperBound, col));
                        }
                    }
                    result = row =>
                    {
                        try
                        {
                            var idx = row.FindField(field);
                            if (idx < 0) return null;
                            var valObj = row[idx];
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

                // 不支持的渲染器
                result = null;
            });
            return result;
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
        
        // 导出项（一个要素 -> 多个环）
        private class PolygonItem
        {
            public List<List<MapPoint>> Rings { get; set; } = new();
            public System.Drawing.Color Color { get; set; }
            public string LayerName { get; set; }
        }

        /// <summary>
        /// DWG 版本选项（显示名 + 枚举值）
        /// </summary>
        public class DwgVersionOption
        {
            public string Name { get; set; }
            public ACadVersion Version { get; set; }
            public override string ToString() => Name;
        }

        /// <summary>
        /// 遍历图层要素，抽取几何环集合、颜色与图层名。
        /// Rings：每个要素内包含多个闭合环（保持原始顺序）。
        /// </summary>
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
                    if (row is not Feature feat) continue;
                    var poly = feat.GetShape() as Polygon;
                    if (poly == null || poly.IsEmpty) continue;

                    var rings = new List<List<MapPoint>>();
                    foreach (var part in poly.Parts)
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
                    var col = colorGetter(row) ?? System.Drawing.Color.FromArgb(255, 200, 200, 200);
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

        /// <summary>
        /// 根据渲染器构建图层名获取器：
        /// - Simple: 固定为图层名
        /// - UniqueValue: 按字段值映射到类标签或字段值
        /// - ClassBreaks: 以分级标签或“≤上界”命名
        /// </summary>
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
                var renderer = fl.GetRenderer();
                if (renderer == null)
                {
                    result = null;
                    return;
                }

                // 优先：使用用户选定字段进行命名（支持多字段组合）
                if (useFieldNaming && selectedFields.Length > 0)
                {
                    result = row => SanitizeLayerName(BuildCompositeKeyFromRow(row, selectedFields, sep));
                    return;
                }

                // Simple
                if (renderer is CIMSimpleRenderer sr)
                {
                    var baseName = SanitizeLayerName(fl.Name);
                    result = _ => baseName;
                    return;
                }

                // UniqueValue（支持多字段组合键）
                if (renderer is CIMUniqueValueRenderer uv)
                {
                    var fields = (uv.Fields ?? Array.Empty<string>()).ToArray();
                    if (fields.Length == 0)
                    {
                        result = _ => SanitizeLayerName(fl.Name);
                        return;
                    }

                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    var delim = CompositeKey.Delim;

                    foreach (var group in uv.Groups ?? Array.Empty<CIMUniqueValueGroup>())
                    {
                        foreach (var cls in group.Classes ?? Array.Empty<CIMUniqueValueClass>())
                        {
                            string label = !string.IsNullOrWhiteSpace(cls.Label) ? cls.Label : null;
                            foreach (var uvv in cls.Values ?? Array.Empty<CIMUniqueValue>())
                            {
                                var key = BuildCompositeKey(uvv?.FieldValues, delim);
                                var candidate = label ?? key;
                                dict[key] = SanitizeLayerName(candidate);
                            }
                        }
                    }

                    result = row =>
                    {
                        try
                        {
                            var key = BuildCompositeKeyFromRow(row, fields, delim);
                            if (dict.TryGetValue(key, out var nm) && !string.IsNullOrWhiteSpace(nm)) return nm;
                            return SanitizeLayerName(fl.Name);
                        }
                        catch { return SanitizeLayerName(fl.Name); }
                    };
                    return;
                }

                // ClassBreaks
                if (renderer is CIMClassBreaksRenderer br)
                {
                    string field = br.Field;
                    var list = new List<(double ub, string name)>();
                    if (br.Breaks != null)
                    {
                        foreach (var b in br.Breaks)
                        {
                            string label = !string.IsNullOrWhiteSpace(b.Label) ? b.Label : $"≤{b.UpperBound}";
                            list.Add((b.UpperBound, SanitizeLayerName(label)));
                        }
                    }
                    result = row =>
                    {
                        try
                        {
                            var idx = row.FindField(field);
                            if (idx < 0) return SanitizeLayerName(fl.Name);
                            var valObj = row[idx];
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

        private void LoadNamingFieldsAsync(FeatureLayer fl)
        {
            NamingFields?.Clear();
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
                            foreach (var field in fields)
                            {
                                if (field == null) continue;
                                var ft = field.FieldType;
                                if (ft == FieldType.Geometry || ft == FieldType.OID || ft == FieldType.GlobalID || ft == FieldType.GUID)
                                    continue;
                                string alias = null;
                                try { alias = field.AliasName; } catch { alias = null; }
                                if (string.IsNullOrWhiteSpace(alias)) alias = field.Name;
                                list.Add((field.Name, alias));
                            }
                            return list;
                        }
                        catch { return new List<(string Name, string Alias)>(); }
                    });

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        NamingFields.Clear();
                        foreach (var item in items)
                            NamingFields.Add(new FieldOption { Name = item.Name, Alias = item.Alias, IsSelected = false });
                    });
                }
                catch { }
            });
        }

        private static string Canonicalize(object v)
        {
            if (v == null) return "null";
            return v switch
            {
                string s => s.Trim(),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? "null",
                _ => v.ToString()?.Trim() ?? "null"
            };
        }

        private static string BuildCompositeKey(IEnumerable<object> values, string delimiter)
        {
            var arr = values == null ? Array.Empty<object>() : values.ToArray();
            if (arr.Length == 0) return "null";
            return string.Join(delimiter, arr.Select(Canonicalize));
        }

        private static string BuildCompositeKeyFromRow(Row row, string[] fields, string delimiter)
        {
            if (row == null || fields == null || fields.Length == 0)
                return "null";
            var list = new List<string>(fields.Length);
            foreach (var f in fields)
            {
                var val = GetFieldValue(row, f);
                list.Add(Canonicalize(val));
            }
            return string.Join(delimiter, list);
        }

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
                    try { return row[i]; }
                    catch { return null; }
                }
            }
            return null;
        }

        private static string SanitizeLayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Layer0";
            var invalid = new char[] { '<','>','/','\\',':','\"','?','*','|',',',';','=','[',']','{','}','(',')' };
            foreach (var c in invalid)
                name = name.Replace(c, '_');
            name = name.Replace(' ', '_');
            // 图层名不宜过长
            if (name.Length > 60)
                name = name.Substring(0, 60);
            return name;
        }

        private static CadLayer GetOrCreateLayer(CadDocument doc, string layerName, System.Drawing.Color color)
        {
            var safe = SanitizeLayerName(layerName);
            CadLayer layer = null;
            try { layer = doc.Layers[safe]; } catch { layer = null; }
            if (layer == null)
            {
                layer = new CadLayer(safe)
                {
                    Color = new ACadSharp.Color(color.R, color.G, color.B)
                };
                doc.Layers.Add(layer);
            }
            return layer;
        }

        /// <summary>
        /// 设置初始视图：切换到模型空间，并根据全局包络设置 Extents。
        /// 某些查看器会参考 ExtMin/ExtMax 与 TileMode 来初始化打开视图。
        /// </summary>
        private static void ApplyInitialModelView(CadDocument doc, double minx, double miny, double maxx, double maxy)
        {
            try
            {
                // 必须是有效范围
                if (double.IsInfinity(minx) || double.IsInfinity(miny) || double.IsInfinity(maxx) || double.IsInfinity(maxy))
                    return;
                if (maxx <= minx || maxy <= miny)
                    return;

                var header = doc.Header;

                // 切换到模型空间
                header.ShowModelSpace = true;

                // 设置模型空间范围，Z 统一为 0
                var minPoint = new XYZ(minx, miny, 0);
                var maxPoint = new XYZ(maxx, maxy, 0);
                header.ModelSpaceExtMin = minPoint;
                header.ModelSpaceExtMax = maxPoint;
                var limitsMin = new XY(minx, miny);
                var limitsMax = new XY(maxx, maxy);
                header.ModelSpaceLimitsMin = limitsMin;
                header.ModelSpaceLimitsMax = limitsMax;

                // 设置默认视口高度与中心
                var width = maxx - minx;
                var height = maxy - miny;
                if (width > 0 && height > 0)
                {
                    if (!doc.VPorts.TryGetValue("*ACTIVE", out var vport))
                    {
                        vport = new VPort("*ACTIVE");
                        doc.VPorts.Add(vport);
                    }
                    vport.Center = new XY(minx + width / 2.0, miny + height / 2.0);
                    vport.ViewHeight = height;
                    vport.AspectRatio = width / height;
                }
            }
            catch { }
        }
        #endregion
    }

    /// <summary>
    /// 简易RelayCommand
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
}
