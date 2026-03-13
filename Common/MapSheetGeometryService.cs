using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Geometry;

namespace XIAOFUTools.Common
{
    internal static class MapSheetGeometryService
    {
        private const double LargeAreaTolerance = 1e-8;
        private const double SmallAreaTolerance = 1e-12;

        private static readonly SpatialReference Cgcs2000 = SpatialReferenceBuilder.CreateSpatialReference(4490);

        public static IReadOnlyList<string> GetLargeScaleCodes(
            Geometry geometry,
            LargeScaleMapSheetOption option,
            string namingConvention,
            int decimalPlaces)
        {
            ValidateProjectedGeometry(geometry, "大比例图幅");

            var candidates = LargeScaleMapSheetCalculator.GetCellsForExtent(
                geometry.Extent.XMin,
                geometry.Extent.XMax,
                geometry.Extent.YMin,
                geometry.Extent.YMax,
                option,
                namingConvention,
                decimalPlaces);

            return candidates
                .Where(cell => HasPositiveAreaIntersection(CreatePolygon(cell, geometry.SpatialReference), geometry, LargeAreaTolerance))
                .Select(cell => cell.Code)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<string> GetSmallScaleCodes(Geometry geometry, SmallScaleMapSheetOption option)
        {
            if (geometry == null || geometry.IsEmpty)
            {
                return Array.Empty<string>();
            }

            if (geometry.SpatialReference == null)
            {
                throw new InvalidOperationException("当前图层缺少空间参考，无法计算小比例图幅。");
            }

            var projectedGeometry = geometry.SpatialReference.Wkid == Cgcs2000.Wkid
                ? geometry
                : GeometryEngine.Instance.Project(geometry, Cgcs2000);

            var candidates = SmallScaleMapSheetCalculator.GetCellsForExtent(
                projectedGeometry.Extent.XMin,
                projectedGeometry.Extent.XMax,
                projectedGeometry.Extent.YMin,
                projectedGeometry.Extent.YMax,
                option);

            return candidates
                .Where(cell => HasPositiveAreaIntersection(CreatePolygon(cell, Cgcs2000), projectedGeometry, SmallAreaTolerance))
                .Select(cell => cell.Code)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
        }

        public static MapSheetIdentifyResult IdentifyLargeScale(
            MapPoint point,
            LargeScaleMapSheetOption option,
            string namingConvention,
            int decimalPlaces)
        {
            ValidateProjectedGeometry(point, "大比例图幅");

            var cells = LargeScaleMapSheetCalculator.GetCellsForPoint(
                point.X,
                point.Y,
                option,
                namingConvention,
                decimalPlaces);

            return new MapSheetIdentifyResult(cells.Select(cell => cell.Code), cells);
        }

        public static MapSheetIdentifyResult IdentifySmallScale(MapPoint point, SmallScaleMapSheetOption option)
        {
            if (point == null || point.IsEmpty)
            {
                return MapSheetIdentifyResult.Empty;
            }

            if (point.SpatialReference == null)
            {
                throw new InvalidOperationException("当前点击位置缺少空间参考，无法计算小比例图幅。");
            }

            var cgcsPoint = point.SpatialReference.Wkid == Cgcs2000.Wkid
                ? point
                : (MapPoint)GeometryEngine.Instance.Project(point, Cgcs2000);

            var cells = SmallScaleMapSheetCalculator.GetCellsForPoint(cgcsPoint.X, cgcsPoint.Y, option);
            return new MapSheetIdentifyResult(cells.Select(cell => cell.Code), cells);
        }

        public static Polygon CreatePolygon(MapSheetCell cell, SpatialReference spatialReference)
        {
            var envelope = EnvelopeBuilderEx.CreateEnvelope(cell.XMin, cell.YMin, cell.XMax, cell.YMax, spatialReference);
            return PolygonBuilderEx.CreatePolygon(envelope);
        }

        private static bool HasPositiveAreaIntersection(Polygon mapSheetPolygon, Geometry geometry, double tolerance)
        {
            var intersection = GeometryEngine.Instance.Intersection(mapSheetPolygon, geometry);
            if (intersection == null || intersection.IsEmpty)
            {
                return false;
            }

            return Math.Abs(GeometryEngine.Instance.Area(intersection)) > tolerance;
        }

        private static void ValidateProjectedGeometry(Geometry geometry, string title)
        {
            if (geometry == null || geometry.IsEmpty)
            {
                throw new InvalidOperationException($"{title}计算失败：输入几何为空。");
            }

            if (geometry.SpatialReference == null)
            {
                throw new InvalidOperationException($"{title}计算失败：输入图层缺少空间参考。");
            }

            if (!geometry.SpatialReference.IsProjected)
            {
                throw new InvalidOperationException($"{title}要求图层使用投影坐标系。");
            }
        }
    }
}
