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

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    public partial class MapSeriesExportViewModel
    {

        private void InitializeCommands()
        {
            RefreshLayoutsCommand = new RelayCommand(RefreshLayouts);
            RefreshMapSeriesCommand = new RelayCommand(RefreshMapSeries);
            SelectAllCommand = new RelayCommand(SelectAll);
            InvertSelectionCommand = new RelayCommand(InvertSelection);
            NavigateToPageCommand = new XIAOFUTools.Shared.Mvvm.RelayCommand<MapSeriesPageItem>(NavigateToPage);
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            ShowHelpCommand = new RelayCommand(ShowHelp);
            StopCommand = new RelayCommand(Stop);
            ExportCommand = new RelayCommand(async () => await Export(), CanExport);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
        }

        private bool MapSeriesPageFilter(object obj)
        {
            if (obj is not MapSeriesPageItem page)
            {
                return false;
            }

            var keyword = PageSearchText?.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                return true;
            }

            if (SelectedPageSearchMode == "名称")
            {
                return (page.PageName ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return page.PageIndex.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private async void NavigateToPage(MapSeriesPageItem page)
        {
            try
            {
                if (page == null || SelectedLayout == null) return;
                
                // 避免重复切换同一页面
                if (_lastNavigatedPageIndex == page.PageIndex) return;
                _lastNavigatedPageIndex = page.PageIndex;
                
                await QueuedTask.Run(async () =>
                {
                    try
                    {
                        var layout = SelectedLayout.GetLayout();
                        var mapSeries = layout?.MapSeries;
                        if (mapSeries != null && mapSeries.Enabled && page.PageIndex >= 1 && page.PageIndex <= mapSeries.PageCount)
                        {
                            // 切换页面前先清除之前的坐标表
                            if (HasPageGeneratedContent())
                            {
                                ClearCoordinateTableElements(layout);
                            }
                            
                            // 切换地图系列页面
                            mapSeries.SetCurrentPageNumber(page.PageIndex.ToString());
                            
                            // 如果启用了坐标表生成，则生成新的坐标表
                            if (HasPageGeneratedContent())
                            {
                                GenerateCoordinateTableForCurrentPage(layout, mapSeries);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"导航页面失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

        private void Stop()
        {
            _cancellationTokenSource?.Cancel();
            IsRunning = false;
        }

        private string CleanFileName(string fileName)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) fileName = fileName.Replace(c, '_');
            return fileName;
        }

        /// <summary>
        /// 清除布局上的坐标表元素
        /// </summary>
        private void ClearCoordinateTableElements(Layout layout)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[驱动制图] 开始清除坐标表元素...");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                
                // 只清除一轮，使用更精确的名称匹配
                var allElements = layout.GetElements().ToList();
                System.Diagnostics.Debug.WriteLine($"[驱动制图] 布局共有 {allElements.Count} 个元素");
                
                var elementsToDelete = allElements
                    .Where(e => e.Name != null && (
                        e.Name.StartsWith("MapSeries_CoordTable_") ||
                        e.Name.StartsWith("Group_") ||
                        e.Name.StartsWith("Title_") ||
                        e.Name.StartsWith("Header_") ||
                        e.Name.StartsWith("Data_") ||
                        e.Name.StartsWith("Edge_") ||
                        e.Name.StartsWith("Area_") ||
                        e.Name.StartsWith("EdgePad") ||
                        e.Name.StartsWith("MapSeries_IntersectTable_") ||
                        e.Name.StartsWith("IntersectTitle_") ||
                        e.Name.StartsWith("IntersectHeader_") ||
                        e.Name.StartsWith("IntersectData_") ||
                        e.Name.StartsWith("IntersectTotal_")))
                    .ToList();
                
                System.Diagnostics.Debug.WriteLine($"[驱动制图] 需要删除 {elementsToDelete.Count} 个元素");
                
                if (elementsToDelete.Count > 0)
                {
                    layout.DeleteElements(elementsToDelete);
                }
                
                _currentCoordinateTableGroupName = null;
                
                // 清除界址点图形图层元素（包括点号和边长）
                if (_coordinateTableSettings?.EnableBoundaryPoints == true ||
                    _coordinateTableSettings?.EnablePointLabels == true ||
                    _coordinateTableSettings?.EnableEdgeLabels == true)
                {
                    ClearBoundaryPointsFromMap(layout);
                }
                
                sw.Stop();
                System.Diagnostics.Debug.WriteLine($"[驱动制图] 清除完成，耗时 {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除坐标表失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清除地图上的界址点标注
        /// </summary>
        private void ClearBoundaryPointsFromMap(Layout layout)
        {
            try
            {
                // 获取地图框
                MapFrame mapFrame = null;
                if (!string.IsNullOrEmpty(_coordinateTableSettings?.MapFrameName))
                {
                    mapFrame = layout.FindElement(_coordinateTableSettings.MapFrameName) as MapFrame;
                }
                if (mapFrame == null)
                {
                    mapFrame = layout.Elements.OfType<MapFrame>().FirstOrDefault();
                }
                
                if (mapFrame?.Map == null) return;
                
                var map = mapFrame.Map;
                
                // 查找界址点图形图层
                var graphicsLayer = map.GetLayersAsFlattenedList()
                    .OfType<GraphicsLayer>()
                    .FirstOrDefault(l => l.Name == "XF_界址点标注");
                
                if (graphicsLayer != null)
                {
                    ClearGraphicsLayerElements(graphicsLayer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除界址点标注失败: {ex.Message}");
            }
        }

        private List<(double X, double Y, string XStr, string YStr)> ExtractCoordinates(Polygon geometry, int decimalPlaces)
        {
            var coordinates = new List<(double X, double Y, string XStr, string YStr)>();
            
            var points = geometry.Points;
            foreach (var point in points)
            {
                coordinates.Add((
                    point.X, 
                    point.Y,
                    point.X.ToString($"F{decimalPlaces}"),
                    point.Y.ToString($"F{decimalPlaces}")
                ));
            }
            
            // 确保首尾闭合
            if (coordinates.Count > 0 && 
                (coordinates[0].X != coordinates[coordinates.Count - 1].X || 
                 coordinates[0].Y != coordinates[coordinates.Count - 1].Y))
            {
                var first = coordinates[0];
                coordinates.Add(first);
            }
            
            return coordinates;
        }

        private bool IsEllipsisRow(string[] tokens)
        {
            return tokens != null && tokens.Length > 0 && tokens[0] == EllipsisMarker;
        }
        
        /// <summary>
        /// 检测两个矩形是否重叠
        /// </summary>
        private bool IsOverlapping(double x1, double y1, double w1, double h1,
            double x2, double y2, double w2, double h2)
        {
            return Math.Abs(x1 - x2) < (w1 + w2) / 2 * 0.9 &&  // 0.9系数允许少量重叠
                   Math.Abs(y1 - y2) < (h1 + h2) / 2 * 0.9;
        }
        
        /// <summary>
        /// 清除图形图层上的所有元素
        /// </summary>
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除图形图层元素失败: {ex.Message}");
            }
        }
    }
}
