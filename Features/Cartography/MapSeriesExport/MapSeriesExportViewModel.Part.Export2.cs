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

        private void CreateTableElements(Layout layout, List<string> tableData, string uniqueValue,
            string areaFormatted, string muAreaFormatted, CoordinateTableSettings settings, 
            Dictionary<string, string> fieldValues = null)
        {
            // 为元素名称添加GUID确保唯一性，避免多次生成时的命名冲突
            string timestamp = Guid.NewGuid().ToString("N");
            
            try
            {
                
                double pointColWidth = settings.PointColWidth;
                double xyColWidth = settings.XYColWidth;
                double edgeColWidth = settings.EdgeColWidth;
                double rowHeight = settings.RowHeight;
                bool generateEdge = settings.GenerateEdge;
                string placementCorner = settings.PlacementCorner;
                double cornerOffset = settings.CornerOffset;
                string areaUnit = settings.AreaUnit;
                
                // 查找模板并获取符号
                CIMPolygonSymbol rectSymbol = null;
                CIMTextSymbol textSymbol = null;
                
                var txTemplate = layout.FindElement("XF_TX") as GraphicElement;
                if (txTemplate != null)
                {
                    var graphic = txTemplate.GetGraphic();
                    if (graphic is CIMPolygonGraphic polyGraphic)
                    {
                        rectSymbol = polyGraphic.Symbol?.Symbol as CIMPolygonSymbol;
                    }
                }
                
                var wbTemplate = layout.FindElement("XF_WB") as GraphicElement;
                if (wbTemplate != null)
                {
                    var graphic = wbTemplate.GetGraphic();
                    if (graphic is CIMTextGraphic textGraphic)
                    {
                        textSymbol = textGraphic.Symbol?.Symbol as CIMTextSymbol;
                    }
                }
                
                double tableWidth = generateEdge ? 
                    (pointColWidth + 2 * xyColWidth + edgeColWidth) :
                    (pointColWidth + 2 * xyColWidth);
                
                double tableRowHeight = rowHeight;

                var subtableLineCounts = new List<int>();
                for (int idx = 0; idx < tableData.Count; idx++)
                {
                    var cnt = tableData[idx]
                        .Split('\n')
                        .Select(s => s.Trim())
                        .Count(s => !string.IsNullOrEmpty(s));
                    if (idx == tableData.Count - 1)
                        cnt += 1;
                    subtableLineCounts.Add(cnt);
                }
                int maxLineCount = subtableLineCounts.Count > 0 ? subtableLineCounts.Max() : 0;

                double startX;
                double startY;
                
                // 根据定位方式确定起始位置
                if (settings.UseAnchorPosition)
                {
                    // 使用锚点元素定位
                    var anchorElement = layout.FindElement(settings.AnchorElementName) as GraphicElement;
                    if (anchorElement != null)
                    {
                        var anchorBounds = anchorElement.GetBounds();
                        double anchorX = (anchorBounds.XMin + anchorBounds.XMax) / 2;
                        double anchorY = (anchorBounds.YMin + anchorBounds.YMax) / 2;
                        
                        switch (placementCorner)
                        {
                            case "左下角":
                                startX = anchorX + cornerOffset;
                                startY = anchorY + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                            case "右下角":
                                startX = anchorX - tableWidth - cornerOffset;
                                startY = anchorY + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                            case "左上角":
                                startX = anchorX + cornerOffset;
                                startY = anchorY - cornerOffset;
                                break;
                            case "右上角":
                                startX = anchorX - tableWidth - cornerOffset;
                                startY = anchorY - cornerOffset;
                                break;
                            default:
                                startX = anchorX + cornerOffset;
                                startY = anchorY + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                        }
                    }
                    else
                    {
                        // 锚点不存在，使用默认位置
                        startX = 10;
                        startY = 300;
                    }
                }
                else
                {
                    // 使用地图框定位
                    MapFrame mapFrame = null;
                    if (!string.IsNullOrEmpty(settings.MapFrameName))
                    {
                        mapFrame = layout.FindElement(settings.MapFrameName) as MapFrame;
                    }
                    if (mapFrame == null)
                    {
                        mapFrame = layout.Elements.OfType<MapFrame>().FirstOrDefault();
                    }
                    
                    if (mapFrame != null)
                    {
                        var mapBounds = mapFrame.GetBounds();
                        switch (placementCorner)
                        {
                            case "左下角":
                                startX = mapBounds.XMin + cornerOffset;
                                startY = mapBounds.YMin + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                            case "右下角":
                                startX = mapBounds.XMax - tableWidth - cornerOffset;
                                startY = mapBounds.YMin + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                            case "左上角":
                                startX = mapBounds.XMin + cornerOffset;
                                startY = mapBounds.YMax - cornerOffset;
                                break;
                            case "右上角":
                                startX = mapBounds.XMax - tableWidth - cornerOffset;
                                startY = mapBounds.YMax - cornerOffset;
                                break;
                            default:
                                startX = mapBounds.XMin + cornerOffset;
                                startY = mapBounds.YMin + cornerOffset + (maxLineCount * tableRowHeight);
                                break;
                        }
                    }
                    else
                    {
                        startX = 10;
                        startY = 300;
                    }
                }
                
                var mainGroupElements = new List<Element>();
                double currentStartX = startX;

                for (int tableIndex = 0; tableIndex < tableData.Count; tableIndex++)
                {
                    var tableText = tableData[tableIndex];
                    var lines = tableText.Split('\n');
                    var subGroupElements = new List<Element>();
                    double edgeColumnXStart = 0;
                    double firstDataRowTopY = 0;
                    bool dataRowTopYCaptured = false;
                    var dataRowTokens = new List<string[]>();
                    int thisLineCount = lines.Select(s => s.Trim()).Count(s => !string.IsNullOrEmpty(s));
                    if (tableIndex == tableData.Count - 1)
                        thisLineCount += 1;
                    double currentY = startY - (maxLineCount - thisLineCount) * tableRowHeight;

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line)) continue;

                        if (i == 0)
                        {
                            var titleElements = LayoutElementHelper.CreateTableCellSync(
                                layout, $"Title_{tableIndex}_{i}_{timestamp}",
                                (currentStartX, currentY), (tableWidth, tableRowHeight), line, true,
                                rectSymbol, textSymbol);
                            subGroupElements.AddRange(titleElements);
                            currentY -= tableRowHeight;
                        }
                        else if (i == 1)
                        {
                            var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            var colWidths = generateEdge ? 
                                new[] { pointColWidth, xyColWidth, xyColWidth, edgeColWidth } :
                                new[] { pointColWidth, xyColWidth, xyColWidth };

                            double xOffset = currentStartX;
                            for (int j = 0; j < Math.Min(tokens.Length, colWidths.Length); j++)
                            {
                                double cellWidth = colWidths[j];
                                
                                var headerElements = LayoutElementHelper.CreateTableCellSync(
                                    layout, $"Header_{tableIndex}_{i}_{j}_{timestamp}",
                                    (xOffset, currentY), (cellWidth, tableRowHeight), tokens[j], true,
                                    rectSymbol, textSymbol);
                                subGroupElements.AddRange(headerElements);
                                xOffset += cellWidth;
                            }
                            if (generateEdge)
                            {
                                edgeColumnXStart = currentStartX + pointColWidth + xyColWidth + xyColWidth;
                            }
                            firstDataRowTopY = currentY - tableRowHeight;
                            dataRowTopYCaptured = true;
                            currentY -= tableRowHeight;
                        }
                        else
                        {
                            var tokens = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (tokens.Length < 3) continue;

                            var colWidths = generateEdge ?
                                new[] { pointColWidth, xyColWidth, xyColWidth, edgeColWidth } :
                                new[] { pointColWidth, xyColWidth, xyColWidth };
                            double xOffset = currentStartX;

                            for (int j = 0; j < Math.Min(tokens.Length, colWidths.Length); j++)
                            {
                                double cellWidth = colWidths[j];
                                if (generateEdge && j == colWidths.Length - 1)
                                {
                                    xOffset += cellWidth;
                                    continue;
                                }

                                var dataElements = LayoutElementHelper.CreateTableCellSync(
                                    layout, $"Data_{tableIndex}_{i}_{j}_{timestamp}",
                                    (xOffset, currentY), (cellWidth, tableRowHeight), tokens[j], false,
                                    rectSymbol, textSymbol);
                                subGroupElements.AddRange(dataElements);
                                xOffset += cellWidth;
                            }
                            dataRowTokens.Add(tokens);
                            currentY -= tableRowHeight;
                        }
                    }

                    if (generateEdge && dataRowTopYCaptured && dataRowTokens.Count > 0)
                    {
                        double xEdge = edgeColumnXStart;

                        var padTop = LayoutElementHelper.CreateTableCellSync(
                            layout, $"EdgePadTop_{tableIndex}_{timestamp}",
                            (xEdge, firstDataRowTopY), (edgeColWidth, tableRowHeight / 2.0), string.Empty, false,
                            rectSymbol, textSymbol);
                        subGroupElements.AddRange(padTop);

                        for (int e = 0; e < dataRowTokens.Count - 1; e++)
                        {
                            bool currentIsEllipsis = IsEllipsisRow(dataRowTokens[e]);
                            bool nextIsEllipsis = IsEllipsisRow(dataRowTokens[e + 1]);
                            string edgeText = (currentIsEllipsis || nextIsEllipsis)
                                ? "•••"
                                : (dataRowTokens[e].Length > 3 ? dataRowTokens[e][3] : string.Empty);
                            double edgeTopY = firstDataRowTopY - (e + 0.5) * tableRowHeight;

                            var edgeElems = LayoutElementHelper.CreateTableCellSync(
                                layout, $"Edge_{tableIndex}_{e}_{timestamp}",
                                (xEdge, edgeTopY), (edgeColWidth, tableRowHeight), edgeText, false,
                                rectSymbol, textSymbol);
                            subGroupElements.AddRange(edgeElems);
                        }

                        double bottomPadTop = firstDataRowTopY - (dataRowTokens.Count - 0.5) * tableRowHeight;
                        var padBottom = LayoutElementHelper.CreateTableCellSync(
                            layout, $"EdgePadBottom_{tableIndex}_{timestamp}",
                            (xEdge, bottomPadTop), (edgeColWidth, tableRowHeight / 2.0), string.Empty, false,
                            rectSymbol, textSymbol);
                        subGroupElements.AddRange(padBottom);
                    }

                    // 根据面积模式生成面积行
                    if (tableIndex == tableData.Count - 1 && settings.AreaMode != "不生成")
                    {
                        string areaText;
                        if (settings.AreaMode == "自定义")
                        {
                            // 自定义模式，替换所有[字段名]占位符
                            areaText = settings.CustomAreaText ?? "";
                            
                            // 使用正则表达式查找所有 [xxx] 格式的占位符
                            var regex = new Regex(@"\[([^\]]+)\]");
                            areaText = regex.Replace(areaText, match =>
                            {
                                var fieldName = match.Groups[1].Value;
                                if (fieldValues != null && fieldValues.TryGetValue(fieldName, out var value))
                                {
                                    return value;
                                }
                                return match.Value; // 未找到字段，保持原样
                            });
                        }
                        else
                        {
                            // 自动生成模式
                            areaText = $"S={areaFormatted} {areaUnit} 合 {muAreaFormatted} 亩";
                        }
                        
                        var areaElements = LayoutElementHelper.CreateTableCellSync(
                            layout, $"Area_{tableIndex}_{timestamp}",
                            (currentStartX, currentY), (tableWidth, tableRowHeight), areaText, false,
                            rectSymbol, textSymbol);
                        subGroupElements.AddRange(areaElements);
                    }

                    // 不使用 GroupElement，直接保留元素引用用于后续清理
                    // 清空子元素列表释放引用
                    subGroupElements.Clear();
                    
                    // 每批元素创建后强制同步布局状态
                    var _ = layout.GetElements().ToList();

                    if (tableIndex < tableData.Count - 1)
                    {
                        if (placementCorner == "右下角" || placementCorner == "右上角")
                        {
                            currentStartX -= tableWidth;
                        }
                        else
                        {
                            currentStartX += tableWidth;
                        }
                    }
                }

                // 不创建主组，记录当前时间戳用于清理
                _currentCoordinateTableGroupName = $"MapSeries_CoordTable_{timestamp}";
                
                // 清空主元素列表释放引用
                mainGroupElements.Clear();
                
                // 强制同步布局状态，确保元素完全创建
                {
                    var _ = layout.GetElements().ToList();
                    
                    // 刷新布局视图确保元素正确渲染
                    var layoutView = LayoutView.Active;
                    if (layoutView != null && layoutView.Layout == layout)
                    {
                        layoutView.Refresh();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建表格元素时发生错误: {ex.Message}");
            }
        }


        private void CreateIntersectTableForCurrentPage(Layout layout, Polygon redlineGeometry, CoordinateTableSettings settings)
        {
            try
            {
                if (redlineGeometry == null ||
                    string.IsNullOrWhiteSpace(settings.IntersectLayerName) ||
                    string.IsNullOrWhiteSpace(settings.IntersectClassField))
                {
                    return;
                }

                var areas = _intersectAreaReader.Read(layout, redlineGeometry, settings);
                var rows = MapSeriesIntersectTableBuilder.BuildRows(
                    areas,
                    redlineGeometry.Area,
                    settings.IntersectAreaUnit,
                    settings.IntersectDecimalPlaces);

                if (rows.Count == 0)
                {
                    return;
                }

                CreateIntersectTableElements(layout, rows, settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"生成交集表格失败: {ex.Message}");
            }
        }

    }
}
