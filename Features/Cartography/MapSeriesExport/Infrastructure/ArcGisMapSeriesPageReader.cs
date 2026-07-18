using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Cartography.MapSeriesExport.Core;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport.Infrastructure
{
    internal sealed class ArcGisMapSeriesPageReader
    {
        public Task<MapSeriesPageReadResult> ReadAsync(LayoutProjectItem layoutItem)
        {
            if (layoutItem == null)
            {
                return Task.FromResult(new MapSeriesPageReadResult
                {
                    Status = MapSeriesPageReadStatus.LayoutUnavailable,
                    Message = "请选择布局"
                });
            }

            return QueuedTask.Run(() => ReadCore(layoutItem));
        }

        private static MapSeriesPageReadResult ReadCore(LayoutProjectItem layoutItem)
        {
            try
            {
                var layout = layoutItem.GetLayout();
                if (layout == null)
                {
                    return Failure(MapSeriesPageReadStatus.LayoutUnavailable, "无法获取布局");
                }

                var mapSeries = layout.MapSeries;
                if (mapSeries == null || !mapSeries.Enabled)
                {
                    return Failure(MapSeriesPageReadStatus.MapSeriesDisabled, "该布局未启用地图系列");
                }

                var pageNames = ReadPageNames(mapSeries);
                if (pageNames.Count == 0)
                {
                    for (var pageNumber = 1; pageNumber <= mapSeries.PageCount; pageNumber++)
                    {
                        pageNames.Add($"页面 {pageNumber}");
                    }
                }

                var pages = new List<MapSeriesPageDescriptor>(pageNames.Count);
                for (var index = 0; index < pageNames.Count; index++)
                {
                    pages.Add(new MapSeriesPageDescriptor
                    {
                        PageIndex = index + 1,
                        PageName = pageNames[index]
                    });
                }

                return new MapSeriesPageReadResult
                {
                    Status = MapSeriesPageReadStatus.Loaded,
                    Message = $"地图系列已启用，共 {pages.Count} 页",
                    Pages = pages
                };
            }
            catch (Exception ex)
            {
                return Failure(MapSeriesPageReadStatus.Failed, $"加载失败: {ex.Message}");
            }
        }

        private static List<string> ReadPageNames(MapSeries mapSeries)
        {
            var pageNames = new List<string>();
            if (mapSeries is not SpatialMapSeries spatialMapSeries ||
                mapSeries.GetDefinition() is not CIMSpatialMapSeries definition ||
                spatialMapSeries.IndexLayer is not FeatureLayer indexLayer ||
                string.IsNullOrEmpty(definition.NameField))
            {
                return pageNames;
            }

            try
            {
                using var table = indexLayer.GetTable();
                var sortField = string.IsNullOrEmpty(definition.SortField)
                    ? definition.NameField
                    : definition.SortField;
                var sortOrder = definition.SortAscending ? "ASC" : "DESC";

                try
                {
                    ReadNames(
                        table,
                        new QueryFilter
                        {
                            SubFields = definition.NameField,
                            PostfixClause = $"ORDER BY {sortField} {sortOrder}"
                        },
                        definition.NameField,
                        pageNames);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[驱动制图] ORDER BY 不支持: {ex.Message}，使用无排序查询");
                    pageNames.Clear();
                    ReadNames(
                        table,
                        new QueryFilter { SubFields = definition.NameField },
                        definition.NameField,
                        pageNames);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[驱动制图] 批量查询失败: {ex.Message}");
                pageNames.Clear();
            }

            return pageNames;
        }

        private static void ReadNames(
            Table table,
            QueryFilter filter,
            string nameField,
            ICollection<string> pageNames)
        {
            using var cursor = table.Search(filter);
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                var name = row[nameField]?.ToString();
                pageNames.Add(string.IsNullOrEmpty(name) ? "未命名" : name);
            }
        }

        private static MapSeriesPageReadResult Failure(
            MapSeriesPageReadStatus status,
            string message)
        {
            return new MapSeriesPageReadResult
            {
                Status = status,
                Message = message
            };
        }
    }
}
