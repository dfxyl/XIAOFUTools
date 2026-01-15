using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core;
using System.Windows;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework.Threading.Tasks;

using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace XIAOFUTools.Tools.AreaSplit
{
    internal class AreaSplitTool : MapTool
    {
        private bool _isDrawing = false;
        // 保存用户点击确认的顶点（主草图），顺序存放
        private List<MapPoint> _confirmedPoints = new List<MapPoint>();
        // 用于存储动态预览的文本覆盖物，键由候选块中心点生成
        private Dictionary<string, (Polygon poly, IDisposable overlay)> _candidateOverlays = new Dictionary<string, (Polygon, IDisposable)>();
        // 用于存储其他全局覆盖（如最终预览时刷新全部） 
        private List<IDisposable> _graphicOverlays = new List<IDisposable>();

        public AreaSplitTool()
        {
            IsSketchTool = true;
            SketchType = SketchGeometryType.Line;
            SketchOutputMode = SketchOutputMode.Map;
            UseSnapping = true; // 默认启用捕捉环境
        }

        #region 工具激活与停用

        protected override async Task OnToolActivateAsync(bool active)
        {
            await QueuedTask.Run(async () =>
            {
                // 检查选择集是否只有一个多边形要素
                var selection = MapView.Active.Map.GetSelection();
                int totalSelected = selection.ToDictionary().Values.Sum(v => v.Count);
                if (totalSelected != 1)
                {
                    MessageBox.Show("请先选择单个多边形要素！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await FrameworkApplication.SetCurrentToolAsync("esri_mapping_selectByRectangleTool");
                    });
                    return;
                }



                _isDrawing = true;
                _confirmedPoints.Clear();
                _candidateOverlays.Clear();
            });
            await base.OnToolActivateAsync(active);
        }

        protected override Task OnToolDeactivateAsync(bool hasMapViewChanged)
        {
            return QueuedTask.Run(() =>
            {
                ClearAllOverlays();
                _isDrawing = false;
                _confirmedPoints.Clear();
                _candidateOverlays.Clear();
            });
        }

        #endregion

        #region 草图修改与确认

        /// <summary>
        /// 当草图修改时调用（用户点击确认顶点或撤销时）  
        /// 这里简单使用当前草图所有点作为最新确认的顶点（主草图），并刷新预览。
        /// </summary>
        protected override async Task<bool> OnSketchModifiedAsync()
        {
            return await QueuedTask.Run(async () =>
            {
                if (!_isDrawing)
                    return true;

                var currentSketch = await GetCurrentSketchAsync() as Polyline;
                if (currentSketch != null && currentSketch.PointCount > 0)
                {
                    // 更新主草图：使用当前草图所有点
                    _confirmedPoints = currentSketch.Points.ToList();
                    // 刷新预览（候选段为 null，此时只用确认的切割线预览）
                    await UpdateCandidatePreview(null);
                }
                else
                {
                    // 没有草图则清理预览
                    ClearAllOverlays();
                }
                return true;
            });
        }

        #endregion

        #region 鼠标移动动态预览

        /// <summary>
        /// 鼠标移动时动态更新预览  
        /// 如果当前草图中新增加点（点数大于 _confirmedPoints 数量），直接使用当前草图作为候选切割线；  
        /// 否则，如果已有确认顶点，则用最后确认顶点与当前鼠标位置构造候选段，
        /// 与确认点拼接构成完整候选切割线，进行预览计算。
        /// </summary>
        protected override void OnToolMouseMove(MapViewMouseEventArgs e)
        {
            if (_isDrawing)
            {
                QueuedTask.Run(async () =>
                {
                    Polyline candidateSegment = null;
                    // 获取当前草图（用户正在绘制的部分）
                    Polyline currentSketch = null;
                    try { currentSketch = await GetCurrentSketchAsync() as Polyline; }
                    catch (Exception) { }
                    MapPoint mouseMapPoint = MapView.Active.ClientToMap(e.ClientPoint);

                    if (currentSketch != null)
                    {
                        if (currentSketch.PointCount > _confirmedPoints.Count)
                        {
                            // 用户已添加新点：直接用整个当前草图
                            candidateSegment = currentSketch;
                        }
                        else if (_confirmedPoints.Count > 0 && mouseMapPoint != null)
                        {
                            // 否则，使用最后确认点和当前鼠标位置构造候选段，并拼接到主草图后
                            List<MapPoint> pts = new List<MapPoint>(_confirmedPoints);
                            pts.Add(mouseMapPoint);
                            candidateSegment = PolylineBuilderEx.CreatePolyline(pts, _confirmedPoints.Last().SpatialReference);
                        }
                    }
                    else if (_confirmedPoints.Count > 0 && mouseMapPoint != null)
                    {
                        candidateSegment = PolylineBuilderEx.CreatePolyline(
                            new List<MapPoint> { _confirmedPoints.Last(), mouseMapPoint },
                            _confirmedPoints.Last().SpatialReference);
                    }
                    await UpdateCandidatePreview(candidateSegment);
                });
            }
            base.OnToolMouseMove(e);
        }

        #endregion

        #region 分割预览与最终分割

        /// <summary>
        /// 更新候选预览
        /// 先以 _confirmedPoints 构造"已确认切割线"，对原始图斑进行分割得到初步块；
        /// 然后如果 candidateSegment 不为空，则在已确认切割结果上再应用候选切割，得到最终预览块；
        /// 最后只更新那些受到候选线影响的块的文本覆盖，不刷新未变化的块。
        /// </summary>
        /// <param name="candidateSegment">候选切割线，可为 null</param>
        private async Task UpdateCandidatePreview(Polyline candidateSegment)
        {
            await QueuedTask.Run(() =>
            {
                try
                {
                    // 获取选中图斑要素
                    var selection = MapView.Active.Map.GetSelection();
                    FeatureLayer featureLayer = null;
                    long oid = -1;
                    foreach (var kvp in selection.ToDictionary())
                    {
                        featureLayer = kvp.Key as FeatureLayer;
                        oid = kvp.Value.First();
                        break;
                    }
                    if (featureLayer == null)
                        return;

                    using (var table = featureLayer.GetTable())
                    using (var cursor = table.Search(new QueryFilter { ObjectIDs = new[] { oid } }, false))
                    {
                        if (!cursor.MoveNext())
                            return;
                        using (var feature = cursor.Current as Feature)
                        {
                            var originalPolygon = feature.GetShape() as Polygon;
                            if (originalPolygon == null)
                                return;

                            // 先对原始图斑用 _confirmedPoints 构成的多折线进行切割（如果有足够的点）
                            List<Geometry> pieces = new List<Geometry>() { originalPolygon };
                            if (_confirmedPoints.Count >= 2)
                            {
                                // 构造多折线：连续连线的 _confirmedPoints
                                Polyline confirmedCutLine = PolylineBuilderEx.CreatePolyline(_confirmedPoints, _confirmedPoints.Last().SpatialReference);
                                var projLine = GeometryEngine.Instance.Project(confirmedCutLine, originalPolygon.SpatialReference) as Polyline;
                                if (projLine != null)
                                {
                                    List<Geometry> tempPieces = new List<Geometry>();
                                    foreach (var piece in pieces)
                                    {
                                        if (piece is Polygon polyPiece)
                                        {
                                            var cuts = GeometryEngine.Instance.Cut(polyPiece, projLine);
                                            if (cuts.Count > 0)
                                                tempPieces.AddRange(cuts);
                                            else
                                                tempPieces.Add(polyPiece);
                                        }
                                        else
                                            tempPieces.Add(piece);
                                    }
                                    pieces = tempPieces;
                                }
                            }

                            // 如果有候选切割线，则在当前分块上再应用候选切割
                            if (candidateSegment != null && candidateSegment.PointCount >= 2)
                            {
                                var projCandidate = GeometryEngine.Instance.Project(candidateSegment, originalPolygon.SpatialReference) as Polyline;
                                if (projCandidate != null)
                                {
                                    List<Geometry> candidatePieces = new List<Geometry>();
                                    foreach (var piece in pieces)
                                    {
                                        if (piece is Polygon polyPiece)
                                        {
                                            var cuts = GeometryEngine.Instance.Cut(polyPiece, projCandidate);
                                            if (cuts.Count > 0)
                                                candidatePieces.AddRange(cuts);
                                            else
                                                candidatePieces.Add(polyPiece);
                                        }
                                        else
                                            candidatePieces.Add(piece);
                                    }
                                    pieces = candidatePieces;
                                }
                            }

                            // 更新覆盖文本——只刷新那些受到候选线影响的块
                            // 用候选块的中心点作为键（保留两位小数）
                            var newKeys = new HashSet<string>();
                            foreach (var geom in pieces)
                            {
                                if (geom is Polygon poly && !poly.IsEmpty)
                                {
                                    MapPoint center = GeometryEngine.Instance.LabelPoint(poly) as MapPoint;
                                    if (center == null)
                                        continue;

                                    string key = GenerateKey(center);
                                    newKeys.Add(key);
                                    double area = GeometryEngine.Instance.Area(poly);
                                    double hectares = area * 0.0001;
                                    double mu = area * 0.0015;
                                    string areaText = $"{area:F2} ㎡\n{hectares:F4} ha\n{mu:F4} mu";

                                    // 判断是否已有覆盖且面积基本相同（容差 1%）
                                    if (_candidateOverlays.ContainsKey(key))
                                    {
                                        double oldArea = GeometryEngine.Instance.Area(_candidateOverlays[key].poly);
                                        if (Math.Abs(oldArea - area) / area < 0.01)
                                        {
                                            // 差别在 1%以内，不更新
                                            continue;
                                        }
                                        else
                                        {
                                            // 删除旧覆盖
                                            _candidateOverlays[key].overlay.Dispose();
                                            _candidateOverlays.Remove(key);
                                        }
                                    }

                                    // 创建新的文本覆盖
                                    var redColor = ColorFactory.Instance.CreateRGBColor(255, 0, 0);
                                    var textSymbol = SymbolFactory.Instance.ConstructTextSymbol(redColor, 8, "Arial", "Regular");
                                    textSymbol.HorizontalAlignment = ArcGIS.Core.CIM.HorizontalAlignment.Center;
                                    textSymbol.VerticalAlignment = ArcGIS.Core.CIM.VerticalAlignment.Center;
                                    var textGraphic = new CIMTextGraphic
                                    {
                                        Text = areaText,
                                        Shape = center,
                                        Symbol = textSymbol.MakeSymbolReference()
                                    };
                                    IDisposable overlay = MapView.Active.AddOverlay(textGraphic);
                                    _candidateOverlays[key] = (poly, overlay);
                                }
                            }

                            // 对于之前存在但此次不再出现的块，移除覆盖
                            var keysToRemove = _candidateOverlays.Keys.Except(newKeys).ToList();
                            foreach (var key in keysToRemove)
                            {
                                _candidateOverlays[key].overlay.Dispose();
                                _candidateOverlays.Remove(key);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"预览错误: {ex.Message}", "错误");
                }
            });
        }

        /// <summary>
        /// 当草图绘制完成（用户双击结束）时
        /// 以 _confirmedPoints 构造最终切割线，对选中图斑进行分割，并清理预览。
        /// </summary>
        protected override async Task<bool> OnSketchCompleteAsync(Geometry geometry)
        {
            return await QueuedTask.Run(async () =>
            {
                try
                {
                    _isDrawing = false;
                    // 更新最终确认的顶点
                    var finalSketch = geometry as Polyline;
                    if (finalSketch != null && finalSketch.PointCount >= 2)
                    {
                        _confirmedPoints = finalSketch.Points.ToList();
                    }

                    // 构造最终切割线
                    Polyline finalCutLine = null;
                    if (_confirmedPoints.Count >= 2)
                    {
                        finalCutLine = PolylineBuilderEx.CreatePolyline(_confirmedPoints, _confirmedPoints.Last().SpatialReference);
                    }

                    // 调用预览更新显示最终效果（这一步会刷新候选预览）
                    await UpdateCandidatePreview(null);

                    // 执行最终分割操作
                    var selection = MapView.Active.Map.GetSelection();
                    FeatureLayer featureLayer = null;
                    long oid = -1;
                    foreach (var kvp in selection.ToDictionary())
                    {
                        featureLayer = kvp.Key as FeatureLayer;
                        oid = kvp.Value.First();
                        break;
                    }
                    if (featureLayer == null)
                        return false;

                    using (var table = featureLayer.GetTable())
                    using (var cursor = table.Search(new QueryFilter { ObjectIDs = new[] { oid } }, false))
                    {
                        if (cursor.MoveNext())
                        {
                            using (var feature = cursor.Current as Feature)
                            {
                                var polygon = feature.GetShape() as Polygon;
                                if (polygon != null && finalCutLine != null)
                                {
                                    var projLine = GeometryEngine.Instance.Project(finalCutLine, polygon.SpatialReference) as Polyline;
                                    if (projLine == null)
                                    {
                                        MessageBox.Show("最终切割线投影失败", "错误");
                                        return false;
                                    }

                                    var editOp = new EditOperation { Name = "动态面积分割" };
                                    editOp.Split(featureLayer, oid, projLine);
                                    await editOp.ExecuteAsync();
                                }
                            }
                        }
                    }

                    ClearAllOverlays();
                    _confirmedPoints.Clear();
                    _candidateOverlays.Clear();
                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"分割失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            });
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 生成候选块键，取中心点 X、Y 保留两位小数
        /// </summary>
        private string GenerateKey(MapPoint pt)
        {
            return $"{Math.Round(pt.X, 2)}_{Math.Round(pt.Y, 2)}";
        }

        /// <summary>
        /// 清除所有覆盖（包括预览和其他）
        /// </summary>
        private void ClearAllOverlays()
        {
            foreach (var kvp in _candidateOverlays)
            {
                kvp.Value.overlay.Dispose();
            }
            _candidateOverlays.Clear();
            foreach (var overlay in _graphicOverlays)
            {
                overlay.Dispose();
            }
            _graphicOverlays.Clear();
        }

        #endregion
    }
}
