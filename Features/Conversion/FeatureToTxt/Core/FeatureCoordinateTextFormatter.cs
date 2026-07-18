using System;
using System.Collections.Generic;
using System.Text;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt.Core
{
    internal sealed class FeatureCoordinateTextFormatter
    {
        internal void Write(
            IReadOnlyList<FeatureCoordinateRing> rings,
            StringBuilder content,
            IList<string> fieldValues,
            FeatureToTxtExportConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(rings);
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(fieldValues);
            ArgumentNullException.ThrowIfNull(configuration);
            if (fieldValues.Count == 0)
            {
                throw new ArgumentException("字段值集合必须包含点数占位项。", nameof(fieldValues));
            }

            var points = BuildNumberedPoints(rings, configuration);
            fieldValues[0] = points.Count.ToString();
            content.AppendLine(string.Join(",", fieldValues) + ",@");
            foreach (var item in points)
            {
                var x = Math.Round(item.Point.X, configuration.DecimalPlaces);
                var y = Math.Round(item.Point.Y, configuration.DecimalPlaces);
                var firstCoordinate = configuration.SwapCoordinates ? x : y;
                var secondCoordinate = configuration.SwapCoordinates ? y : x;
                content.Append(configuration.PointPrefix);
                content.Append(item.PointIndex);
                content.Append(',');
                content.Append(item.RingNumber);
                content.Append(',');
                content.Append(firstCoordinate.ToString($"F{configuration.DecimalPlaces}"));
                content.Append(',');
                content.AppendLine(secondCoordinate.ToString($"F{configuration.DecimalPlaces}"));
            }
        }

        internal IReadOnlyList<NumberedFeatureCoordinate> BuildNumberedPoints(
            IReadOnlyList<FeatureCoordinateRing> rings,
            FeatureToTxtExportConfiguration configuration)
        {
            var result = new List<NumberedFeatureCoordinate>();
            var globalPointIndex = 1;
            for (var ringIndex = 0; ringIndex < rings.Count; ringIndex++)
            {
                var ringNumber = ringIndex + 1;
                var ringPoints = new List<FeatureCoordinate>(rings[ringIndex].Points);
                var hasClosingPoint = NormalizeClosingPoint(ringPoints, configuration.OutputClosingPoint);
                var localPointIndex = configuration.InnerRingStartsAtOne && ringNumber > 1
                    ? 1
                    : globalPointIndex;
                var ringStartPointIndex = localPointIndex;
                for (var pointIndex = 0; pointIndex < ringPoints.Count; pointIndex++)
                {
                    var isClosingPoint = hasClosingPoint && pointIndex == ringPoints.Count - 1;
                    int number;
                    if (isClosingPoint && !configuration.ClosingPointContinuesNumbering)
                    {
                        number = ringStartPointIndex;
                    }
                    else
                    {
                        number = localPointIndex++;
                    }

                    result.Add(new NumberedFeatureCoordinate(
                        ringPoints[pointIndex],
                        ringNumber,
                        number,
                        isClosingPoint));
                    if (!isClosingPoint || configuration.ClosingPointContinuesNumbering)
                    {
                        globalPointIndex++;
                    }
                }
            }

            return result;
        }

        private static bool NormalizeClosingPoint(IList<FeatureCoordinate> points, bool outputClosingPoint)
        {
            if (points.Count <= 1)
            {
                return false;
            }

            var isClosed = points[0].IsEqualTo(points[^1]);
            if (outputClosingPoint)
            {
                if (!isClosed)
                {
                    points.Add(points[0]);
                }

                return true;
            }

            if (isClosed)
            {
                points.RemoveAt(points.Count - 1);
            }

            return false;
        }
    }

    internal sealed record FeatureCoordinate(double X, double Y)
    {
        internal bool IsEqualTo(FeatureCoordinate other)
        {
            return other != null &&
                   Math.Abs(X - other.X) <= 1e-9 &&
                   Math.Abs(Y - other.Y) <= 1e-9;
        }
    }

    internal sealed record FeatureCoordinateRing(IReadOnlyList<FeatureCoordinate> Points);

    internal sealed record NumberedFeatureCoordinate(
        FeatureCoordinate Point,
        int RingNumber,
        int PointIndex,
        bool IsClosingPoint);
}
