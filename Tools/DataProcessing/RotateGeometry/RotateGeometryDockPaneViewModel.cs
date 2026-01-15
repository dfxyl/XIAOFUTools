using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Editing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace XIAOFUTools.Tools.RotateGeometry
{
    internal class RotateGeometryDockPaneViewModel : PropertyChangedBase
    {
        #region 属性
        private ObservableCollection<FeatureLayer> _featureLayers = new ObservableCollection<FeatureLayer>();
        public ObservableCollection<FeatureLayer> FeatureLayers
        {
            get => _featureLayers;
            set => SetProperty(ref _featureLayers, value);
        }

        private FeatureLayer _selectedLayer;
        public FeatureLayer SelectedLayer
        {
            get => _selectedLayer;
            set
            {
                if (SetProperty(ref _selectedLayer, value))
                {
                    NotifyPropertyChanged(() => HasSelectedLayer);
                    NotifyPropertyChanged(() => CanProcess);
                    LoadNumericFields();
                    UpdateSelectionInfo();
                    UpdateGeometryTypeFlags();
                    AppendLog($"已切换图层：{_selectedLayer?.Name ?? "(无)"}");
                }
            }
        }

        public bool HasSelectedLayer => SelectedLayer != null;

        // 自动根据是否存在选择集决定处理范围，无需用户勾选

        private bool _useConstantAngle = true;
        public bool UseConstantAngle
        {
            get => _useConstantAngle;
            set
            {
                if (SetProperty(ref _useConstantAngle, value))
                {
                    if (value)
                    {
                        if (UseFieldAngle) UseFieldAngle = false;
                        AppendLog("角度模式：统一角度(度)");
                    }
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }

        private bool _useFieldAngle = false;
        public bool UseFieldAngle
        {
            get => _useFieldAngle;
            set
            {
                if (SetProperty(ref _useFieldAngle, value))
                {
                    if (value)
                    {
                        if (UseConstantAngle) UseConstantAngle = false;
                        AppendLog("角度模式：按字段角度(度)");
                    }
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }

        private double _constantAngleDegrees = 0.0;
        public double ConstantAngleDegrees
        {
            get => _constantAngleDegrees;
            set
            {
                if (SetProperty(ref _constantAngleDegrees, value))
                {
                    AppendLog($"统一角度(度)：{value}");
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }

        private string _angleDirection = "逆时针"; // 统一方向设置：逆时针为正、顺时针为负
        public string AngleDirection
        {
            get => _angleDirection;
            set
            {
                if (SetProperty(ref _angleDirection, value))
                {
                    AppendLog($"方向：{value}（逆时针为正，顺时针为负）");
                }
            }
        }

        private ObservableCollection<string> _numericFields = new ObservableCollection<string>();
        public ObservableCollection<string> NumericFields
        {
            get => _numericFields;
            set => SetProperty(ref _numericFields, value);
        }

        private string _selectedAngleField;
        public string SelectedAngleField
        {
            get => _selectedAngleField;
            set
            {
                if (SetProperty(ref _selectedAngleField, value))
                {
                    AppendLog($"角度字段：{value}");
                    NotifyPropertyChanged(() => CanProcess);
                }
            }
        }

        // 删除字段正方向，统一使用 AngleDirection

        private string _lineAnchor = "中点"; // 起点/中点/终点
        public string LineAnchor
        {
            get => _lineAnchor;
            set
            {
                if (SetProperty(ref _lineAnchor, value))
                {
                    string msg = value == "起点" ? "线锚点：起点（以线首顶点为旋转中心）" :
                                  value == "中点" ? "线锚点：中点（沿折线总长度的半程点）" :
                                  value == "终点" ? "线锚点：终点（以线末顶点为旋转中心）" : $"线锚点：{value}";
                    AppendLog(msg);
                }
            }
        }

        private string _polygonAnchor = "质心"; // 质心/起点
        public string PolygonAnchor
        {
            get => _polygonAnchor;
            set
            {
                if (SetProperty(ref _polygonAnchor, value))
                {
                    string msg = value == "质心" ? "面锚点：质心（几何质心）" :
                                  value == "标签点" ? "面锚点：标签点（内部代表点）" :
                                  value == "包络中心" ? "面锚点：包络中心（外包矩形中心）" :
                                  value == "左下角" ? "面锚点：左下角（外包矩形）" :
                                  value == "左上角" ? "面锚点：左上角（外包矩形）" :
                                  value == "右下角" ? "面锚点：右下角（外包矩形）" :
                                  value == "右上角" ? "面锚点：右上角（外包矩形）" :
                                  value == "起点" ? "面锚点：起点（外环第一段起点）" : $"面锚点：{value}";
                    AppendLog(msg);
                }
            }
        }

        private bool _isProcessing = false;
        public bool IsProcessing
        {
            get => _isProcessing;
            set { SetProperty(ref _isProcessing, value); NotifyPropertyChanged(() => CanProcess); }
        }

        public bool CanProcess
        {
            get
            {
                if (IsProcessing || !HasSelectedLayer)
                    return false;
                if (UseConstantAngle)
                    return true;
                if (UseFieldAngle)
                    return !string.IsNullOrEmpty(SelectedAngleField);
                return false;
            }
        }

        private int _progress = 0;
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private bool _isProgressIndeterminate = false;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        private bool _cancelRequested = false;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
        }

        private string _statusMessage = "就绪";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _logContent = string.Empty;
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        private string _selectionInfo = "未选择要素，将处理全部";
        public string SelectionInfo
        {
            get => _selectionInfo;
            set => SetProperty(ref _selectionInfo, value);
        }
        private int _lastSelectionCount = -1;

        // 根据当前图层类型控制锚点UI显示
        private bool _isLineLayer;
        public bool IsLineLayer
        {
            get => _isLineLayer;
            set => SetProperty(ref _isLineLayer, value);
        }

        private bool _isPolygonLayer;
        public bool IsPolygonLayer
        {
            get => _isPolygonLayer;
            set => SetProperty(ref _isPolygonLayer, value);
        }
        #endregion

        #region 命令
        public ICommand RefreshLayersCommand => new RelayCommand(() => RefreshLayers());
        public ICommand RunCommand => new RelayCommand(async () => await RunAsync(), () => CanProcess);
        public ICommand CancelCommand => new RelayCommand(() => Cancel(), () => IsProcessing);
        public ICommand ShowHelpCommand => new RelayCommand(() => ShowHelp());
        #endregion

        #region 构造
        public RotateGeometryDockPaneViewModel()
        {
            RefreshLayers();
            MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
        }
        #endregion

        #region 公共方法
        public void RefreshLayers()
        {
            Task.Run(async () =>
            {
                try
                {
                    var tempLayers = new List<FeatureLayer>();
                    await QueuedTask.Run(() =>
                    {
                        var map = MapView.Active?.Map;
                        if (map == null) return;
                        var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>();
                        foreach (var fl in layers)
                        {
                            try
                            {
                                var def = fl.GetFeatureClass()?.GetDefinition();
                                var gtype = def?.GetShapeType();
                                if (gtype == GeometryType.Polygon || gtype == GeometryType.Polyline)
                                {
                                    tempLayers.Add(fl);
                                }
                            }
                            catch { }
                        }
                    });

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        FeatureLayers.Clear();
                        foreach (var l in tempLayers) FeatureLayers.Add(l);
                        if (FeatureLayers.Count > 0 && SelectedLayer == null)
                            SelectedLayer = FeatureLayers[0];
                        StatusMessage = $"已加载 {FeatureLayers.Count} 个线/面图层";
                        UpdateSelectionInfo();
                        UpdateGeometryTypeFlags();
                        AppendLog($"已刷新图层列表：{FeatureLayers.Count} 个线/面图层");
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                        AppendLog($"错误: {ex.Message}");
                    });
                }
            });
        }
        #endregion

        #region 私有方法
        private void LoadNumericFields()
        {
            NumericFields.Clear();
            if (SelectedLayer == null) return;
            Task.Run(async () =>
            {
                try
                {
                    var names = new List<string>();
                    await QueuedTask.Run(() =>
                    {
                        using (var table = SelectedLayer.GetTable())
                        {
                            var def = table.GetDefinition();
                            foreach (var f in def.GetFields())
                            {
                                if (f.FieldType == FieldType.Double || f.FieldType == FieldType.Single || f.FieldType == FieldType.Integer || f.FieldType == FieldType.SmallInteger)
                                {
                                    names.Add(f.Name);
                                }
                            }
                        }
                    });
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var n in names) NumericFields.Add(n);
                        if (NumericFields.Count > 0 && string.IsNullOrEmpty(SelectedAngleField))
                            SelectedAngleField = NumericFields[0];
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => AppendLog($"读取字段失败: {ex.Message}"));
                }
            });
        }

        private double GetAngleDegreesForRow(Row row)
        {
            if (UseConstantAngle)
            {
                var angle = ConstantAngleDegrees;
                if (string.Equals(AngleDirection, "顺时针")) angle = -Math.Abs(angle);
                else angle = Math.Abs(angle);
                return angle;
            }
            else if (UseFieldAngle && !string.IsNullOrEmpty(SelectedAngleField))
            {
                try
                {
                    var obj = row[SelectedAngleField];
                    if (obj == null) return 0.0;
                    double ang;
                    if (obj is double d) ang = d;
                    else if (obj is float f) ang = f;
                    else if (obj is int i) ang = i;
                    else if (obj is short s) ang = s;
                    else if (!double.TryParse(Convert.ToString(obj, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out ang))
                        return 0.0;
                    if (string.Equals(AngleDirection, "顺时针")) ang = -Math.Abs(ang); else ang = Math.Abs(ang);
                    return ang;
                }
                catch { return 0.0; }
            }
            return 0.0;
        }

        private async Task RunAsync()
        {
            if (!HasSelectedLayer) return;
            IsProcessing = true;
            CancelRequested = false;
            Progress = 0;
            IsProgressIndeterminate = false;
            StatusMessage = "处理中...";
            AppendLog("开始旋转...");
            // 运行前输出当前配置摘要
            AppendLog($"配置：图层 = {SelectedLayer?.Name}");
            AppendLog($"配置：角度模式 = {(UseConstantAngle ? "统一角度" : "按字段角度")}; 方向 = {AngleDirection}");
            if (UseConstantAngle) AppendLog($"配置：统一角度(度) = {ConstantAngleDegrees}");
            else AppendLog($"配置：角度字段 = {SelectedAngleField}");
            AppendLog($"配置：线锚点 = {LineAnchor}; 面锚点 = {PolygonAnchor}");
            AppendLog($"配置：{SelectionInfo}");

            await QueuedTask.Run(async () =>
            {
                try
                {
                    var layer = SelectedLayer;
                    var fc = layer.GetFeatureClass();

                    // 准备游标
                    bool useSelection = layer.SelectionCount > 0;
                    RowCursor cursor = useSelection ? layer.GetSelection().Search(null, false) : fc.Search(null, false);

                    using (cursor)
                    {
                        var editOp = new ArcGIS.Desktop.Editing.EditOperation { Name = "旋转图形[线/面]" };
                        int processed = 0;
                        int total = 0;

                        // 先统计总数
                        total = useSelection ? layer.SelectionCount : (int)fc.GetCount();

                        while (cursor.MoveNext())
                        {
                            if (CancelRequested) break;
                            using (var row = cursor.Current)
                            {
                                var feature = row as Feature;
                                var shape = feature?.GetShape();
                                if (shape == null || shape.IsEmpty)
                                    continue;

                                double angleDeg = GetAngleDegreesForRow(row);
                                if (Math.Abs(angleDeg) < 1e-12)
                                {
                                    processed++;
                                    continue;
                                }
                                double angleRad = angleDeg * Math.PI / 180.0;

                                Geometry rotated = null;
                                if (shape is Polyline pl)
                                {
                                    var anchor = GetPolylineAnchorPoint(pl, LineAnchor);
                                    if (anchor != null)
                                        rotated = GeometryEngine.Instance.Rotate(pl, anchor, angleRad);
                                }
                                else if (shape is Polygon pg)
                                {
                                    var anchor = GetPolygonAnchorPoint(pg, PolygonAnchor);
                                    if (anchor != null)
                                        rotated = GeometryEngine.Instance.Rotate(pg, anchor, angleRad);
                                }

                                if (rotated != null)
                                {
                                    editOp.Modify(layer, row.GetObjectID(), rotated);
                                }

                                processed++;
                                int prog = total > 0 ? (int)(processed * 100.0 / total) : 0;
                                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { Progress = prog; StatusMessage = $"已处理 {processed}/{total}"; });
                            }
                        }

                        if (!CancelRequested)
                        {
                            var result = await editOp.ExecuteAsync();
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                            {
                                if (result)
                                {
                                    AppendLog("旋转完成");
                                    StatusMessage = "完成";
                                    Progress = 100;
                                    UpdateSelectionInfo();
                                }
                                else
                                {
                                    AppendLog("旋转失败：编辑操作未执行");
                                    StatusMessage = "失败";
                                }
                            });
                        }
                        else
                        {
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { AppendLog("已取消"); StatusMessage = "已取消"; });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { AppendLog("错误: " + ex.Message); StatusMessage = "错误"; });
                }
                finally
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() => { IsProcessing = false; Progress = 0; });
                }
            });
        }

        private void Cancel()
        {
            CancelRequested = true;
            StatusMessage = "正在取消...";
        }

        private MapPoint GetPolylineAnchorPoint(Polyline pl, string anchor)
        {
            try
            {
                var pts = pl.Points?.ToList();
                if (pts == null || pts.Count == 0) return null;

                if (anchor == "起点")
                {
                    return pts.First();
                }
                else if (anchor == "终点")
                {
                    return pts.Last();
                }
                else // 中点
                {
                    // 沿顶点线性插值的半程点
                    double totalLen = 0;
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        totalLen += Distance2D(pts[i], pts[i + 1]);
                    }
                    if (totalLen <= 0) return pts.First();
                    double half = totalLen / 2.0;
                    double acc = 0;
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        double seg = Distance2D(pts[i], pts[i + 1]);
                        if (acc + seg >= half)
                        {
                            double t = (half - acc) / seg;
                            return Interpolate(pts[i], pts[i + 1], t);
                        }
                        acc += seg;
                    }
                    return pts.Last();
                }
            }
            catch { return null; }
        }

        private MapPoint GetPolygonAnchorPoint(Polygon pg, string anchor)
        {
            try
            {
                if (anchor == "起点")
                {
                    var part = pg.Parts?.FirstOrDefault();
                    var seg = part?.FirstOrDefault();
                    return seg?.StartPoint;
                }
                else if (anchor == "质心")
                {
                    var c = GeometryEngine.Instance.Centroid(pg) as MapPoint;
                    if (c == null || double.IsNaN(c.X) || double.IsNaN(c.Y))
                        c = GeometryEngine.Instance.LabelPoint(pg) as MapPoint;
                    return c;
                }
                else if (anchor == "标签点")
                {
                    return GeometryEngine.Instance.LabelPoint(pg) as MapPoint;
                }
                else if (anchor == "包络中心")
                {
                    var env = pg.Extent;
                    if (env == null) return null;
                    var sr = pg.SpatialReference;
                    return MapPointBuilderEx.CreateMapPoint((env.XMin + env.XMax) / 2.0, (env.YMin + env.YMax) / 2.0, sr);
                }
                else if (anchor == "左下角" || anchor == "左上角" || anchor == "右下角" || anchor == "右上角")
                {
                    var env = pg.Extent;
                    if (env == null) return null;
                    var sr = pg.SpatialReference;
                    double x = 0, y = 0;
                    switch (anchor)
                    {
                        case "左下角": x = env.XMin; y = env.YMin; break;
                        case "左上角": x = env.XMin; y = env.YMax; break;
                        case "右下角": x = env.XMax; y = env.YMin; break;
                        case "右上角": x = env.XMax; y = env.YMax; break;
                    }
                    return MapPointBuilderEx.CreateMapPoint(x, y, sr);
                }
                // 默认回退：质心
                var c2 = GeometryEngine.Instance.Centroid(pg) as MapPoint;
                if (c2 == null || double.IsNaN(c2.X) || double.IsNaN(c2.Y))
                    c2 = GeometryEngine.Instance.LabelPoint(pg) as MapPoint;
                return c2;
            }
            catch { return null; }
        }

        private static double Distance2D(MapPoint a, MapPoint b)
        {
            double dx = b.X - a.X; double dy = b.Y - a.Y; return Math.Sqrt(dx * dx + dy * dy);
        }

        private static MapPoint Interpolate(MapPoint a, MapPoint b, double t)
        {
            var sr = a.SpatialReference ?? b.SpatialReference;
            double x = a.X + (b.X - a.X) * t;
            double y = a.Y + (b.Y - a.Y) * t;
            return MapPointBuilderEx.CreateMapPoint(x, y, sr);
        }

        private void AppendLog(string message)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{ts}] {message}\n";
        }

        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            // 仅在当前视图和当前图层相关时更新，简单起见总是刷新文本
            UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            try
            {
                int count = 0;
                if (SelectedLayer != null)
                {
                    count = SelectedLayer.SelectionCount;
                }
                SelectionInfo = count > 0 ? $"已选择 {count} 个要素，将处理选择集" : "未选择要素，将处理全部";
                if (count != _lastSelectionCount)
                {
                    AppendLog(count > 0 ? $"选择集更新：已选择 {count} 个要素，将处理选择集" : "选择集更新：未选择要素，将处理全部");
                    _lastSelectionCount = count;
                }
            }
            catch
            {
                SelectionInfo = "未选择要素，将处理全部";
            }
        }

        private void UpdateGeometryTypeFlags()
        {
            if (SelectedLayer == null)
            {
                IsLineLayer = false;
                IsPolygonLayer = false;
                return;
            }
            Task.Run(async () =>
            {
                GeometryType? gtype = null;
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        gtype = SelectedLayer.GetFeatureClass()?.GetDefinition()?.GetShapeType();
                    }
                    catch { gtype = null; }
                });
                bool isLine = gtype == GeometryType.Polyline;
                bool isPolygon = gtype == GeometryType.Polygon;
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    bool beforeLine = IsLineLayer;
                    bool beforePolygon = IsPolygonLayer;
                    IsLineLayer = isLine;
                    IsPolygonLayer = isPolygon;
                    if (beforeLine != isLine || beforePolygon != isPolygon)
                    {
                        var typeName = isLine ? "线" : isPolygon ? "面" : "其他";
                        AppendLog($"当前图层类型：{typeName}；已根据类型显示相应锚点选项");
                    }
                }));
            });
        }
        private void ShowHelp()
        {
            var help = "旋转图形[线/面] 使用说明\n\n" +
                       "功能：\n" +
                       "- 对选定图层的线或面几何做旋转；\n" +
                       "- 支持统一角度或按字段角度（度）；\n" +
                       "- 方向可选逆时针/顺时针（统一设置）；\n" +
                       "- 线锚点：起点/中点/终点；\n" +
                       "- 面锚点：质心/标签点/包络中心/左下角/左上角/右下角/右上角/起点。\n\n" +
                       "注意：\n" +
                       "- 建议在编辑会话中操作；\n" +
                       "- 角度单位为度；逆时针为正；顺时针为负。";
            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(help, "帮助");
        }
        #endregion
    }

    /// <summary>
    /// 简易命令实现（本命名空间内独立一份，避免跨命名空间引用）
    /// </summary>
    internal class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public RelayCommand(Action execute, Func<bool> canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public event EventHandler CanExecuteChanged { add { CommandManager.RequerySuggested += value; } remove { CommandManager.RequerySuggested -= value; } }
        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();
    }
}
