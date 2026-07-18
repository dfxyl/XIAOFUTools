#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Services
{
    internal sealed class HistoricalAreaResolver
    {
        public async Task<HistoricalResolvedArea> ResolveAsync(
            HistoricalAreaContext context,
            Envelope? customExtent,
            FeatureLayer? featureLayer,
            CancellationToken cancellationToken = default)
        {
            var mapView = MapView.Active;
            if (mapView?.Map == null)
            {
                throw new InvalidOperationException("当前没有活动地图视图。");
            }

            return context.SourceType switch
            {
                HistoricalAreaSourceType.CurrentView => ResolveFromExtent(mapView.Extent, "当前视图"),
                HistoricalAreaSourceType.CustomExtent => ResolveFromExtent(
                    customExtent ?? throw new InvalidOperationException("请先框选下载范围。"),
                    "框选范围"),
                HistoricalAreaSourceType.FeatureLayer => await ResolveFromFeatureLayerAsync(context, featureLayer, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(context.SourceType))
            };
        }

        private static HistoricalResolvedArea ResolveFromExtent(Envelope extent, string description)
        {
            var polygon = PolygonBuilderEx.CreatePolygon(extent);
            return new HistoricalResolvedArea
            {
                SourceType = description == "当前视图" ? HistoricalAreaSourceType.CurrentView : HistoricalAreaSourceType.CustomExtent,
                AreaGeometry = polygon,
                Extent = extent,
                Description = description,
                RequiresPreciseClip = false
            };
        }

        private static async Task<HistoricalResolvedArea> ResolveFromFeatureLayerAsync(
            HistoricalAreaContext context,
            FeatureLayer? featureLayer,
            CancellationToken cancellationToken)
        {
            if (featureLayer == null)
            {
                throw new InvalidOperationException("请选择一个面图层。");
            }

            return await QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var table = featureLayer.GetTable();
                if (table == null)
                {
                    throw new InvalidOperationException("无法读取所选图层。");
                }

                if (featureLayer.ShapeType != ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon)
                {
                    throw new InvalidOperationException("仅支持面图层作为下载范围。");
                }

                bool useSelection = context.PreferSelection && featureLayer.SelectionCount > 0;
                using var cursor = SelectionUtils.GetSelectionOrAllCursor(featureLayer, useSelection, new QueryFilter(), false);
                if (cursor == null)
                {
                    throw new InvalidOperationException("未找到可用的范围要素。");
                }

                var polygons = new List<Geometry>();
                while (cursor.MoveNext())
                {
                    using var row = cursor.Current;
                    if (row is Feature feature)
                    {
                        var geometry = feature.GetShape();
                        if (geometry != null && !geometry.IsEmpty)
                        {
                            polygons.Add(geometry);
                        }
                    }
                }

                if (polygons.Count == 0)
                {
                    throw new InvalidOperationException("面图层中没有可用于下载的范围要素。");
                }

                var unionGeometry = GeometryEngine.Instance.Union(polygons);
                if (unionGeometry is not Polygon polygon || polygon.IsEmpty)
                {
                    throw new InvalidOperationException("范围要素合并失败。");
                }

                return new HistoricalResolvedArea
                {
                    SourceType = HistoricalAreaSourceType.FeatureLayer,
                    AreaGeometry = polygon,
                    Extent = polygon.Extent,
                    Description = useSelection ? $"{featureLayer.Name} 选择集" : featureLayer.Name,
                    RequiresPreciseClip = true
                };
            });
        }
    }
}
