using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Cartography.MapSeriesExport.Core;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport.Infrastructure
{
    internal sealed class ArcGisMapSeriesIntersectAreaReader
    {
        internal IReadOnlyList<MapSeriesIntersectArea> Read(
            Layout layout,
            Polygon redlineGeometry,
            CoordinateTableSettings settings)
        {
            if (layout == null ||
                redlineGeometry == null ||
                settings == null ||
                string.IsNullOrWhiteSpace(settings.IntersectLayerName) ||
                string.IsNullOrWhiteSpace(settings.IntersectClassField))
            {
                return Array.Empty<MapSeriesIntersectArea>();
            }

            var classLayer = FindFeatureLayer(layout, settings);
            return classLayer == null
                ? Array.Empty<MapSeriesIntersectArea>()
                : ReadAreas(redlineGeometry, classLayer, settings.IntersectClassField);
        }

        private static FeatureLayer FindFeatureLayer(
            Layout layout,
            CoordinateTableSettings settings)
        {
            var maps = new List<Map>();
            var mapFrame = ResolveMapFrame(layout, settings);
            if (mapFrame?.Map != null)
            {
                maps.Add(mapFrame.Map);
            }

            var activeMap = MapView.Active?.Map;
            if (activeMap != null && !maps.Contains(activeMap))
            {
                maps.Add(activeMap);
            }

            return maps
                .SelectMany(map => map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
                .FirstOrDefault(layer => string.Equals(
                    layer.Name,
                    settings.IntersectLayerName,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static MapFrame ResolveMapFrame(
            Layout layout,
            CoordinateTableSettings settings)
        {
            var configured = string.IsNullOrWhiteSpace(settings.MapFrameName)
                ? null
                : layout.FindElement(settings.MapFrameName) as MapFrame;
            return configured ?? layout.Elements.OfType<MapFrame>().FirstOrDefault();
        }

        private static IReadOnlyList<MapSeriesIntersectArea> ReadAreas(
            Polygon redlineGeometry,
            FeatureLayer classLayer,
            string classField)
        {
            var areas = new List<MapSeriesIntersectArea>();
            var featureClass = classLayer.GetFeatureClass();
            if (featureClass == null)
            {
                return areas;
            }

            var classSpatialReference = featureClass.GetDefinition().GetSpatialReference();
            var redlineSpatialReference = redlineGeometry.SpatialReference;
            var requiresProjection = redlineSpatialReference != null &&
                                     classSpatialReference != null &&
                                     !SpatialReference.AreEqual(
                                         redlineSpatialReference,
                                         classSpatialReference,
                                         false);
            var queryGeometry = requiresProjection
                ? GeometryEngine.Instance.Project(redlineGeometry, classSpatialReference) as Polygon
                : redlineGeometry;
            if (queryGeometry == null)
            {
                return areas;
            }

            using var cursor = featureClass.Search(new SpatialQueryFilter
            {
                FilterGeometry = queryGeometry,
                SpatialRelationship = SpatialRelationship.Intersects
            });
            while (cursor.MoveNext())
            {
                using var feature = cursor.Current as Feature;
                if (feature?.GetShape() is not Polygon classPolygon)
                {
                    continue;
                }

                var comparablePolygon = requiresProjection
                    ? GeometryEngine.Instance.Project(classPolygon, redlineSpatialReference) as Polygon
                    : classPolygon;
                if (comparablePolygon == null)
                {
                    continue;
                }

                var intersection = GeometryEngine.Instance.Intersection(
                    redlineGeometry,
                    comparablePolygon);
                if (intersection is not Polygon intersectPolygon ||
                    intersectPolygon.IsEmpty ||
                    intersectPolygon.Area <= 0.0001)
                {
                    continue;
                }

                var category = "未分类";
                try
                {
                    category = feature[classField]?.ToString();
                }
                catch
                {
                    // 字段缺失时保持兼容的“未分类”。
                }

                areas.Add(new MapSeriesIntersectArea(category, intersectPolygon.Area));
            }

            return areas;
        }
    }
}
