using System;
using System.Collections.Generic;

namespace XIAOFUTools.Features.Editing.Boundary.Shared.Core
{
    internal static class BoundaryLabelPlacementPlanner
    {
        private static readonly double[] DistanceFactors = { 1.0, 1.3, 1.6, 2.0 };

        private static readonly BoundaryVertex[] PointDirections =
        {
            new(Math.Sqrt(2) / 2, Math.Sqrt(2) / 2),
            new(-Math.Sqrt(2) / 2, Math.Sqrt(2) / 2),
            new(Math.Sqrt(2) / 2, -Math.Sqrt(2) / 2),
            new(-Math.Sqrt(2) / 2, -Math.Sqrt(2) / 2),
            new(1, 0),
            new(0, 1),
            new(-1, 0),
            new(0, -1)
        };

        public static IReadOnlyList<BoundaryVertex> PlanPointCandidates(
            IReadOnlyList<BoundaryVertex> ring,
            int index,
            double distance)
        {
            ArgumentNullException.ThrowIfNull(ring);
            if (ring.Count == 0)
            {
                throw new ArgumentException("界址环不能为空。", nameof(ring));
            }
            if (index < 0 || index >= ring.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var current = ring[index];
            var candidates = new List<BoundaryVertex>(1 + PointDirections.Length * DistanceFactors.Length)
            {
                CalculateOutsideBisectorPosition(ring, index, distance)
            };

            foreach (var factor in DistanceFactors)
            {
                var candidateDistance = distance * factor;
                foreach (var direction in PointDirections)
                {
                    candidates.Add(new BoundaryVertex(
                        current.X + direction.X * candidateDistance,
                        current.Y + direction.Y * candidateDistance));
                }
            }

            return candidates;
        }

        public static IReadOnlyList<BoundaryVertex> PlanEdgeCandidates(
            BoundaryVertex start,
            BoundaryVertex end,
            double distance,
            IReadOnlyList<BoundaryVertex> ring)
        {
            ArgumentNullException.ThrowIfNull(ring);

            var middleX = (start.X + end.X) / 2;
            var middleY = (start.Y + end.Y) / 2;
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            if (length < 0.0001)
            {
                length = 1;
            }

            var perpendicularX = -deltaY / length;
            var perpendicularY = deltaX / length;
            var testPoint = new BoundaryVertex(
                middleX + perpendicularX * distance * 0.1,
                middleY + perpendicularY * distance * 0.1);
            var pointsInside = IsPointInPolygon(testPoint, ring);
            var outsideX = pointsInside ? -perpendicularX : perpendicularX;
            var outsideY = pointsInside ? -perpendicularY : perpendicularY;

            var candidates = new List<BoundaryVertex>(DistanceFactors.Length * 2);
            foreach (var factor in DistanceFactors)
            {
                var candidateDistance = distance * factor;
                candidates.Add(new BoundaryVertex(
                    middleX + outsideX * candidateDistance,
                    middleY + outsideY * candidateDistance));
                candidates.Add(new BoundaryVertex(
                    middleX - outsideX * candidateDistance,
                    middleY - outsideY * candidateDistance));
            }

            return candidates;
        }

        public static bool IsPointInPolygon(BoundaryVertex point, IReadOnlyList<BoundaryVertex> ring)
        {
            ArgumentNullException.ThrowIfNull(ring);
            var inside = false;

            for (int current = 0, previous = ring.Count - 1;
                 current < ring.Count;
                 previous = current++)
            {
                var currentVertex = ring[current];
                var previousVertex = ring[previous];
                if (((currentVertex.Y > point.Y) != (previousVertex.Y > point.Y)) &&
                    (point.X < (previousVertex.X - currentVertex.X) *
                     (point.Y - currentVertex.Y) /
                     (previousVertex.Y - currentVertex.Y) + currentVertex.X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        public static bool IsOverlapping(BoundaryLabelBounds first, BoundaryLabelBounds second)
        {
            return Math.Abs(first.X - second.X) < (first.Width + second.Width) / 2 * 0.9 &&
                   Math.Abs(first.Y - second.Y) < (first.Height + second.Height) / 2 * 0.9;
        }

        private static BoundaryVertex CalculateOutsideBisectorPosition(
            IReadOnlyList<BoundaryVertex> ring,
            int index,
            double distance)
        {
            var current = ring[index];
            var previous = ring[(index - 1 + ring.Count) % ring.Count];
            var next = ring[(index + 1) % ring.Count];

            var previousX = previous.X - current.X;
            var previousY = previous.Y - current.Y;
            var nextX = next.X - current.X;
            var nextY = next.Y - current.Y;

            var previousLength = Math.Sqrt(previousX * previousX + previousY * previousY);
            var nextLength = Math.Sqrt(nextX * nextX + nextY * nextY);
            if (previousLength > 0.0001)
            {
                previousX /= previousLength;
                previousY /= previousLength;
            }
            if (nextLength > 0.0001)
            {
                nextX /= nextLength;
                nextY /= nextLength;
            }

            var bisectorX = previousX + nextX;
            var bisectorY = previousY + nextY;
            var bisectorLength = Math.Sqrt(bisectorX * bisectorX + bisectorY * bisectorY);
            if (bisectorLength < 0.0001)
            {
                bisectorX = -previousY;
                bisectorY = previousX;
            }
            else
            {
                bisectorX /= bisectorLength;
                bisectorY /= bisectorLength;
            }

            var testPoint = new BoundaryVertex(
                current.X + bisectorX * distance * 0.1,
                current.Y + bisectorY * distance * 0.1);
            var pointsInside = IsPointInPolygon(testPoint, ring);
            var outsideX = pointsInside ? -bisectorX : bisectorX;
            var outsideY = pointsInside ? -bisectorY : bisectorY;

            return new BoundaryVertex(
                current.X + outsideX * distance,
                current.Y + outsideY * distance);
        }
    }
}
