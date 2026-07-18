using System;

namespace XIAOFUTools.Features.Editing.Boundary.Shared.Core
{
    internal readonly record struct BoundaryEdgeMeasurementResult(
        double Length,
        double TextAngleDegrees);

    internal static class BoundaryEdgeMeasurement
    {
        public static BoundaryEdgeMeasurementResult Measure(BoundaryVertex start, BoundaryVertex end)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            var length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            var angle = NormalizeTextAngle(Math.Atan2(deltaY, deltaX) * 180.0 / Math.PI);
            return new BoundaryEdgeMeasurementResult(length, angle);
        }

        public static double RoundLength(double length, int decimals)
        {
            return decimals >= 0 ? Math.Round(length, decimals) : length;
        }

        private static double NormalizeTextAngle(double angleDegrees)
        {
            if (angleDegrees > 90)
            {
                angleDegrees -= 180;
            }
            else if (angleDegrees < -90)
            {
                angleDegrees += 180;
            }

            return angleDegrees;
        }
    }
}
