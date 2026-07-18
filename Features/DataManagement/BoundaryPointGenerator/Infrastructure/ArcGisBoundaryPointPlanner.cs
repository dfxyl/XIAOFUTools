using System.Collections.Generic;
using System.Linq;
using ArcGIS.Core.Geometry;
using XIAOFUTools.Features.DataManagement.BoundaryPointGenerator.Core;

namespace XIAOFUTools.Features.DataManagement.BoundaryPointGenerator.Infrastructure
{
    internal sealed record ArcGisBoundaryPoint(MapPoint Point, string Direction);

    internal sealed class ArcGisBoundaryPointPlanner
    {
        private readonly BoundaryPointPlanner _planner = new();

        internal IReadOnlyList<ArcGisBoundaryPoint> Plan(Polygon polygon, bool useCorners)
        {
            if (polygon == null || polygon.IsEmpty)
            {
                return new List<ArcGisBoundaryPoint>();
            }

            var coordinates = polygon.Parts
                .SelectMany(part => part)
                .SelectMany(segment => new[]
                {
                    new BoundaryCoordinate(segment.StartPoint.X, segment.StartPoint.Y),
                    new BoundaryCoordinate(segment.EndPoint.X, segment.EndPoint.Y)
                })
                .ToList();
            var plan = useCorners
                ? _planner.PlanCorners(coordinates)
                : _planner.PlanCardinal(coordinates);

            return plan
                .Select(item => new ArcGisBoundaryPoint(
                    MapPointBuilderEx.CreateMapPoint(
                        item.Coordinate.X,
                        item.Coordinate.Y,
                        polygon.SpatialReference),
                    item.Direction))
                .ToList();
        }
    }
}
