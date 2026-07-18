using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable
{
    public partial class LayoutCoordinateTableView
    {

    private void CreateTableElementsOnMCT(Layout layout, List<string> tableData, string uniqueValue, string areaFormatted, string muAreaFormatted, string areaUnit, double pointColWidth, double xyColWidth, double edgeColWidth, double rowHeight, bool generateEdge, string placementCorner, double cornerOffset)
    {
        string timestamp = Guid.NewGuid().ToString("N");
        Element obj = layout.FindElement("XF_TX");
        GraphicElement templateRect = (GraphicElement)(object)((obj is GraphicElement) ? obj : null);
        Element obj2 = layout.FindElement("XF_WB");
        GraphicElement templateText = (GraphicElement)(object)((obj2 is GraphicElement) ? obj2 : null);
        CIMPolygonSymbol rectSymbol = null;
        CIMTextSymbol textSymbol = null;
        if (templateRect != null)
        {
            CIMGraphic graphic = templateRect.GetGraphic();
            CIMPolygonGraphic polygonGraphic = (CIMPolygonGraphic)(object)((graphic is CIMPolygonGraphic) ? graphic : null);
            if (polygonGraphic != null && ((CIMGraphic)polygonGraphic).Symbol != null)
            {
                CIMSymbol symbol = ((CIMGraphic)polygonGraphic).Symbol.Symbol;
                rectSymbol = (CIMPolygonSymbol)(object)((symbol is CIMPolygonSymbol) ? symbol : null);
            }

            ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
            {
                LogMessage((rectSymbol != null) ? "检测到图形模板 XF_TX，将使用模板样式" : "图形模板 XF_TX 格式不正确，使用默认样式");
            }, Array.Empty<object>());
        }
        else
        {
            ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
            {
                LogMessage("未找到图形模板 XF_TX，使用默认样式");
            }, Array.Empty<object>());
        }

        if (templateText != null)
        {
            CIMGraphic graphic2 = templateText.GetGraphic();
            CIMTextGraphic textGraphic = (CIMTextGraphic)(object)((graphic2 is CIMTextGraphic) ? graphic2 : null);
            if (textGraphic != null && ((CIMGraphic)textGraphic).Symbol != null)
            {
                CIMSymbol symbol2 = ((CIMGraphic)textGraphic).Symbol.Symbol;
                textSymbol = (CIMTextSymbol)(object)((symbol2 is CIMTextSymbol) ? symbol2 : null);
            }

            ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
            {
                LogMessage((textSymbol != null) ? "检测到文本模板 XF_WB，将使用模板样式" : "文本模板 XF_WB 格式不正确，使用默认样式");
            }, Array.Empty<object>());
        }
        else
        {
            ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
            {
                LogMessage("未找到文本模板 XF_WB，使用默认样式");
            }, Array.Empty<object>());
        }

        double tableWidth = (generateEdge ? (pointColWidth + 2.0 * xyColWidth + edgeColWidth) : (pointColWidth + 2.0 * xyColWidth));
        double tableRowHeight = rowHeight;
        string widthsStr = (generateEdge ? $"{pointColWidth:F2},{xyColWidth:F2},{xyColWidth:F2},{edgeColWidth:F2}" : $"{pointColWidth:F2},{xyColWidth:F2},{xyColWidth:F2}");
        ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
        {
            LogMessage($"列宽(mm)={widthsStr}；行高(mm)={rowHeight:F2}；表宽(mm)={tableWidth:F2}");
        }, Array.Empty<object>());
        List<int> subtableLineCounts = new List<int>();
        for (int idx = 0; idx < tableData.Count; idx++)
        {
            int cnt = (
                from s in tableData[idx].Split('\n')select s.Trim()).Count((string s) => !string.IsNullOrEmpty(s));
            if (idx == tableData.Count - 1)
            {
                cnt++;
            }

            subtableLineCounts.Add(cnt);
        }

        int maxLineCount = ((subtableLineCounts.Count > 0) ? subtableLineCounts.Max() : 0);
        MapFrame mapFrame = layout.Elements.OfType<MapFrame>().FirstOrDefault();
        double startX;
        double startY;
        if (mapFrame != null)
        {
            Envelope mapBounds = ((Element)mapFrame).GetBounds(false);
            switch (placementCorner)
            {
                case "左下角":
                    startX = mapBounds.XMin + cornerOffset;
                    startY = mapBounds.YMin + cornerOffset + (double)maxLineCount * tableRowHeight;
                    break;
                case "右下角":
                    startX = mapBounds.XMax - tableWidth - cornerOffset;
                    startY = mapBounds.YMin + cornerOffset + (double)maxLineCount * tableRowHeight;
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
                    startY = mapBounds.YMin + cornerOffset + (double)maxLineCount * tableRowHeight;
                    break;
            }

            ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
            {
                LogMessage($"地图框: ({mapBounds.XMin:F2},{mapBounds.YMin:F2})-({mapBounds.XMax:F2},{mapBounds.YMax:F2}), 起始位置: ({startX:F2},{startY:F2}), 角落={placementCorner}");
            }, Array.Empty<object>());
        }
        else
        {
            startX = 10.0;
            startY = 300.0;
            ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
            {
                LogMessage($"警告: 未找到地图框，使用默认位置 ({startX},{startY})");
            }, Array.Empty<object>());
        }

        List<Element> mainGroupElements = new List<Element>();
        double currentStartX = startX;
        for (int tableIndex = 0; tableIndex < tableData.Count; tableIndex++)
        {
            string tableText = tableData[tableIndex];
            string[] lines = tableText.Split('\n');
            List<Element> subGroupElements = new List<Element>();
            double edgeColumnXStart = 0.0;
            double firstDataRowTopY = 0.0;
            bool dataRowTopYCaptured = false;
            List<string[]> dataRowTokens = new List<string[]>();
            int thisLineCount = lines.Select((string s) => s.Trim()).Count((string s) => !string.IsNullOrEmpty(s));
            if (tableIndex == tableData.Count - 1)
            {
                thisLineCount++;
            }

            double currentY = startY - (double)(maxLineCount - thisLineCount) * tableRowHeight;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                switch (i)
                {
                    case 0:
                    {
                        List<Element> titleElements = LayoutElementHelper.CreateTableCellSync(layout, $"Title_{tableIndex}_{i}_{timestamp}", (X: currentStartX, Y: currentY), (Width: tableWidth, Height: tableRowHeight), line, isHeader: true, rectSymbol, textSymbol);
                        subGroupElements.AddRange(titleElements);
                        currentY -= tableRowHeight;
                        continue;
                    }

                    case 1:
                    {
                        string[] tokens = line.Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        double[] colWidths = ((!generateEdge) ? new double[3]
                        {
                            pointColWidth,
                            xyColWidth,
                            xyColWidth
                        }

                        : new double[4]
                        {
                            pointColWidth,
                            xyColWidth,
                            xyColWidth,
                            edgeColWidth
                        }

                        );
                        double xOffset = currentStartX;
                        for (int j = 0; j < Math.Min(tokens.Length, colWidths.Length); j++)
                        {
                            double cellWidth = colWidths[j];
                            List<Element> headerElements = LayoutElementHelper.CreateTableCellSync(layout, $"Header_{tableIndex}_{i}_{j}_{timestamp}", (X: xOffset, Y: currentY), (Width: cellWidth, Height: tableRowHeight), tokens[j], isHeader: true, rectSymbol, textSymbol);
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
                        continue;
                    }
                }

                string[] tokens2 = line.Split(new char[2] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens2.Length < 3)
                {
                    continue;
                }

                double[] colWidths2 = ((!generateEdge) ? new double[3]
                {
                    pointColWidth,
                    xyColWidth,
                    xyColWidth
                }

                : new double[4]
                {
                    pointColWidth,
                    xyColWidth,
                    xyColWidth,
                    edgeColWidth
                }

                );
                double xOffset2 = currentStartX;
                for (int j2 = 0; j2 < Math.Min(tokens2.Length, colWidths2.Length); j2++)
                {
                    double cellWidth2 = colWidths2[j2];
                    if (generateEdge && j2 == colWidths2.Length - 1)
                    {
                        xOffset2 += cellWidth2;
                        continue;
                    }

                    List<Element> dataElements = LayoutElementHelper.CreateTableCellSync(layout, $"Data_{tableIndex}_{i}_{j2}_{timestamp}", (X: xOffset2, Y: currentY), (Width: cellWidth2, Height: tableRowHeight), tokens2[j2], isHeader: false, rectSymbol, textSymbol);
                    subGroupElements.AddRange(dataElements);
                    xOffset2 += cellWidth2;
                }

                dataRowTokens.Add(tokens2);
                currentY -= tableRowHeight;
            }

            if (generateEdge && dataRowTopYCaptured && dataRowTokens.Count > 0)
            {
                double xEdge = edgeColumnXStart;
                List<Element> padTop = LayoutElementHelper.CreateTableCellSync(layout, $"EdgePadTop_{tableIndex}_{timestamp}", (X: xEdge, Y: firstDataRowTopY), (Width: edgeColWidth, Height: tableRowHeight / 2.0), string.Empty, isHeader: false, rectSymbol, textSymbol);
                subGroupElements.AddRange(padTop);
                for (int e = 0; e < dataRowTokens.Count - 1; e++)
                {
                    string edgeText = ((dataRowTokens[e].Length > 3) ? dataRowTokens[e][3] : string.Empty);
                    double edgeTopY = firstDataRowTopY - ((double)e + 0.5) * tableRowHeight;
                    List<Element> edgeElems = LayoutElementHelper.CreateTableCellSync(layout, $"Edge_{tableIndex}_{e}_{timestamp}", (X: xEdge, Y: edgeTopY), (Width: edgeColWidth, Height: tableRowHeight), edgeText, isHeader: false, rectSymbol, textSymbol);
                    subGroupElements.AddRange(edgeElems);
                }

                double bottomPadTop = firstDataRowTopY - ((double)dataRowTokens.Count - 0.5) * tableRowHeight;
                List<Element> padBottom = LayoutElementHelper.CreateTableCellSync(layout, $"EdgePadBottom_{tableIndex}_{timestamp}", (X: xEdge, Y: bottomPadTop), (Width: edgeColWidth, Height: tableRowHeight / 2.0), string.Empty, isHeader: false, rectSymbol, textSymbol);
                subGroupElements.AddRange(padBottom);
            }

            if (tableIndex == tableData.Count - 1)
            {
                string areaText = $"S={areaFormatted} {areaUnit} 合 {muAreaFormatted} 亩";
                List<Element> areaElements = LayoutElementHelper.CreateTableCellSync(layout, $"Area_{tableIndex}_{timestamp}", (X: currentStartX, Y: currentY), (Width: tableWidth, Height: tableRowHeight), areaText, isHeader: false, rectSymbol, textSymbol);
                subGroupElements.AddRange(areaElements);
            }

            if (subGroupElements.Count > 0)
            {
                string subGroupName = $"Group_{uniqueValue}_Sub_{tableIndex}_{timestamp}";
                GroupElement subGroup = ElementFactory.Instance.CreateGroupElement((IElementContainer)(object)layout, (IEnumerable<Element>)subGroupElements, subGroupName, true, (ElementInfo)null);
                if (subGroup != null)
                {
                    mainGroupElements.Add((Element)(object)subGroup);
                    LayoutView lv = LayoutView.Active;
                    if (lv != null && lv.Layout == layout)
                    {
                        lv.Refresh();
                    }
                }
            }

            if (tableIndex < tableData.Count - 1)
            {
                currentStartX = ((!(placementCorner == "右下角") && !(placementCorner == "右上角")) ? (currentStartX + tableWidth) : (currentStartX - tableWidth));
            }
        }

        if (mainGroupElements.Count > 0)
        {
            string mainGroupName = "Group_" + uniqueValue + "_" + timestamp;
            GroupElement mainGroup = ElementFactory.Instance.CreateGroupElement((IElementContainer)(object)layout, (IEnumerable<Element>)mainGroupElements, mainGroupName, true, (ElementInfo)null);
            LayoutView layoutView = LayoutView.Active;
            if (layoutView != null && layoutView.Layout == layout)
            {
                layoutView.Refresh();
            }

            ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
            {
                LogMessage("坐标表组创建完成: " + mainGroupName);
            }, Array.Empty<object>());
        }
    }
    }
}
