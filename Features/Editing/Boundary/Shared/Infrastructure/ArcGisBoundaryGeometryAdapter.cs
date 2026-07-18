using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Geometry;
using XIAOFUTools.Features.Editing.Boundary.Shared.Core;

namespace XIAOFUTools.Features.Editing.Boundary.Shared.Infrastructure
{
    internal static class ArcGisBoundaryGeometryAdapter
    {
        public static IReadOnlyList<MapPoint> NormalizeRing(
            IEnumerable<MapPoint> points,
            double tolerance)
        {
            return BoundaryRingNormalizer.Normalize(
                points,
                ToBoundaryVertex,
                tolerance);
        }

        public static IReadOnlyList<MapPoint> PlanPointLabelCandidates(
            IReadOnlyList<MapPoint> ring,
            int index,
            double distance,
            SpatialReference spatialReference)
        {
            var coreRing = ring.Select(ToBoundaryVertex).ToArray();
            return BoundaryLabelPlacementPlanner
                .PlanPointCandidates(coreRing, index, distance)
                .Select(point => ToMapPoint(point, spatialReference))
                .ToArray();
        }

        public static IReadOnlyList<MapPoint> PlanEdgeLabelCandidates(
            MapPoint start,
            MapPoint end,
            double distance,
            IReadOnlyList<MapPoint> ring,
            SpatialReference spatialReference)
        {
            var coreRing = ring.Select(ToBoundaryVertex).ToArray();
            return BoundaryLabelPlacementPlanner
                .PlanEdgeCandidates(
                    ToBoundaryVertex(start),
                    ToBoundaryVertex(end),
                    distance,
                    coreRing)
                .Select(point => ToMapPoint(point, spatialReference))
                .ToArray();
        }

        public static BoundaryVertex ToBoundaryVertex(MapPoint point)
        {
            ArgumentNullException.ThrowIfNull(point);
            return new BoundaryVertex(point.X, point.Y);
        }

        private static MapPoint ToMapPoint(BoundaryVertex point, SpatialReference spatialReference)
        {
            return MapPointBuilderEx.CreateMapPoint(point.X, point.Y, spatialReference);
        }
    }
}
