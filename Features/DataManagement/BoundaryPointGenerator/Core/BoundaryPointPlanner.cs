using System;
using System.Collections.Generic;

namespace XIAOFUTools.Features.DataManagement.BoundaryPointGenerator.Core
{
    internal sealed record BoundaryCoordinate(double X, double Y);

    internal sealed record PlannedBoundaryPoint(BoundaryCoordinate Coordinate, string Direction);

    internal sealed class BoundaryPointPlanner
    {
        private const double DuplicateTolerance = 0.0001;
        private const double ScoreTolerance = 1e-9;

        internal IReadOnlyList<PlannedBoundaryPoint> PlanCardinal(
            IEnumerable<BoundaryCoordinate> sourcePoints)
        {
            var points = GetUniquePoints(sourcePoints);
            if (points.Count == 0)
            {
                return Array.Empty<PlannedBoundaryPoint>();
            }

            var east = points[0];
            var west = points[0];
            var south = points[0];
            var north = points[0];
            foreach (var point in points)
            {
                if (point.X > east.X) east = point;
                if (point.X < west.X) west = point;
                if (point.Y < south.Y) south = point;
                if (point.Y > north.Y) north = point;
            }

            return new[]
            {
                new PlannedBoundaryPoint(east, "东"),
                new PlannedBoundaryPoint(west, "西"),
                new PlannedBoundaryPoint(south, "南"),
                new PlannedBoundaryPoint(north, "北")
            };
        }

        internal IReadOnlyList<PlannedBoundaryPoint> PlanCorners(
            IEnumerable<BoundaryCoordinate> sourcePoints)
        {
            var points = GetUniquePoints(sourcePoints);
            if (points.Count == 0)
            {
                return Array.Empty<PlannedBoundaryPoint>();
            }

            var northEast = points[0];
            var northWest = points[0];
            var southEast = points[0];
            var southWest = points[0];
            var northEastScore = northEast.X + northEast.Y;
            var northWestScore = northWest.Y - northWest.X;
            var southEastScore = southEast.X - southEast.Y;
            var southWestScore = southWest.X + southWest.Y;

            foreach (var point in points)
            {
                var ne = point.X + point.Y;
                var nw = point.Y - point.X;
                var se = point.X - point.Y;
                var sw = point.X + point.Y;
                if (ne > northEastScore ||
                    (Math.Abs(ne - northEastScore) < ScoreTolerance &&
                     (point.X > northEast.X || point.Y > northEast.Y)))
                {
                    northEast = point;
                    northEastScore = ne;
                }

                if (nw > northWestScore ||
                    (Math.Abs(nw - northWestScore) < ScoreTolerance &&
                     (point.Y > northWest.Y || point.X < northWest.X)))
                {
                    northWest = point;
                    northWestScore = nw;
                }

                if (se > southEastScore ||
                    (Math.Abs(se - southEastScore) < ScoreTolerance &&
                     (point.X > southEast.X || point.Y < southEast.Y)))
                {
                    southEast = point;
                    southEastScore = se;
                }

                if (sw < southWestScore ||
                    (Math.Abs(sw - southWestScore) < ScoreTolerance &&
                     (point.X < southWest.X || point.Y < southWest.Y)))
                {
                    southWest = point;
                    southWestScore = sw;
                }
            }

            return new[]
            {
                new PlannedBoundaryPoint(northEast, "东北"),
                new PlannedBoundaryPoint(northWest, "西北"),
                new PlannedBoundaryPoint(southEast, "东南"),
                new PlannedBoundaryPoint(southWest, "西南")
            };
        }

        private static List<BoundaryCoordinate> GetUniquePoints(
            IEnumerable<BoundaryCoordinate> sourcePoints)
        {
            var unique = new List<BoundaryCoordinate>();
            if (sourcePoints == null)
            {
                return unique;
            }

            foreach (var point in sourcePoints)
            {
                if (point == null)
                {
                    continue;
                }

                var duplicate = unique.Exists(existing =>
                    Math.Abs(point.X - existing.X) < DuplicateTolerance &&
                    Math.Abs(point.Y - existing.Y) < DuplicateTolerance);
                if (!duplicate)
                {
                    unique.Add(point);
                }
            }

            return unique;
        }
    }
}
