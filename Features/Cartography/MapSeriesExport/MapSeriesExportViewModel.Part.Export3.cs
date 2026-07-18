using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable;
using XIAOFUTools.Features.Cartography.MapSeriesExport.Core;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    public partial class MapSeriesExportViewModel
    {

        private void CreateIntersectTableElements(
            Layout layout,
            List<MapSeriesIntersectTableRow> rows,
            CoordinateTableSettings settings)
        {
            string timestamp = Guid.NewGuid().ToString("N");

            try
            {
                CIMPolygonSymbol rectSymbol = null;
                CIMTextSymbol textSymbol = null;

                var txTemplate = layout.FindElement("XF_TX") as GraphicElement;
                if (txTemplate?.GetGraphic() is CIMPolygonGraphic polyGraphic)
                {
                    rectSymbol = polyGraphic.Symbol?.Symbol as CIMPolygonSymbol;
                }

                var wbTemplate = layout.FindElement("XF_WB") as GraphicElement;
                if (wbTemplate?.GetGraphic() is CIMTextGraphic textGraphic)
                {
                    textSymbol = textGraphic.Symbol?.Symbol as CIMTextSymbol;
                }

                double categoryColWidth = settings.IntersectCategoryColWidth;
                double areaColWidth = settings.IntersectAreaColWidth;
                double tableWidth = categoryColWidth + areaColWidth;
                double rowHeight = settings.IntersectRowHeight;
                int lineCount = rows.Count + 2;
                double tableHeight = lineCount * rowHeight;
                var (startX, startY) = ResolveTableStartPosition(
                    layout,
                    settings,
                    settings.IntersectPlacementCorner,
                    settings.IntersectCornerOffset,
                    tableWidth,
                    tableHeight);

                var createdElements = new List<Element>();
                double currentY = startY;

                createdElements.AddRange(LayoutElementHelper.CreateTableCellSync(
                    layout,
                    $"IntersectTitle_{timestamp}",
                    (startX, currentY),
                    (tableWidth, rowHeight),
                    settings.IntersectTableTitle,
                    true,
                    rectSymbol,
                    textSymbol));
                currentY -= rowHeight;

                createdElements.AddRange(LayoutElementHelper.CreateTableCellSync(
                    layout,
                    $"IntersectHeader_Category_{timestamp}",
                    (startX, currentY),
                    (categoryColWidth, rowHeight),
                    "类别",
                    true,
                    rectSymbol,
                    textSymbol));
                createdElements.AddRange(LayoutElementHelper.CreateTableCellSync(
                    layout,
                    $"IntersectHeader_Area_{timestamp}",
                    (startX + categoryColWidth, currentY),
                    (areaColWidth, rowHeight),
                    $"面积({settings.IntersectAreaUnit})",
                    true,
                    rectSymbol,
                    textSymbol));
                currentY -= rowHeight;

                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    string prefix = row.IsTotal ? "IntersectTotal" : "IntersectData";
                    bool isHeader = row.IsTotal;
                    string areaText = row.Area.ToString($"F{settings.IntersectDecimalPlaces}");

                    createdElements.AddRange(LayoutElementHelper.CreateTableCellSync(
                        layout,
                        $"{prefix}_Category_{i}_{timestamp}",
                        (startX, currentY),
                        (categoryColWidth, rowHeight),
                        row.Category,
                        isHeader,
                        rectSymbol,
                        textSymbol));
                    createdElements.AddRange(LayoutElementHelper.CreateTableCellSync(
                        layout,
                        $"{prefix}_Area_{i}_{timestamp}",
                        (startX + categoryColWidth, currentY),
                        (areaColWidth, rowHeight),
                        areaText,
                        isHeader,
                        rectSymbol,
                        textSymbol));
                    currentY -= rowHeight;
                }

                _currentCoordinateTableGroupName = $"MapSeries_IntersectTable_{timestamp}";
                createdElements.Clear();

                var _ = layout.GetElements().ToList();
                var layoutView = LayoutView.Active;
                if (layoutView != null && layoutView.Layout == layout)
                {
                    layoutView.Refresh();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建交集表格元素失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 在地图上创建界址点标注、点号和边长
        /// </summary>
        private void CreateBoundaryPointsOnMap(Layout layout, Polygon geometry, CoordinateTableSettings settings)
        {
            try
            {
                // 获取地图框
                MapFrame mapFrame = null;
                if (!string.IsNullOrEmpty(settings.MapFrameName))
                {
                    mapFrame = layout.FindElement(settings.MapFrameName) as MapFrame;
                }
                if (mapFrame == null)
                {
                    mapFrame = layout.Elements.OfType<MapFrame>().FirstOrDefault();
                }
                
                if (mapFrame == null)
                {
                    System.Diagnostics.Debug.WriteLine("未找到地图框，无法创建界址点");
                    return;
                }
                
                var map = mapFrame.Map;
                if (map == null)
                {
                    System.Diagnostics.Debug.WriteLine("地图框没有关联地图");
                    return;
                }
                
                // 获取或创建图形图层
                var graphicsLayer = GetOrCreateGraphicsLayer(map, "XF_界址点标注");
                if (graphicsLayer == null)
                {
                    System.Diagnostics.Debug.WriteLine("无法创建图形图层");
                    return;
                }
                
                // 清除该图层上的现有元素
                ClearGraphicsLayerElements(graphicsLayer);
                
                // 提取界址点坐标（不包括闭合点）
                var points = geometry.Points.ToList();
                if (points.Count > 1 && 
                    Math.Abs(points[0].X - points[points.Count - 1].X) < 0.001 &&
                    Math.Abs(points[0].Y - points[points.Count - 1].Y) < 0.001)
                {
                    points.RemoveAt(points.Count - 1); // 移除闭合点
                }
                
                // 获取点符号（优先从模板获取）
                var pointSymbol = GetPointSymbolFromTemplate(layout, settings.BoundaryPointSize);
                
                // 获取点号文本符号（优先从模板获取）
                var pointTextSymbol = settings.EnablePointLabels ? GetTextSymbolFromTemplate(layout, settings.PointLabelSize) : null;
                
                // 获取边长文本符号（优先从模板获取）
                var edgeTextSymbol = settings.EnableEdgeLabels ? GetEdgeTextSymbolFromTemplate(layout, settings.EdgeLabelSize) : null;
                
                // 计算比例尺用于距离换算
                double mapScale = mapFrame.Camera.Scale;
                double pointLabelDistanceInMapUnits = settings.PointLabelDistance * mapScale / 1000.0;
                double edgeLabelDistanceInMapUnits = settings.EdgeLabelDistance * mapScale / 1000.0;
                
                // 用于压盖检测的已放置标注位置列表（点号和边长共用）
                var placedLabels = new List<(double X, double Y, double Width, double Height, string Type)>();
                
                // 计算文本尺寸参数
                double ptToMm = 0.35;
                
                // 为每个界址点创建点元素、点号和边长（交替生成以实现互相压盖检测）
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    var mapPoint = MapPointBuilderEx.CreateMapPoint(point.X, point.Y, geometry.SpatialReference);
                    
                    // 如果启用界址点，创建点图形元素
                    if (settings.EnableBoundaryPoints)
                    {
                        var pointGraphic = new CIMPointGraphic
                        {
                            Location = mapPoint,
                            Symbol = pointSymbol.MakeSymbolReference()
                        };
                        graphicsLayer.AddElement(pointGraphic);
                    }
                    
                    // 如果启用点号，创建点号文本
                    if (settings.EnablePointLabels && pointTextSymbol != null)
                    {
                        string labelText = settings.FormatPointLabel(i + 1);
                        
                        // 计算文本尺寸用于重叠检测
                        double charWidth = settings.PointLabelSize * ptToMm * 0.5;
                        double charHeight = settings.PointLabelSize * ptToMm * 0.8;
                        double labelWidthMm = labelText.Length * charWidth;
                        double labelWidth = labelWidthMm * mapScale / 1000.0;
                        double labelHeight = charHeight * mapScale / 1000.0;
                        
                        // 获取8方向候选位置（类CASS的选位算法）
                        var candidatePositions = GetCandidateLabelPositions8Dir(points, i, pointLabelDistanceInMapUnits, geometry.SpatialReference);
                        
                        MapPoint bestPosition = candidatePositions[0];
                        
                        // 压盖处理（检测与已有点号和边长的重叠）
                        if (settings.PointLabelOverlapMode == "压盖隐藏")
                        {
                            // 检查是否与已有标注重叠
                            bool shouldPlace = true;
                            foreach (var placed in placedLabels)
                            {
                                if (IsOverlapping(bestPosition.X, bestPosition.Y, labelWidth, labelHeight,
                                    placed.X, placed.Y, placed.Width, placed.Height))
                                {
                                    shouldPlace = false;
                                    break;
                                }
                            }
                            
                            if (shouldPlace)
                            {
                                placedLabels.Add((bestPosition.X, bestPosition.Y, labelWidth, labelHeight, "point"));
                                CreateTextGraphic(graphicsLayer, bestPosition, labelText, pointTextSymbol);
                            }
                        }
                        else if (settings.PointLabelOverlapMode == "压盖避让")
                        {
                            // 尝试找到不重叠的位置
                            foreach (var candidate in candidatePositions)
                            {
                                bool overlaps = false;
                                foreach (var placed in placedLabels)
                                {
                                    if (IsOverlapping(candidate.X, candidate.Y, labelWidth, labelHeight,
                                        placed.X, placed.Y, placed.Width, placed.Height))
                                    {
                                        overlaps = true;
                                        break;
                                    }
                                }
                                
                                if (!overlaps)
                                {
                                    bestPosition = candidate;
                                    break;
                                }
                            }
                            
                            // 始终放置（避让模式）
                            placedLabels.Add((bestPosition.X, bestPosition.Y, labelWidth, labelHeight, "point"));
                            CreateTextGraphic(graphicsLayer, bestPosition, labelText, pointTextSymbol);
                        }
                    }
                    
                    // 生成该点对应的边长标注（平行于边线，与点号交替生成实现互相检测）
                    if (settings.EnableEdgeLabels && edgeTextSymbol != null)
                    {
                        var p1 = points[i];
                        var p2 = points[(i + 1) % points.Count];
                        
                        // 计算边长
                        double dx = p2.X - p1.X;
                        double dy = p2.Y - p1.Y;
                        double edgeLength = Math.Sqrt(dx * dx + dy * dy);
                        
                        // 计算边的旋转角度（平行于边线）
                        double angleRad = Math.Atan2(dy, dx);
                        double angleDeg = angleRad * 180.0 / Math.PI;
                        
                        // 确保文字不会倒置（角度在-90到90度之间）
                        if (angleDeg > 90) angleDeg -= 180;
                        if (angleDeg < -90) angleDeg += 180;
                        
                        // 边长文本
                        string edgeLabelText = settings.FormatEdgeLabel(edgeLength);
                        
                        // 计算边长文本尺寸
                        double edgeCharWidth = settings.EdgeLabelSize * ptToMm * 0.5;
                        double edgeCharHeight = settings.EdgeLabelSize * ptToMm * 0.8;
                        double edgeLabelWidthMm = edgeLabelText.Length * edgeCharWidth;
                        double edgeLabelWidth = edgeLabelWidthMm * mapScale / 1000.0;
                        double edgeLabelHeight = edgeCharHeight * mapScale / 1000.0;
                        
                        // 获取边长标注候选位置（边的外侧）
                        var edgeCandidates = GetEdgeLabelCandidatePositions(p1, p2, edgeLabelDistanceInMapUnits, points, geometry.SpatialReference);
                        
                        MapPoint bestEdgePos = edgeCandidates[0];
                        
                        // 压盖处理（检测与已有点号和边长的重叠）
                        if (settings.EdgeLabelOverlapMode == "压盖隐藏")
                        {
                            bool shouldPlace = true;
                            foreach (var placed in placedLabels)
                            {
                                if (IsOverlapping(bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight,
                                    placed.X, placed.Y, placed.Width, placed.Height))
                                {
                                    shouldPlace = false;
                                    break;
                                }
                            }
                            
                            if (shouldPlace)
                            {
                                placedLabels.Add((bestEdgePos.X, bestEdgePos.Y, edgeLabelWidth, edgeLabelHeight, "edge"));
                                CreateRotatedTextGraphic(graphicsLayer, bestEdgePos, edgeLabelText, edgeTextSymbol, angleDeg);
                            }
                        }
                        else if (settings.EdgeLabelOverlapMode == "压盖避让")
                        {
                            foreach (var candidate in edgeCandidates)
                            {
                                bool overlaps = false;
                                foreach (var placed in placedLabels)
                                {
                                    if (IsOverlapping(candidate.X, candidate.Y, edgeLabelWidth, edgeLabelHeight,
                                        placed.X, placed.Y, placed.Width, placed.Height))
                                    {
                                        overlaps = true;
                                        break;
                                    }
                                }
                                
                                if (!overlaps)
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
                
                System.Diagnostics.Debug.WriteLine($"已创建 {points.Count} 个界址点标注");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建界址点失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建文本图形元素
        /// </summary>
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
        
        /// <summary>
        /// 创建带旋转角度的文本图形元素（用于边长标注平行于边线）
        /// </summary>
        private void CreateRotatedTextGraphic(GraphicsLayer layer, MapPoint position, string text, CIMTextSymbol symbol, double angleDegrees)
        {
            // 复制符号并设置旋转角度
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
    }
}
