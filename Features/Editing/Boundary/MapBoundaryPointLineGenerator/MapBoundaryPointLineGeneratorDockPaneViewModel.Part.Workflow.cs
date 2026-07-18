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
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Features.Editing.Boundary.Shared.Core;
using XIAOFUTools.Features.Editing.Boundary.Shared.Infrastructure;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.Boundary.MapBoundaryPointLineGenerator
{
    internal partial class MapBoundaryPointLineGeneratorDockPaneViewModel
    {
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
                        var points = ArcGisBoundaryGeometryAdapter
                            .NormalizeRing(polygon.Points, 0.001)
                            .ToList();

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
                                string labelText = BoundaryLabelFormatter.FormatPointLabel(
                                    i + 1,
                                    PointLabelPrefix,
                                    PointLabelSuffix);
                                double charWidth = PointLabelSize * ptToMm * 0.5;
                                double charHeight = PointLabelSize * ptToMm * 0.8;
                                double labelWidth = labelText.Length * charWidth * mapScale / 1000.0;
                                double labelHeight = charHeight * mapScale / 1000.0;

                                var candidatePositions = ArcGisBoundaryGeometryAdapter.PlanPointLabelCandidates(
                                    points,
                                    i,
                                    pointLabelDistanceInMapUnits,
                                    polygon.SpatialReference);
                                MapPoint bestPosition = candidatePositions[0];

                                if (PointLabelOverlapMode == "压盖隐藏")
                                {
                                    bool shouldPlace = !placedLabels.Any(placed =>
                                        BoundaryLabelPlacementPlanner.IsOverlapping(
                                            new BoundaryLabelBounds(bestPosition.X, bestPosition.Y, labelWidth, labelHeight),
                                            new BoundaryLabelBounds(placed.X, placed.Y, placed.Width, placed.Height)));

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
                                            BoundaryLabelPlacementPlanner.IsOverlapping(
                                                new BoundaryLabelBounds(candidate.X, candidate.Y, labelWidth, labelHeight),
                                                new BoundaryLabelBounds(placed.X, placed.Y, placed.Width, placed.Height))))
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
                                var edgeMeasurement = BoundaryEdgeMeasurement.Measure(
                                    ArcGisBoundaryGeometryAdapter.ToBoundaryVertex(p1),
                                    ArcGisBoundaryGeometryAdapter.ToBoundaryVertex(p2));

                                string edgeLabelText = BoundaryLabelFormatter.FormatEdgeLabel(
                                    edgeMeasurement.Length,
                                    EdgeLabelDecimal,
                                    EdgeLabelPadZeros,
                                    EdgeLabelPrefix,
                                    EdgeLabelSuffix);
                                double edgeCharWidth = EdgeLabelSize * ptToMm * 0.5;
                                double edgeCharHeight = EdgeLabelSize * ptToMm * 0.8;
                                double edgeLabelWidth = edgeLabelText.Length * edgeCharWidth * mapScale / 1000.0;
                                double edgeLabelHeight = edgeCharHeight * mapScale / 1000.0;

                                var edgeCandidates = ArcGisBoundaryGeometryAdapter.PlanEdgeLabelCandidates(
                                    p1,
                                    p2,
                                    edgeLabelDistanceInMapUnits,
                                    points,
                                    polygon.SpatialReference);
                                MapPoint bestEdgePos = edgeCandidates[0];

                                if (EdgeLabelOverlapMode == "压盖隐藏")
                                {
                                    bool shouldPlace = !placedLabels.Any(placed =>
                                        BoundaryLabelPlacementPlanner.IsOverlapping(
                                            new BoundaryLabelBounds(bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight),
                                            new BoundaryLabelBounds(placed.X, placed.Y, placed.Width, placed.Height)));

                                    if (shouldPlace)
                                    {
                                        placedLabels.Add((bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight, "edge"));
                                        CreateRotatedTextGraphic(graphicsLayer, bestEdgePos, edgeLabelText, edgeTextSymbol, edgeMeasurement.TextAngleDegrees);
                                    }
                                }
                                else
                                {
                                    foreach (var candidate in edgeCandidates)
                                    {
                                        if (!placedLabels.Any(placed =>
                                            BoundaryLabelPlacementPlanner.IsOverlapping(
                                                new BoundaryLabelBounds(candidate.X, candidate.Y, edgeLabelWidth, edgeLabelHeight),
                                                new BoundaryLabelBounds(placed.X, placed.Y, placed.Width, placed.Height))))
                                        {
                                            bestEdgePos = candidate;
                                            break;
                                        }
                                    }
                                    placedLabels.Add((bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight, "edge"));
                                    CreateRotatedTextGraphic(graphicsLayer, bestEdgePos, edgeLabelText, edgeTextSymbol, edgeMeasurement.TextAngleDegrees);
                                }
                            }
                        }

                        featureIndex++;
                        UpdateProgress(featureIndex, total);
                    }

                    LogInfo($"生成完成，共处理 {featureIndex} 个要素");
                    PresentationServices.UiThread.InvokeOrRun(() =>
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
    }
}
