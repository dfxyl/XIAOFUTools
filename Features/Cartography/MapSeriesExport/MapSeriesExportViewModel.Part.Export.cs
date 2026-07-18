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
using XIAOFUTools.Features.Cartography.MapSeriesExport.Infrastructure;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    public partial class MapSeriesExportViewModel
    {

        private void RebuildMapSeriesPageView()
        {
            MapSeriesPagesView = CollectionViewSource.GetDefaultView(MapSeriesPages);
            if (MapSeriesPagesView != null)
            {
                MapSeriesPagesView.Filter = MapSeriesPageFilter;
            }
            ApplyMapSeriesPageFilter();
        }

        private void ApplyMapSeriesPageFilter()
        {
            MapSeriesPagesView?.Refresh();
        }

        private bool CanExport()
        {
            return !IsRunning && SelectedLayout != null && MapSeriesPages.Count > 0 &&
                   !string.IsNullOrWhiteSpace(OutputFolder) && int.TryParse(Resolution, out int res) && res > 0;
        }


        private bool HasPageGeneratedContent()
        {
            var settings = _coordinateTableSettings;
            return settings?.EnableCoordinateTable == true ||
                   settings?.EnableBoundaryPoints == true ||
                   settings?.EnablePointLabels == true ||
                   settings?.EnableEdgeLabels == true ||
                   settings?.EnableIntersectTable == true;
        }


        private async Task Export()
        {
            try
            {
                IsRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();

                await _outputFolderStore.EnsureOutputDirectoryAsync(OutputFolder, _cancellationTokenSource.Token);

                var pagesToExport = GetPagesToExport();
                if (pagesToExport.Count == 0)
                {
                    PresentationServices.Dialogs.Show("没有要导出的页面", "提示");
                    return;
                }

                int successCount = 0;
                int totalCount = pagesToExport.Count;

                await QueuedTask.Run(async () =>
                {
                    var layout = SelectedLayout.GetLayout();
                    var mapSeries = layout?.MapSeries;
                    if (mapSeries == null || !mapSeries.Enabled)
                    {
                        PresentationServices.UiThread.InvokeOrRun(() =>
                            PresentationServices.Dialogs.Show("地图系列未启用", "错误"));
                        return;
                    }

                    if (!int.TryParse(Resolution, out int resolution)) resolution = 300;

                    foreach (var pageIndex in pagesToExport)
                    {
                        if (_cancellationTokenSource.Token.IsCancellationRequested) break;
                        try
                        {
                            if (pageIndex >= 1 && pageIndex <= mapSeries.PageCount)
                            {
                                // 切换页面前清除坐标表
                                if (HasPageGeneratedContent())
                                {
                                    ClearCoordinateTableElements(layout);
                                }
                                
                                // 切换地图系列页面
                                mapSeries.SetCurrentPageNumber(pageIndex.ToString());
                                
                                // 如果启用了坐标表生成，则生成坐标表
                                if (HasPageGeneratedContent())
                                {
                                    GenerateCoordinateTableForCurrentPage(layout, mapSeries);
                                }
                                
                                string pageName = CleanFileName(mapSeries.CurrentPageName ?? $"Page_{pageIndex}");
                                string filePath = Path.Combine(OutputFolder, $"{pageName}.{GetFileExtension()}");
                                ExportPage(layout, filePath, resolution);
                                successCount++;
                            }
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"导出页面 {pageIndex} 失败: {ex.Message}"); }
                    }
                    
                    // 导出完成后清除最后一个坐标表
                    if (HasPageGeneratedContent())
                    {
                        ClearCoordinateTableElements(layout);
                    }
                });

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                    PresentationServices.Dialogs.Show($"导出完成！成功: {successCount}/{totalCount} 页", "导出完成");
            }
            catch (Exception ex) { PresentationServices.Dialogs.Show($"导出错误：{ex.Message}", "错误"); }
            finally
            {
                IsRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private List<int> GetPagesToExport()
        {
            return MapSeriesExportPlanner.ResolvePageNumbers(
                    SelectedExportMode,
                    MapSeriesPages.Count,
                    MapSeriesPages.Where(page => page.IsSelected).Select(page => page.PageIndex),
                    PageRange)
                .ToList();
        }

        private void ExportPage(Layout layout, string filePath, int resolution)
        {
            _layoutExporter.Export(layout, SelectedFormat, filePath, resolution);
        }

        /// <summary>
        /// 为当前页面生成坐标表（同步版本，用于导出）
        /// </summary>
        private void GenerateCoordinateTableForCurrentPage(Layout layout, MapSeries mapSeries)
        {
            try
            {
                var settings = _coordinateTableSettings;
                if (settings == null || !HasPageGeneratedContent()) return;

                // 从地图系列获取索引图层
                var spatialMapSeries = mapSeries as SpatialMapSeries;
                if (spatialMapSeries == null) return;

                var indexLayer = spatialMapSeries.IndexLayer as FeatureLayer;
                if (indexLayer == null)
                {
                    System.Diagnostics.Debug.WriteLine("地图系列索引图层不是要素图层");
                    return;
                }

                // 获取索引字段名称
                var smsDefinition = mapSeries.GetDefinition() as CIMSpatialMapSeries;
                string indexField = smsDefinition?.NameField;
                if (string.IsNullOrEmpty(indexField))
                {
                    System.Diagnostics.Debug.WriteLine("地图系列索引字段为空");
                    return;
                }

                // 获取当前地图系列页面对应的要素
                var currentPageName = mapSeries.CurrentPageName;
                if (string.IsNullOrEmpty(currentPageName)) return;

                // 查询匹配当前页面的要素
                using (var table = indexLayer.GetTable())
                {
                    var queryFilter = new QueryFilter
                    {
                        WhereClause = $"{indexField} = '{currentPageName}'"
                    };

                    using (var cursor = table.Search(queryFilter))
                    {
                        if (cursor.MoveNext())
                        {
                            using (var row = cursor.Current)
                            {
                                var geometry = row["SHAPE"] as Polygon;
                                var uniqueValue = row[indexField]?.ToString();

                                // 提取所有字段值用于自定义文本替换
                                var fieldValues = new Dictionary<string, string>();
                                var fields = row.GetFields();
                                foreach (var field in fields)
                                {
                                    try
                                    {
                                        var value = row[field.Name];
                                        fieldValues[field.Name] = value?.ToString() ?? "";
                                    }
                                    catch { }
                                }

                                if (geometry != null && !string.IsNullOrEmpty(uniqueValue))
                                {
                                    if (settings.EnableCoordinateTable)
                                    {
                                        ProcessPolygonAndCreateTable(layout, geometry, uniqueValue, settings, fieldValues);
                                    }
                                    
                                    // 如果启用了界址点、点号或边长生成，则创建相应标注
                                    if (settings.EnableBoundaryPoints || settings.EnablePointLabels || settings.EnableEdgeLabels)
                                    {
                                        CreateBoundaryPointsOnMap(layout, geometry, settings);
                                    }

                                    if (settings.EnableIntersectTable)
                                    {
                                        CreateIntersectTableForCurrentPage(layout, geometry, settings);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"生成坐标表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理面要素并创建坐标表（同步版本）
        /// </summary>
        private void ProcessPolygonAndCreateTable(Layout layout, Polygon geometry, string uniqueValue, 
            CoordinateTableSettings settings, Dictionary<string, string> fieldValues = null)
        {
            try
            {
                // 提取坐标点
                var coordinates = ExtractCoordinates(geometry, settings.XYDecimal);
                var edgeLengths = CalculateEdgeLengths(coordinates);
                
                // 计算面积
                var (areaFormatted, muAreaFormatted) = FormatArea(geometry.Area, settings.AreaUnit, settings.AreaDecimal, settings.MuDecimal);
                
                // 替换标题中的字段占位符
                string titleText = settings.TitleText ?? "";
                if (fieldValues != null && titleText.Contains("["))
                {
                    var regex = new Regex(@"\[([^\]]+)\]");
                    titleText = regex.Replace(titleText, match =>
                    {
                        var fieldName = match.Groups[1].Value;
                        if (fieldValues.TryGetValue(fieldName, out var value))
                        {
                            return value;
                        }
                        return match.Value;
                    });
                }
                
                // 生成表格文本
                var tableData = GenerateTableData(coordinates, titleText, settings.PointPrefix, settings.GenerateEdge,
                    edgeLengths, settings.EdgeDecimal, settings.RowsPerColumn, settings.SwapXY, settings.CompressTotalRows);
                
                // 在布局上创建表格元素（同步）
                CreateTableElements(layout, tableData, uniqueValue, areaFormatted, muAreaFormatted, settings, fieldValues);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理面要素 {uniqueValue} 时发生错误: {ex.Message}");
            }
        }

        private (string areaFormatted, string muAreaFormatted) FormatArea(double area, string unitChoice, 
            int decimalPlaces, int muDecimalPlaces)
        {
            if (unitChoice == "平方米")
            {
                double main = Math.Round(area, decimalPlaces, MidpointRounding.AwayFromZero);
                string areaFormatted = main.ToString($"F{decimalPlaces}");
                double muArea = main * 0.0015;
                string muAreaFormatted = muArea.ToString($"F{muDecimalPlaces}");
                return (areaFormatted, muAreaFormatted);
            }
            else if (unitChoice == "公顷")
            {
                double hectares = area / 10000.0;
                double main = Math.Round(hectares, decimalPlaces, MidpointRounding.AwayFromZero);
                string areaFormatted = main.ToString($"F{decimalPlaces}");
                double muArea = main * 15.0;
                string muAreaFormatted = muArea.ToString($"F{muDecimalPlaces}");
                return (areaFormatted, muAreaFormatted);
            }

            double fallbackMain = Math.Round(area, decimalPlaces, MidpointRounding.AwayFromZero);
            return (fallbackMain.ToString($"F{decimalPlaces}"), (fallbackMain * 0.0015).ToString($"F{muDecimalPlaces}"));
        }

        private List<string> GenerateTableData(List<(double X, double Y, string XStr, string YStr)> coordinates,
            string titleText, string prefixStr, bool generateEdge, List<double> edgeLengths,
            int edgeDecimal, int rowsPerColumn, bool surveyStyle, int compressTotalRows)
        {
            var tableData = new List<string>();
            int splitRowsPerColumn = Math.Max(2, rowsPerColumn);
            
            string headerInfo = generateEdge ? 
                $"{titleText}\n点号    X坐标    Y坐标    边长" : 
                $"{titleText}\n点号    X坐标    Y坐标";

            var dataRows = new List<string[]>();
            for (int i = 0; i < coordinates.Count; i++)
            {
                int displayNo = (i == coordinates.Count - 1) ? 1 : i + 1;
                string pointName = $"{prefixStr}{displayNo}";
                string xOut = surveyStyle ? coordinates[i].YStr : coordinates[i].XStr;
                string yOut = surveyStyle ? coordinates[i].XStr : coordinates[i].YStr;

                if (generateEdge && i < edgeLengths.Count)
                {
                    string edgeStr = edgeLengths[i].ToString($"F{edgeDecimal}");
                    dataRows.Add(new[] { pointName, xOut, yOut, edgeStr });
                }
                else
                {
                    if (generateEdge)
                    {
                        dataRows.Add(new[] { pointName, xOut, yOut, string.Empty });
                    }
                    else
                    {
                        dataRows.Add(new[] { pointName, xOut, yOut });
                    }
                }
            }

            if (compressTotalRows > 0 && dataRows.Count > compressTotalRows)
            {
                int compressedDataRows = ResolveCompressedDataRowCount(dataRows.Count, splitRowsPerColumn, compressTotalRows);
                if (compressedDataRows < dataRows.Count)
                {
                    dataRows = CompressRowsToTargetCount(dataRows, compressedDataRows, generateEdge, splitRowsPerColumn);
                }
            }

            var dataLines = dataRows.Select(row => FormatTableDataLine(row, generateEdge)).ToList();
            
            if (dataLines.Count <= splitRowsPerColumn)
            {
                string subtable = headerInfo + "\n" + string.Join("\n", dataLines);
                tableData.Add(subtable);
            }
            else
            {
                var firstChunk = dataLines.Take(splitRowsPerColumn);
                string firstSubtable = headerInfo + "\n" + string.Join("\n", firstChunk);
                tableData.Add(firstSubtable);
                
                int index = splitRowsPerColumn;
                while (index < dataLines.Count)
                {
                    var lines = new List<string> { headerInfo.Split('\n')[0], headerInfo.Split('\n')[1] };
                    lines.Add(dataLines[index - 1]);
                    
                    var nextChunk = dataLines.Skip(index).Take(splitRowsPerColumn - 1);
                    lines.AddRange(nextChunk);
                    
                    string subtable = string.Join("\n", lines);
                    tableData.Add(subtable);
                    
                    index += splitRowsPerColumn - 1;
                }
            }
            
            return tableData;
        }

        private string[] CreateEllipsisRow(bool generateEdge)
        {
            return generateEdge
                ? new[] { EllipsisMarker, EllipsisMarker, EllipsisMarker, EllipsisMarker }
                : new[] { EllipsisMarker, EllipsisMarker, EllipsisMarker };
        }

        private string FormatTableDataLine(string[] tokens, bool generateEdge)
        {
            if (generateEdge)
            {
                string edgeText = tokens.Length > 3 ? tokens[3] : string.Empty;
                return $"{tokens[0]}    {tokens[1]}    {tokens[2]}    {edgeText}";
            }

            return $"{tokens[0]}    {tokens[1]}    {tokens[2]}";
        }
    }
}
