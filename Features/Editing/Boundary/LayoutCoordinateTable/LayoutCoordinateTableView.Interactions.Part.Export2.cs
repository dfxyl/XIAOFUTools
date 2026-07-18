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

    private (string areaFormatted, string muAreaFormatted) FormatArea(double area, string unitChoice, int decimalPlaces, int muDecimalPlaces)
    {
        if (unitChoice == "平方米")
        {
            double main = Math.Round(area, decimalPlaces, MidpointRounding.AwayFromZero);
            string areaFormatted = main.ToString($"F{decimalPlaces}");
            string muAreaFormatted = (main * 0.0015).ToString($"F{muDecimalPlaces}");
            return (areaFormatted: areaFormatted, muAreaFormatted: muAreaFormatted);
        }

        if (unitChoice == "公顷")
        {
            double hectares = area / 10000.0;
            double main2 = Math.Round(hectares, decimalPlaces, MidpointRounding.AwayFromZero);
            string areaFormatted2 = main2.ToString($"F{decimalPlaces}");
            string muAreaFormatted2 = (main2 * 15.0).ToString($"F{muDecimalPlaces}");
            return (areaFormatted: areaFormatted2, muAreaFormatted: muAreaFormatted2);
        }

        double fallbackMain = Math.Round(area, decimalPlaces, MidpointRounding.AwayFromZero);
        return (areaFormatted: fallbackMain.ToString($"F{decimalPlaces}"), muAreaFormatted: (fallbackMain * 0.0015).ToString($"F{muDecimalPlaces}"));
    }

    private List<string> GenerateTableData(List<(double X, double Y, string XStr, string YStr)> coordinates, string titleText, string prefixStr, bool generateEdge, List<double> edgeLengths, int edgeDecimal, int rowsPerColumn, bool surveyStyle)
    {
        List<string> tableData = new List<string>();
        string headerInfo = (generateEdge ? (titleText + "\n点号    X坐标    Y坐标    边长") : (titleText + "\n点号    X坐标    Y坐标"));
        List<string> dataLines = new List<string>();
        for (int i = 0; i < coordinates.Count; i++)
        {
            int displayNo = ((i == coordinates.Count - 1) ? 1 : (i + 1));
            string pointName = $"{prefixStr}{displayNo}";
            if (generateEdge && i < edgeLengths.Count)
            {
                string edgeStr = edgeLengths[i].ToString($"F{edgeDecimal}");
                string xOut = (surveyStyle ? coordinates[i].YStr : coordinates[i].XStr);
                string yOut = (surveyStyle ? coordinates[i].XStr : coordinates[i].YStr);
                string dataLine = $"{pointName}    {xOut}    {yOut}    {edgeStr}";
                dataLines.Add(dataLine);
            }
            else
            {
                string xOut2 = (surveyStyle ? coordinates[i].YStr : coordinates[i].XStr);
                string yOut2 = (surveyStyle ? coordinates[i].XStr : coordinates[i].YStr);
                string dataLine2 = $"{pointName}    {xOut2}    {yOut2}";
                dataLines.Add(dataLine2);
            }
        }

        if (dataLines.Count <= rowsPerColumn)
        {
            string subtable = headerInfo + "\n" + string.Join("\n", dataLines);
            tableData.Add(subtable);
        }
        else
        {
            IEnumerable<string> firstChunk = dataLines.Take(rowsPerColumn);
            string firstSubtable = headerInfo + "\n" + string.Join("\n", firstChunk);
            tableData.Add(firstSubtable);
            for (int index = rowsPerColumn; index < dataLines.Count; index += rowsPerColumn - 1)
            {
                List<string> lines = new List<string>
                {
                    headerInfo.Split('\n')[0],
                    headerInfo.Split('\n')[1]
                };
                lines.Add(dataLines[index - 1]);
                IEnumerable<string> nextChunk = dataLines.Skip(index).Take(rowsPerColumn - 1);
                lines.AddRange(nextChunk);
                string subtable2 = string.Join("\n", lines);
                tableData.Add(subtable2);
            }
        }

        return tableData;
    }

    private async Task CreateTableElementsAsync(Layout layout, List<string> tableData, string uniqueValue, string areaFormatted, string muAreaFormatted, string areaUnit, double pointColWidth, double xyColWidth, double edgeColWidth, double rowHeight, bool generateEdge, string placementCorner, double cornerOffset)
    {
        _ = ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
        {
            LogMessage("正在为 " + uniqueValue + " 创建表格元素...");
        }, Array.Empty<object>());
        try
        {
            if (!QueuedTask.OnWorker)
            {
                await QueuedTask.Run((Action)delegate
                {
                    CreateTableElementsOnMCT(layout, tableData, uniqueValue, areaFormatted, muAreaFormatted, areaUnit, pointColWidth, xyColWidth, edgeColWidth, rowHeight, generateEdge, placementCorner, cornerOffset);
                }, TaskCreationOptions.None);
            }
            else
            {
                CreateTableElementsOnMCT(layout, tableData, uniqueValue, areaFormatted, muAreaFormatted, areaUnit, pointColWidth, xyColWidth, edgeColWidth, rowHeight, generateEdge, placementCorner, cornerOffset);
            }

            _ = ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
            {
                LogMessage("面要素 " + uniqueValue + " 的表格元素创建完成");
            }, Array.Empty<object>());
        }
        catch (Exception ex)
        {
            Exception ex2 = ex;
            Exception ex3 = ex2;
            _ = ((DispatcherObject)Application.Current).Dispatcher.BeginInvoke((Delegate)(Action)delegate
            {
                LogMessage("创建表格元素时发生错误: " + ex3.Message);
            }, Array.Empty<object>());
            throw;
        }
    }
    }
}
