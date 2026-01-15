using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.Edit.Boundary.MapBoundaryPointLineGenerator
{
    /// <summary>
    /// 地图生成界址点线 ViewModel
    /// 在布局的图形图层上生成界址点、界址线、点号、边长标注
    /// </summary>
    internal class MapBoundaryPointLineGeneratorDockPaneViewModel : PropertyChangedBase
    {
        #region 属性
        private bool _isProcessing;
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
        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrEmpty(SelectedLayoutName);

        // 面要素图层
        private ObservableCollection<FeatureLayer> _polygonLayers = new();
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }

        private FeatureLayer _selectedPolygonLayer;
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                LoadAvailableFields();
                UpdateSelectionInfo();
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // 唯一字段
        private ObservableCollection<string> _availableFields = new();
        public ObservableCollection<string> AvailableFields
        {
            get => _availableFields;
            set => SetProperty(ref _availableFields, value);
        }

        private string _selectedUniqueField;
        public string SelectedUniqueField
        {
            get => _selectedUniqueField;
            set => SetProperty(ref _selectedUniqueField, value);
        }

        // 布局列表（存储名称字符串）
        private ObservableCollection<string> _layoutNames = new();
        public ObservableCollection<string> LayoutNames
        {
            get => _layoutNames;
            set => SetProperty(ref _layoutNames, value);
        }

        private string _selectedLayoutName;
        public string SelectedLayoutName
        {
            get => _selectedLayoutName;
            set
            {
                SetProperty(ref _selectedLayoutName, value);
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // 获取当前选中的 Layout 对象
        private Layout GetSelectedLayout()
        {
            if (string.IsNullOrEmpty(SelectedLayoutName)) return null;
            var items = Project.Current?.GetItems<LayoutProjectItem>();
            var layoutItem = items?.FirstOrDefault(i => i.Name == SelectedLayoutName);
            return layoutItem?.GetLayout();
        }

        // 选择集相关
        private bool _useSelection = true;
        public bool UseSelection
        {
            get => _useSelection;
            set => SetProperty(ref _useSelection, value);
        }

        private bool _hasSelection;
        public bool HasSelection
        {
            get => _hasSelection;
            set => SetProperty(ref _hasSelection, value);
        }

        private int _selectedCount;
        public int SelectedCount
        {
            get => _selectedCount;
            set => SetProperty(ref _selectedCount, value);
        }

        // ========== 界址点设置 ==========
        private bool _enableBoundaryPoints = true;
        public bool EnableBoundaryPoints
        {
            get => _enableBoundaryPoints;
            set => SetProperty(ref _enableBoundaryPoints, value);
        }

        private double _boundaryPointSize = 6.0;
        public double BoundaryPointSize
        {
            get => _boundaryPointSize;
            set => SetProperty(ref _boundaryPointSize, value);
        }

        // ========== 界址线设置 ==========
        private bool _enableBoundaryLines = false;
        public bool EnableBoundaryLines
        {
            get => _enableBoundaryLines;
            set => SetProperty(ref _enableBoundaryLines, value);
        }

        private double _boundaryLineWidth = 1.0;
        public double BoundaryLineWidth
        {
            get => _boundaryLineWidth;
            set => SetProperty(ref _boundaryLineWidth, value);
        }

        // ========== 点号设置 ==========
        private bool _enablePointLabels = true;
        public bool EnablePointLabels
        {
            get => _enablePointLabels;
            set => SetProperty(ref _enablePointLabels, value);
        }

        private string _pointLabelPrefix = "J";
        public string PointLabelPrefix
        {
            get => _pointLabelPrefix;
            set => SetProperty(ref _pointLabelPrefix, value);
        }

        private string _pointLabelSuffix = "";
        public string PointLabelSuffix
        {
            get => _pointLabelSuffix;
            set => SetProperty(ref _pointLabelSuffix, value);
        }

        private double _pointLabelDistance = 3.0;
        public double PointLabelDistance
        {
            get => _pointLabelDistance;
            set => SetProperty(ref _pointLabelDistance, value);
        }

        private double _pointLabelSize = 12.0;
        public double PointLabelSize
        {
            get => _pointLabelSize;
            set => SetProperty(ref _pointLabelSize, value);
        }

        public ObservableCollection<string> OverlapModes { get; } = new() { "压盖隐藏", "压盖避让" };

        private string _pointLabelOverlapMode = "压盖隐藏";
        public string PointLabelOverlapMode
        {
            get => _pointLabelOverlapMode;
            set => SetProperty(ref _pointLabelOverlapMode, value);
        }

        // ========== 边长标注设置 ==========
        private bool _enableEdgeLabels = false;
        public bool EnableEdgeLabels
        {
            get => _enableEdgeLabels;
            set => SetProperty(ref _enableEdgeLabels, value);
        }

        private string _edgeLabelPrefix = "";
        public string EdgeLabelPrefix
        {
            get => _edgeLabelPrefix;
            set => SetProperty(ref _edgeLabelPrefix, value);
        }

        private string _edgeLabelSuffix = "";
        public string EdgeLabelSuffix
        {
            get => _edgeLabelSuffix;
            set => SetProperty(ref _edgeLabelSuffix, value);
        }

        private int _edgeLabelDecimal = 2;
        public int EdgeLabelDecimal
        {
            get => _edgeLabelDecimal;
            set => SetProperty(ref _edgeLabelDecimal, Math.Max(0, Math.Min(6, value)));
        }

        private bool _edgeLabelPadZeros = false;
        public bool EdgeLabelPadZeros
        {
            get => _edgeLabelPadZeros;
            set => SetProperty(ref _edgeLabelPadZeros, value);
        }

        private double _edgeLabelDistance = 2.0;
        public double EdgeLabelDistance
        {
            get => _edgeLabelDistance;
            set => SetProperty(ref _edgeLabelDistance, value);
        }

        private double _edgeLabelSize = 10.0;
        public double EdgeLabelSize
        {
            get => _edgeLabelSize;
            set => SetProperty(ref _edgeLabelSize, value);
        }

        private string _edgeLabelOverlapMode = "压盖隐藏";
        public string EdgeLabelOverlapMode
        {
            get => _edgeLabelOverlapMode;
            set => SetProperty(ref _edgeLabelOverlapMode, value);
        }

        // 状态与日志
        private string _statusMessage = "准备就绪";
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
        private readonly StringBuilder _logBuilder = new();

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
        #endregion

        #region 命令
        private ICommand _runCommand;
        public ICommand RunCommand => _runCommand ??= new RelayCommand(Execute, () => CanProcess);

        private ICommand _refreshCommand;
        public ICommand RefreshCommand => _refreshCommand ??= new RelayCommand(RefreshAll);

        private ICommand _createAllTemplatesCommand;
        public ICommand CreateAllTemplatesCommand => _createAllTemplatesCommand ??= new RelayCommand(CreateAllTemplates);

        private ICommand _showHelpCommand;
        public ICommand ShowHelpCommand => _showHelpCommand ??= new RelayCommand(ShowHelp);
        #endregion

        #region 构造函数
        public MapBoundaryPointLineGeneratorDockPaneViewModel()
        {
            LoadPolygonLayers();
            LoadLayouts();

            // 订阅事件
            MapViewInitializedEvent.Subscribe((args) => { LoadPolygonLayers(); });
            ActiveMapViewChangedEvent.Subscribe((args) => { LoadPolygonLayers(); });
            MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
        }
        #endregion

        #region 数据加载
        private void LoadPolygonLayers()
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var list = await LayerUtils.GetPolygonLayersAsync();
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var fl in list) PolygonLayers.Add(fl);
                        if (PolygonLayers.Count > 0) SelectedPolygonLayer = PolygonLayers[0];
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        StatusMessage = $"加载图层失败: {ex.Message}");
                }
            });
        }

        private void LoadLayouts()
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    var layoutNames = await QueuedTask.Run(() =>
                    {
                        var items = Project.Current?.GetItems<LayoutProjectItem>();
                        return items?.Select(i => i.Name).ToList() ?? new List<string>();
                    });

                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        LayoutNames.Clear();
                        foreach (var name in layoutNames) LayoutNames.Add(name);
                        if (LayoutNames.Count > 0) SelectedLayoutName = LayoutNames[0];
                    });
                }
                catch (Exception ex)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        StatusMessage = $"加载布局失败: {ex.Message}");
                }
            });
        }

        private void LoadAvailableFields()
        {
            AvailableFields.Clear();
            if (SelectedPolygonLayer == null) return;

            QueuedTask.Run(() =>
            {
                try
                {
                    using var table = SelectedPolygonLayer.GetTable();
                    var fields = table?.GetDefinition()?.GetFields();
                    if (fields == null) return;

                    var textFields = fields.Where(f => f.FieldType == FieldType.String || f.FieldType == FieldType.Integer || f.FieldType == FieldType.SmallInteger)
                        .Select(f => f.Name).ToList();

                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        foreach (var n in textFields) AvailableFields.Add(n);
                        SelectedUniqueField = AvailableFields.FirstOrDefault(n =>
                            n.Equals("ZDDM", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("DM", StringComparison.OrdinalIgnoreCase) ||
                            n.Equals("FID", StringComparison.OrdinalIgnoreCase) ||
                            n.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase)) ?? AvailableFields.FirstOrDefault();
                    });
                }
                catch (Exception ex)
                {
                    LogError($"读取字段失败: {ex.Message}");
                }
            });
        }

        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                var info = await SelectionUtils.GetSelectionInfoAsync(SelectedPolygonLayer);
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    UseSelection = SelectionUtils.RecommendUseSelection(UseSelection, info.HasSelection);
                    HasSelection = info.HasSelection;
                    SelectedCount = info.Count;
                });
            });
        }

        private void RefreshAll()
        {
            LoadPolygonLayers();
            LoadLayouts();
        }
        #endregion

        #region 模板创建
        /// <summary>
        /// 一次性创建所有模板
        /// </summary>
        private async void CreateAllTemplates()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var layout = GetSelectedLayout();
                    if (layout == null)
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            MessageBox.Show("请先选择布局", "提示"));
                        return;
                    }

                    var createdList = new List<string>();
                    var existsList = new List<string>();
                    var redColor = CIMColor.CreateRGBColor(255, 0, 0);

                    // XF_JZD - 界址点（实心圆无边框）
                    if (layout.FindElement("XF_JZD") == null)
                    {
                        var pointSymbol = SymbolFactory.Instance.ConstructPointSymbol(redColor, 6, SimpleMarkerStyle.Circle);
                        // 移除边框
                        if (pointSymbol.SymbolLayers != null)
                        {
                            foreach (var layer in pointSymbol.SymbolLayers.OfType<CIMVectorMarker>())
                            {
                                if (layer.MarkerGraphics != null)
                                {
                                    foreach (var mg in layer.MarkerGraphics)
                                    {
                                        if (mg.Symbol is CIMPolygonSymbol polySymbol)
                                        {
                                            polySymbol.SymbolLayers = polySymbol.SymbolLayers?
                                                .Where(sl => sl is CIMSolidFill).ToArray();
                                        }
                                    }
                                }
                            }
                        }
                        var pointGraphic = new CIMPointGraphic
                        {
                            Location = MapPointBuilderEx.CreateMapPoint(-35, -35),
                            Symbol = pointSymbol.MakeSymbolReference()
                        };
                        ElementFactory.Instance.CreateGraphicElement(layout, pointGraphic, "XF_JZD", true, new ElementInfo());
                        createdList.Add("XF_JZD(界址点)");
                    }
                    else existsList.Add("XF_JZD");

                    // XF_JZX - 界址线
                    if (layout.FindElement("XF_JZX") == null)
                    {
                        var lineSymbol = SymbolFactory.Instance.ConstructLineSymbol(redColor, 1.0, SimpleLineStyle.Solid);
                        var polyline = PolylineBuilderEx.CreatePolyline(new[] {
                            MapPointBuilderEx.CreateMapPoint(-45, -35),
                            MapPointBuilderEx.CreateMapPoint(-25, -35)
                        });
                        var lineGraphic = new CIMLineGraphic { Line = polyline, Symbol = lineSymbol.MakeSymbolReference() };
                        ElementFactory.Instance.CreateGraphicElement(layout, lineGraphic, "XF_JZX", true, new ElementInfo());
                        createdList.Add("XF_JZX(界址线)");
                    }
                    else existsList.Add("XF_JZX");

                    // XF_DH - 点号（宋体）
                    if (layout.FindElement("XF_DH") == null)
                    {
                        var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, 12, "宋体", "Regular");
                        var textGraphic = new CIMTextGraphic
                        {
                            Shape = MapPointBuilderEx.CreateMapPoint(-35, -40),
                            Text = "J1",
                            Symbol = textSymbol.MakeSymbolReference()
                        };
                        ElementFactory.Instance.CreateGraphicElement(layout, textGraphic, "XF_DH", true, new ElementInfo());
                        createdList.Add("XF_DH(点号)");
                    }
                    else existsList.Add("XF_DH");

                    // XF_BC - 边长（宋体）
                    if (layout.FindElement("XF_BC") == null)
                    {
                        var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, 10, "宋体", "Regular");
                        var textGraphic = new CIMTextGraphic
                        {
                            Shape = MapPointBuilderEx.CreateMapPoint(-35, -50),
                            Text = "12.34",
                            Symbol = textSymbol.MakeSymbolReference()
                        };
                        ElementFactory.Instance.CreateGraphicElement(layout, textGraphic, "XF_BC", true, new ElementInfo());
                        createdList.Add("XF_BC(边长)");
                    }
                    else existsList.Add("XF_BC");

                    // 汇总提示
                    var msg = new StringBuilder();
                    if (createdList.Count > 0)
                        msg.AppendLine($"已创建: {string.Join(", ", createdList)}");
                    if (existsList.Count > 0)
                        msg.AppendLine($"已存在: {string.Join(", ", existsList)}");
                    msg.AppendLine("\n模板位于版面外（左下角负坐标），可调整样式。");

                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        MessageBox.Show(msg.ToString(), createdList.Count > 0 ? "已创建模板" : "提示"));
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建模板失败: {ex.Message}", "错误");
            }
        }
        #endregion

        #region 执行生成
        private async void Execute()
        {
            if (IsProcessing) return;
            IsProcessing = true;
            IsProgressIndeterminate = true;
            Progress = 0;
            ClearLog();
            StatusMessage = "正在生成...";

            try
            {
                await QueuedTask.Run(() =>
                {
                    if (SelectedPolygonLayer == null)
                    {
                        LogError("未选择面图层");
                        return;
                    }
                    
                    var layout = GetSelectedLayout();
                    if (layout == null)
                    {
                        LogError("未选择布局");
                        return;
                    }

                    // 根据图层所在地图查找包含该地图的地图框
                    var layerMap = SelectedPolygonLayer.Map;
                    if (layerMap == null)
                    {
                        LogError("图层没有关联地图");
                        return;
                    }

                    // 在布局中查找包含该地图的地图框
                    MapFrame mapFrame = layout.Elements.OfType<MapFrame>()
                        .FirstOrDefault(mf => mf.Map == layerMap);
                    
                    if (mapFrame == null)
                    {
                        // 如果找不到匹配的，尝试使用第一个地图框
                        mapFrame = layout.Elements.OfType<MapFrame>().FirstOrDefault();
                    }
                    
                    if (mapFrame == null)
                    {
                        LogError("布局中未找到地图框");
                        return;
                    }

                    var map = mapFrame.Map;
                    if (map == null)
                    {
                        LogError("地图框没有关联地图");
                        return;
                    }
                    
                    LogInfo($"使用地图框: {mapFrame.Name}");

                    // 获取或创建图形图层
                    var graphicsLayer = GetOrCreateGraphicsLayer(map, "XF_界址点线标注");
                    if (graphicsLayer == null)
                    {
                        LogError("无法创建图形图层");
                        return;
                    }

                    // 清除现有元素
                    ClearGraphicsLayerElements(graphicsLayer);

                    // 获取要素
                    bool useSelection = UseSelection && SelectionUtils.GetSelectionCount(SelectedPolygonLayer) > 0;
                    using var cursor = SelectionUtils.GetSelectionOrAllCursor(SelectedPolygonLayer, useSelection, new QueryFilter(), false);

                    int featureIndex = 0;
                    int total = useSelection ? SelectedCount : GetFeatureCount(SelectedPolygonLayer);
                    LogInfo($"开始处理 {total} 个要素...");

                    // 计算比例尺
                    double mapScale = mapFrame.Camera.Scale;
                    double pointLabelDistanceInMapUnits = PointLabelDistance * mapScale / 1000.0;
                    double edgeLabelDistanceInMapUnits = EdgeLabelDistance * mapScale / 1000.0;

                    // 获取符号
                    var pointSymbol = GetPointSymbolFromTemplate(layout, BoundaryPointSize);
                    var lineSymbol = GetLineSymbolFromTemplate(layout, BoundaryLineWidth);
                    var pointTextSymbol = EnablePointLabels ? GetTextSymbolFromTemplate(layout, PointLabelSize, "XF_DH") : null;
                    var edgeTextSymbol = EnableEdgeLabels ? GetTextSymbolFromTemplate(layout, EdgeLabelSize, "XF_BC") : null;

                    // 压盖检测列表
                    var placedLabels = new List<(double X, double Y, double Width, double Height, string Type)>();
                    double ptToMm = 0.35;

                    while (cursor.MoveNext())
                    {
                        var feature = cursor.Current as Feature;
                        var polygon = feature?.GetShape() as Polygon;
                        if (polygon == null) continue;

                        var uniqueValue = GetStringSafe(feature, SelectedUniqueField) ?? featureIndex.ToString();
                        LogInfo($"处理要素: {uniqueValue}");

                        // 提取界址点坐标
                        var points = polygon.Points.ToList();
                        if (points.Count > 1 &&
                            Math.Abs(points[0].X - points[points.Count - 1].X) < 0.001 &&
                            Math.Abs(points[0].Y - points[points.Count - 1].Y) < 0.001)
                        {
                            points.RemoveAt(points.Count - 1);
                        }

                        // 生成界址点、界址线、点号、边长
                        for (int i = 0; i < points.Count; i++)
                        {
                            var point = points[i];
                            var mapPoint = MapPointBuilderEx.CreateMapPoint(point.X, point.Y, polygon.SpatialReference);

                            // 界址点
                            if (EnableBoundaryPoints)
                            {
                                var pointGraphic = new CIMPointGraphic
                                {
                                    Location = mapPoint,
                                    Symbol = pointSymbol.MakeSymbolReference()
                                };
                                graphicsLayer.AddElement(pointGraphic);
                            }

                            // 界址线
                            if (EnableBoundaryLines)
                            {
                                var nextPoint = points[(i + 1) % points.Count];
                                var nextMapPoint = MapPointBuilderEx.CreateMapPoint(nextPoint.X, nextPoint.Y, polygon.SpatialReference);
                                var polyline = PolylineBuilderEx.CreatePolyline(new[] { mapPoint, nextMapPoint }, polygon.SpatialReference);
                                var lineGraphic = new CIMLineGraphic
                                {
                                    Line = polyline,
                                    Symbol = lineSymbol.MakeSymbolReference()
                                };
                                graphicsLayer.AddElement(lineGraphic);
                            }

                            // 点号
                            if (EnablePointLabels && pointTextSymbol != null)
                            {
                                string labelText = FormatPointLabel(i + 1);
                                double charWidth = PointLabelSize * ptToMm * 0.5;
                                double charHeight = PointLabelSize * ptToMm * 0.8;
                                double labelWidth = labelText.Length * charWidth * mapScale / 1000.0;
                                double labelHeight = charHeight * mapScale / 1000.0;

                                var candidatePositions = GetCandidateLabelPositions8Dir(points, i, pointLabelDistanceInMapUnits, polygon.SpatialReference);
                                MapPoint bestPosition = candidatePositions[0];

                                if (PointLabelOverlapMode == "压盖隐藏")
                                {
                                    bool shouldPlace = !placedLabels.Any(placed =>
                                        IsOverlapping(bestPosition.X, bestPosition.Y, labelWidth, labelHeight,
                                            placed.X, placed.Y, placed.Width, placed.Height));

                                    if (shouldPlace)
                                    {
                                        placedLabels.Add((bestPosition.X, bestPosition.Y, labelWidth, labelHeight, "point"));
                                        CreateTextGraphic(graphicsLayer, bestPosition, labelText, pointTextSymbol);
                                    }
                                }
                                else
                                {
                                    foreach (var candidate in candidatePositions)
                                    {
                                        if (!placedLabels.Any(placed =>
                                            IsOverlapping(candidate.X, candidate.Y, labelWidth, labelHeight,
                                                placed.X, placed.Y, placed.Width, placed.Height)))
                                        {
                                            bestPosition = candidate;
                                            break;
                                        }
                                    }
                                    placedLabels.Add((bestPosition.X, bestPosition.Y, labelWidth, labelHeight, "point"));
                                    CreateTextGraphic(graphicsLayer, bestPosition, labelText, pointTextSymbol);
                                }
                            }

                            // 边长标注
                            if (EnableEdgeLabels && edgeTextSymbol != null)
                            {
                                var p1 = points[i];
                                var p2 = points[(i + 1) % points.Count];
                                double dx = p2.X - p1.X;
                                double dy = p2.Y - p1.Y;
                                double edgeLength = Math.Sqrt(dx * dx + dy * dy);
                                double angleRad = Math.Atan2(dy, dx);
                                double angleDeg = angleRad * 180.0 / Math.PI;
                                if (angleDeg > 90) angleDeg -= 180;
                                if (angleDeg < -90) angleDeg += 180;

                                string edgeLabelText = FormatEdgeLabel(edgeLength);
                                double edgeCharWidth = EdgeLabelSize * ptToMm * 0.5;
                                double edgeCharHeight = EdgeLabelSize * ptToMm * 0.8;
                                double edgeLabelWidth = edgeLabelText.Length * edgeCharWidth * mapScale / 1000.0;
                                double edgeLabelHeight = edgeCharHeight * mapScale / 1000.0;

                                var edgeCandidates = GetEdgeLabelCandidatePositions(p1, p2, edgeLabelDistanceInMapUnits, points, polygon.SpatialReference);
                                MapPoint bestEdgePos = edgeCandidates[0];

                                if (EdgeLabelOverlapMode == "压盖隐藏")
                                {
                                    bool shouldPlace = !placedLabels.Any(placed =>
                                        IsOverlapping(bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight,
                                            placed.X, placed.Y, placed.Width, placed.Height));

                                    if (shouldPlace)
                                    {
                                        placedLabels.Add((bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight, "edge"));
                                        CreateRotatedTextGraphic(graphicsLayer, bestEdgePos, edgeLabelText, edgeTextSymbol, angleDeg);
                                    }
                                }
                                else
                                {
                                    foreach (var candidate in edgeCandidates)
                                    {
                                        if (!placedLabels.Any(placed =>
                                            IsOverlapping(candidate.X, candidate.Y, edgeLabelWidth, edgeLabelHeight,
                                                placed.X, placed.Y, placed.Width, placed.Height)))
                                        {
                                            bestEdgePos = candidate;
                                            break;
                                        }
                                    }
                                    placedLabels.Add((bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight, "edge"));
                                    CreateRotatedTextGraphic(graphicsLayer, bestEdgePos, edgeLabelText, edgeTextSymbol, angleDeg);
                                }
                            }
                        }

                        featureIndex++;
                        UpdateProgress(featureIndex, total);
                    }

                    LogInfo($"生成完成，共处理 {featureIndex} 个要素");
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        Progress = 100;
                        IsProgressIndeterminate = false;
                        StatusMessage = "完成";
                    });
                });
            }
            catch (Exception ex)
            {
                LogError($"执行失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
            }
        }
        #endregion

        #region 辅助方法
        private string FormatPointLabel(int index)
        {
            return $"{PointLabelPrefix}{index}{PointLabelSuffix}";
        }

        private string FormatEdgeLabel(double length)
        {
            string numStr;
            if (EdgeLabelPadZeros)
            {
                numStr = length.ToString($"F{EdgeLabelDecimal}");
            }
            else
            {
                numStr = Math.Round(length, EdgeLabelDecimal).ToString();
            }
            return $"{EdgeLabelPrefix}{numStr}{EdgeLabelSuffix}";
        }

        private GraphicsLayer GetOrCreateGraphicsLayer(Map map, string layerName)
        {
            try
            {
                var existingLayer = map.GetLayersAsFlattenedList()
                    .OfType<GraphicsLayer>()
                    .FirstOrDefault(l => l.Name == layerName);

                if (existingLayer != null)
                    return existingLayer;

                var graphicsLayerParams = new GraphicsLayerCreationParams { Name = layerName };
                return LayerFactory.Instance.CreateLayer<GraphicsLayer>(graphicsLayerParams, map);
            }
            catch (Exception ex)
            {
                LogError($"创建图形图层失败: {ex.Message}");
                return null;
            }
        }

        private void ClearGraphicsLayerElements(GraphicsLayer graphicsLayer)
        {
            try
            {
                var elements = graphicsLayer.GetElementsAsFlattenedList();
                if (elements != null && elements.Any())
                {
                    graphicsLayer.RemoveElements(elements);
                }
            }
            catch { }
        }

        private CIMPointSymbol GetPointSymbolFromTemplate(Layout layout, double userSize)
        {
            CIMPointSymbol symbol = null;
            try
            {
                var template = layout.FindElement("XF_JZD") as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMPointGraphic pointGraphic)
                    {
                        symbol = pointGraphic.Symbol?.Symbol as CIMPointSymbol;
                    }
                }
            }
            catch { }

            if (symbol == null)
            {
                var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                symbol = SymbolFactory.Instance.ConstructPointSymbol(redColor, userSize, SimpleMarkerStyle.Circle);
            }
            else
            {
                symbol = symbol.Clone() as CIMPointSymbol;
                symbol?.SetSize(userSize);
            }
            return symbol;
        }

        private CIMLineSymbol GetLineSymbolFromTemplate(Layout layout, double userWidth)
        {
            CIMLineSymbol symbol = null;
            try
            {
                var template = layout.FindElement("XF_JZX") as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMLineGraphic lineGraphic)
                    {
                        symbol = lineGraphic.Symbol?.Symbol as CIMLineSymbol;
                    }
                }
            }
            catch { }

            if (symbol == null)
            {
                var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                symbol = SymbolFactory.Instance.ConstructLineSymbol(redColor, userWidth, SimpleLineStyle.Solid);
            }
            else
            {
                symbol = symbol.Clone() as CIMLineSymbol;
                symbol?.SetSize(userWidth);
            }
            return symbol;
        }

        private CIMTextSymbol GetTextSymbolFromTemplate(Layout layout, double userSize, string templateName)
        {
            CIMTextSymbol symbol = null;
            try
            {
                var template = layout.FindElement(templateName) as GraphicElement;
                if (template != null)
                {
                    var graphic = template.GetGraphic();
                    if (graphic is CIMTextGraphic textGraphic)
                    {
                        symbol = textGraphic.Symbol?.Symbol as CIMTextSymbol;
                    }
                }
            }
            catch { }

            if (symbol == null)
            {
                var redColor = CIMColor.CreateRGBColor(255, 0, 0);
                symbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, userSize, "Arial", "Regular");
            }
            else
            {
                symbol = symbol.Clone() as CIMTextSymbol;
                symbol?.SetSize(userSize);
            }

            symbol.HorizontalAlignment = HorizontalAlignment.Center;
            symbol.VerticalAlignment = VerticalAlignment.Center;
            return symbol;
        }

        private void CreateTextGraphic(GraphicsLayer layer, MapPoint position, string text, CIMTextSymbol symbol)
        {
            var textGraphic = new CIMTextGraphic
            {
                Shape = position,
                Text = text,
                Symbol = symbol.MakeSymbolReference()
            };
            layer.AddElement(textGraphic);
        }

        private void CreateRotatedTextGraphic(GraphicsLayer layer, MapPoint position, string text, CIMTextSymbol symbol, double angleDegrees)
        {
            var rotatedSymbol = symbol.Clone() as CIMTextSymbol;
            if (rotatedSymbol != null)
            {
                rotatedSymbol.Angle = angleDegrees;
            }

            var textGraphic = new CIMTextGraphic
            {
                Shape = position,
                Text = text,
                Symbol = (rotatedSymbol ?? symbol).MakeSymbolReference()
            };
            layer.AddElement(textGraphic);
        }

        private bool IsOverlapping(double x1, double y1, double w1, double h1,
            double x2, double y2, double w2, double h2)
        {
            return Math.Abs(x1 - x2) < (w1 + w2) / 2 * 0.9 &&
                   Math.Abs(y1 - y2) < (h1 + h2) / 2 * 0.9;
        }

        private List<MapPoint> GetCandidateLabelPositions8Dir(List<MapPoint> points, int index, double distance, SpatialReference sr)
        {
            var candidates = new List<MapPoint>();
            int count = points.Count;
            var current = points[index];

            // 首选位置：角平分线外侧
            var primaryPos = CalculateLabelPosition(points, index, distance, sr);
            candidates.Add(primaryPos);

            // 8方向候选位置
            double sqrt2 = Math.Sqrt(2) / 2;
            var directions = new (double dx, double dy)[]
            {
                (sqrt2, sqrt2), (-sqrt2, sqrt2), (sqrt2, -sqrt2), (-sqrt2, -sqrt2),
                (1, 0), (0, 1), (-1, 0), (0, -1)
            };

            double[] distanceFactors = { 1.0, 1.3, 1.6, 2.0 };
            foreach (var factor in distanceFactors)
            {
                double d = distance * factor;
                foreach (var (dx, dy) in directions)
                {
                    candidates.Add(MapPointBuilderEx.CreateMapPoint(current.X + dx * d, current.Y + dy * d, sr));
                }
            }

            return candidates;
        }

        private MapPoint CalculateLabelPosition(List<MapPoint> points, int index, double distance, SpatialReference sr)
        {
            int count = points.Count;
            var current = points[index];
            var prev = points[(index - 1 + count) % count];
            var next = points[(index + 1) % count];

            double v1x = prev.X - current.X;
            double v1y = prev.Y - current.Y;
            double v2x = next.X - current.X;
            double v2y = next.Y - current.Y;

            double len1 = Math.Sqrt(v1x * v1x + v1y * v1y);
            double len2 = Math.Sqrt(v2x * v2x + v2y * v2y);
            if (len1 > 0.0001) { v1x /= len1; v1y /= len1; }
            if (len2 > 0.0001) { v2x /= len2; v2y /= len2; }

            double bisectX = v1x + v2x;
            double bisectY = v1y + v2y;
            double bisectLen = Math.Sqrt(bisectX * bisectX + bisectY * bisectY);

            if (bisectLen < 0.0001)
            {
                bisectX = -v1y;
                bisectY = v1x;
                bisectLen = 1.0;
            }
            else
            {
                bisectX /= bisectLen;
                bisectY /= bisectLen;
            }

            double testDist = distance * 0.1;
            double testX = current.X + bisectX * testDist;
            double testY = current.Y + bisectY * testDist;

            bool isInside = IsPointInPolygon(testX, testY, points);

            double outX = isInside ? -bisectX : bisectX;
            double outY = isInside ? -bisectY : bisectY;

            return MapPointBuilderEx.CreateMapPoint(current.X + outX * distance, current.Y + outY * distance, sr);
        }

        private bool IsPointInPolygon(double x, double y, List<MapPoint> polygon)
        {
            int count = polygon.Count;
            bool inside = false;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                double xi = polygon[i].X, yi = polygon[i].Y;
                double xj = polygon[j].X, yj = polygon[j].Y;

                if (((yi > y) != (yj > y)) &&
                    (x < (xj - xi) * (y - yi) / (yj - yi) + xi))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private List<MapPoint> GetEdgeLabelCandidatePositions(MapPoint p1, MapPoint p2, double distance, List<MapPoint> points, SpatialReference sr)
        {
            var candidates = new List<MapPoint>();

            double midX = (p1.X + p2.X) / 2;
            double midY = (p1.Y + p2.Y) / 2;

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 0.0001) len = 1;

            double perpX = -dy / len;
            double perpY = dx / len;

            // 测试哪个方向是外侧
            double testX = midX + perpX * distance * 0.1;
            double testY = midY + perpY * distance * 0.1;
            bool isInside = IsPointInPolygon(testX, testY, points);

            double outPerpX = isInside ? -perpX : perpX;
            double outPerpY = isInside ? -perpY : perpY;

            double[] distanceFactors = { 1.0, 1.3, 1.6, 2.0 };
            foreach (var factor in distanceFactors)
            {
                double d = distance * factor;
                candidates.Add(MapPointBuilderEx.CreateMapPoint(midX + outPerpX * d, midY + outPerpY * d, sr));
                candidates.Add(MapPointBuilderEx.CreateMapPoint(midX - outPerpX * d, midY - outPerpY * d, sr));
            }

            return candidates;
        }

        private string GetStringSafe(Feature feature, string fieldName)
        {
            if (feature == null || string.IsNullOrEmpty(fieldName)) return null;
            try
            {
                var value = feature[fieldName];
                return value?.ToString();
            }
            catch { return null; }
        }

        private int GetFeatureCount(FeatureLayer layer)
        {
            try
            {
                using var table = layer.GetTable();
                using var cursor = table.Search();
                int count = 0;
                while (cursor.MoveNext()) count++;
                return count;
            }
            catch { return 0; }
        }

        private void UpdateProgress(int current, int total)
        {
            if (total <= 0) return;
            int pct = (int)(current * 100.0 / total);
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Progress = pct;
                IsProgressIndeterminate = false;
            });
        }
        #endregion

        #region 日志
        private void LogInfo(string msg)
        {
            _logBuilder.AppendLine($"[信息] {msg}");
            System.Windows.Application.Current?.Dispatcher.Invoke(() => LogContent = _logBuilder.ToString());
        }

        private void LogError(string msg)
        {
            _logBuilder.AppendLine($"[错误] {msg}");
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                LogContent = _logBuilder.ToString();
                StatusMessage = msg;
            });
        }

        private void ClearLog()
        {
            _logBuilder.Clear();
            LogContent = string.Empty;
        }

        private void ShowHelp()
        {
            MessageBox.Show(
                "地图生成界址点线\n\n" +
                "功能说明：\n" +
                "在布局的图形图层上生成界址点、界址线、点号、边长标注。\n\n" +
                "使用步骤：\n" +
                "1. 选择面要素图层\n" +
                "2. 选择唯一字段（可选，用于标识）\n" +
                "3. 选择目标布局和地图框\n" +
                "4. 配置界址点、界址线、点号、边长设置\n" +
                "5. 点击\"生成模板\"创建符号模板（可选，用于自定义样式）\n" +
                "6. 点击\"开始\"生成\n\n" +
                "模板说明：\n" +
                "- XF_JZD: 界址点符号模板\n" +
                "- XF_JZX: 界址线符号模板\n" +
                "- XF_DH: 点号文字模板\n" +
                "- XF_BC: 边长文字模板",
                "帮助");
        }
        #endregion
    }
}
