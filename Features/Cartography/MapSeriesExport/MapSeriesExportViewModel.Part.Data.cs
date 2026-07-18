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
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    public partial class MapSeriesExportViewModel
    {
        
        private async void LoadLayouts()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    var project = Project.Current;
                    if (project == null) return;
                    var layouts = project.GetItems<LayoutProjectItem>().ToList();
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        Layouts.Clear();
                        foreach (var layout in layouts) Layouts.Add(layout);
                        if (Layouts.Count > 0) SelectedLayout = Layouts[0];
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

        private async void LoadMapSeriesPages()
        {
            try
            {
                var result = await _pageReader.ReadAsync(SelectedLayout);
                PresentationServices.UiThread.InvokeOrRun(() => ApplyMapSeriesPageResult(result));
            }
            catch (Exception ex)
            {
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    MapSeriesStatus = $"加载失败: {ex.Message}";
                    MapSeriesStatusColor = new SolidColorBrush(Colors.Red);
                    MapSeriesPages.Clear();
                });
            }
        }

        private void ApplyMapSeriesPageResult(
            XIAOFUTools.Features.Cartography.MapSeriesExport.Core.MapSeriesPageReadResult result)
        {
            MapSeriesStatus = result.Message;
            switch (result.Status)
            {
                case XIAOFUTools.Features.Cartography.MapSeriesExport.Core.MapSeriesPageReadStatus.Loaded:
                    MapSeriesStatusColor = new SolidColorBrush(Colors.Green);
                    MapSeriesPages = new ObservableCollection<MapSeriesPageItem>(
                        result.Pages.Select(page => new MapSeriesPageItem
                        {
                            PageIndex = page.PageIndex,
                            PageName = page.PageName,
                            IsSelected = true
                        }));
                    break;
                case XIAOFUTools.Features.Cartography.MapSeriesExport.Core.MapSeriesPageReadStatus.MapSeriesDisabled:
                    MapSeriesStatusColor = new SolidColorBrush(Colors.Orange);
                    MapSeriesPages.Clear();
                    break;
                case XIAOFUTools.Features.Cartography.MapSeriesExport.Core.MapSeriesPageReadStatus.LayoutUnavailable:
                    MapSeriesStatusColor = new SolidColorBrush(
                        SelectedLayout == null ? Colors.Gray : Colors.Red);
                    MapSeriesPages.Clear();
                    break;
                default:
                    MapSeriesStatusColor = new SolidColorBrush(Colors.Red);
                    MapSeriesPages.Clear();
                    break;
            }
        }

        private IEnumerable<MapSeriesPageItem> GetVisiblePages()
        {
            if (MapSeriesPagesView != null)
            {
                return MapSeriesPagesView.Cast<object>().OfType<MapSeriesPageItem>();
            }
            return MapSeriesPages;
        }

        private string GetFileExtension() => SelectedFormat.ToLower() switch { "pdf" => "pdf", "jpg" => "jpg", "png" => "png", "tif" => "tif", _ => "pdf" };

        private List<double> CalculateEdgeLengths(List<(double X, double Y, string XStr, string YStr)> coordinates)
        {
            var edgeLengths = new List<double>();
            
            for (int i = 0; i < coordinates.Count - 1; i++)
            {
                double dx = coordinates[i + 1].X - coordinates[i].X;
                double dy = coordinates[i + 1].Y - coordinates[i].Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                edgeLengths.Add(length);
            }
            
            return edgeLengths;
        }

        private int ResolveCompressedDataRowCount(int sourceRowCount, int rowsPerColumn, int maxDisplayRows)
        {
            if (sourceRowCount <= 0 || maxDisplayRows <= 0)
            {
                return sourceRowCount;
            }

            if (rowsPerColumn <= 1)
            {
                return Math.Min(sourceRowCount, maxDisplayRows);
            }

            int upper = Math.Min(sourceRowCount, maxDisplayRows);
            int best = 1;

            for (int candidate = 1; candidate <= upper; candidate++)
            {
                int displayed = CalculateDisplayedRowCount(candidate, rowsPerColumn);
                if (displayed <= maxDisplayRows)
                {
                    best = candidate;
                }
                else
                {
                    break;
                }
            }

            return best;
        }

        private int CalculateDisplayedRowCount(int dataRowCount, int rowsPerColumn)
        {
            if (dataRowCount <= 0)
            {
                return 0;
            }

            if (dataRowCount <= rowsPerColumn)
            {
                return dataRowCount;
            }

            int displayed = rowsPerColumn;
            int index = rowsPerColumn;
            while (index < dataRowCount)
            {
                int take = Math.Min(rowsPerColumn - 1, dataRowCount - index);
                displayed += 1 + take;
                index += rowsPerColumn - 1;
            }

            return displayed;
        }

        private List<string[]> CompressRowsToTargetCount(List<string[]> sourceRows, int targetCount,
            bool generateEdge, int rowsPerColumn)
        {
            if (sourceRows == null || sourceRows.Count == 0 || targetCount >= sourceRows.Count)
            {
                return sourceRows;
            }

            if (targetCount <= 1)
            {
                return new List<string[]> { sourceRows[0] };
            }

            if (targetCount == 2)
            {
                return new List<string[]> { sourceRows[0], sourceRows[^1] };
            }

            int bestHeadCount = 1;
            int bestTailCount = targetCount - bestHeadCount - 1;
            double totalDisplayedCenter = (CalculateDisplayedRowCount(targetCount, rowsPerColumn) + 1) / 2.0;
            double bestScore = double.MaxValue;

            for (int headCount = 1; headCount <= targetCount - 2; headCount++)
            {
                int tailCount = targetCount - headCount - 1;
                if (headCount + tailCount >= sourceRows.Count)
                {
                    continue;
                }

                int ellipsisDataIndex = headCount + 1;
                int ellipsisDisplayIndex = CalculateDisplayedIndexForDataRow(ellipsisDataIndex, rowsPerColumn);
                double centerDistance = Math.Abs(ellipsisDisplayIndex - totalDisplayedCenter);
                double balancePenalty = Math.Abs(headCount - tailCount) * 0.01;
                double score = centerDistance + balancePenalty;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestHeadCount = headCount;
                    bestTailCount = tailCount;
                }
            }

            var compressed = new List<string[]>();
            compressed.AddRange(sourceRows.Take(bestHeadCount));
            compressed.Add(CreateEllipsisRow(generateEdge));
            compressed.AddRange(sourceRows.Skip(sourceRows.Count - bestTailCount));
            return compressed;
        }

        private int CalculateDisplayedIndexForDataRow(int dataRowIndex, int rowsPerColumn)
        {
            if (dataRowIndex <= 0)
            {
                return 0;
            }

            if (dataRowIndex <= rowsPerColumn)
            {
                return dataRowIndex;
            }

            int displayed = rowsPerColumn;
            int index = rowsPerColumn;
            while (index < dataRowIndex)
            {
                displayed += 1;
                int take = Math.Min(rowsPerColumn - 1, dataRowIndex - index);
                displayed += take;
                index += rowsPerColumn - 1;
            }

            return displayed;
        }

    }
}
