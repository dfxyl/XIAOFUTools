using System;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Tools.Output.MapSeriesExport
{
    public sealed record MapSeriesIntersectArea(string Category, double Area);

    public sealed record MapSeriesIntersectTableRow(string Category, double Area, double Percent, bool IsTotal);

    public static class MapSeriesIntersectTableBuilder
    {
        private const string OtherCategory = "其他";
        private const string TotalCategory = "合计";

        public static List<MapSeriesIntersectTableRow> BuildRows(
            IEnumerable<MapSeriesIntersectArea> intersectAreas,
            double redlineArea,
            string areaUnit,
            int decimalPlaces)
        {
            var sourceItems = (intersectAreas ?? Enumerable.Empty<MapSeriesIntersectArea>())
                .Where(item => item.Area > 0)
                .GroupBy(item => string.IsNullOrWhiteSpace(item.Category) ? "未分类" : item.Category.Trim())
                .Select(group => new MapSeriesIntersectArea(group.Key, group.Sum(item => item.Area)))
                .ToList();

            if (redlineArea <= 0)
            {
                return sourceItems
                    .Select(item => new MapSeriesIntersectTableRow(
                        item.Category,
                        Math.Round(ConvertAreaUnit(item.Area, areaUnit), decimalPlaces),
                        0,
                        false))
                    .ToList();
            }

            double intersectTotal = sourceItems.Sum(item => item.Area);
            double uncoveredArea = redlineArea - intersectTotal;
            if (uncoveredArea > 0.0001)
            {
                sourceItems.Add(new MapSeriesIntersectArea(OtherCategory, uncoveredArea));
                intersectTotal += uncoveredArea;
            }

            if (sourceItems.Count == 0)
            {
                sourceItems.Add(new MapSeriesIntersectArea(OtherCategory, redlineArea));
                intersectTotal = redlineArea;
            }

            double adjustmentRatio = intersectTotal > 0 ? redlineArea / intersectTotal : 1;
            var adjustedSquareMeters = sourceItems
                .Select(item => new MapSeriesIntersectArea(item.Category, item.Area * adjustmentRatio))
                .ToList();

            var roundedAreas = ApplyLargestRemainder(
                adjustedSquareMeters.Select(item => ConvertAreaUnit(item.Area, areaUnit)).ToList(),
                ConvertAreaUnit(redlineArea, areaUnit),
                decimalPlaces);

            MergeTinyOtherIntoLargestRemainder(adjustedSquareMeters, roundedAreas, areaUnit, decimalPlaces);

            double roundedTotal = Math.Round(ConvertAreaUnit(redlineArea, areaUnit), decimalPlaces);
            var rows = new List<MapSeriesIntersectTableRow>();

            for (int i = 0; i < adjustedSquareMeters.Count; i++)
            {
                double area = roundedAreas[i];
                if (area == 0)
                {
                    continue;
                }

                double percent = redlineArea > 0 ? adjustedSquareMeters[i].Area / redlineArea * 100.0 : 0;
                rows.Add(new MapSeriesIntersectTableRow(
                    adjustedSquareMeters[i].Category,
                    area,
                    Math.Round(percent, 2),
                    false));
            }

            rows.Add(new MapSeriesIntersectTableRow(TotalCategory, roundedTotal, 100.0, true));
            return rows;
        }

        public static double ConvertAreaUnit(double areaInSquareMeters, string targetUnit)
        {
            return targetUnit switch
            {
                "公顷" => areaInSquareMeters / 10000.0,
                "亩" => areaInSquareMeters / 666.6666666667,
                _ => areaInSquareMeters
            };
        }

        private static List<double> ApplyLargestRemainder(
            IReadOnlyList<double> values,
            double expectedTotal,
            int decimalPlaces)
        {
            if (values.Count == 0)
            {
                return new List<double>();
            }

            double minUnit = Math.Pow(10, -Math.Max(0, decimalPlaces));
            double roundedTotal = Math.Round(expectedTotal, decimalPlaces);
            var floorValues = values.Select(value => Math.Floor(value / minUnit) * minUnit).ToList();
            var remainders = values.Select((value, index) => value - floorValues[index]).ToList();
            double floorTotal = Math.Round(floorValues.Sum(), decimalPlaces);
            int unitsToDistribute = (int)Math.Round((roundedTotal - floorTotal) / minUnit);

            var order = remainders
                .Select((remainder, index) => new { remainder, index })
                .OrderByDescending(item => item.remainder)
                .ThenBy(item => item.index)
                .ToList();

            var result = floorValues.ToList();
            for (int i = 0; i < Math.Min(unitsToDistribute, order.Count); i++)
            {
                result[order[i].index] += minUnit;
            }

            return result.Select(value => Math.Round(value, decimalPlaces)).ToList();
        }

        private static void MergeTinyOtherIntoLargestRemainder(
            List<MapSeriesIntersectArea> adjustedSquareMeters,
            List<double> roundedAreas,
            string areaUnit,
            int decimalPlaces)
        {
            int otherIndex = adjustedSquareMeters.FindIndex(item => item.Category == OtherCategory);
            if (otherIndex < 0 || otherIndex >= roundedAreas.Count)
            {
                return;
            }

            double minUnit = Math.Pow(10, -Math.Max(0, decimalPlaces));
            double otherRoundedArea = roundedAreas[otherIndex];
            if (otherRoundedArea <= 0)
            {
                return;
            }

            if (otherRoundedArea - minUnit > 0.000000001)
            {
                return;
            }

            var candidateIndexes = adjustedSquareMeters
                .Select((item, index) => new { item, index })
                .Where(x => x.index != otherIndex && roundedAreas[x.index] > 0)
                .ToList();
            if (candidateIndexes.Count == 0)
            {
                return;
            }

            int targetIndex = candidateIndexes
                .OrderByDescending(x => GetRemainderAtPrecision(
                    MapSeriesIntersectTableBuilder.ConvertAreaUnit(x.item.Area, areaUnit),
                    decimalPlaces))
                .ThenByDescending(x => roundedAreas[x.index])
                .ThenBy(x => x.index)
                .First()
                .index;

            roundedAreas[targetIndex] = Math.Round(roundedAreas[targetIndex] + otherRoundedArea, decimalPlaces);
            roundedAreas[otherIndex] = 0;
        }

        private static double GetRemainderAtPrecision(double value, int decimalPlaces)
        {
            double minUnit = Math.Pow(10, -Math.Max(0, decimalPlaces));
            return value - Math.Floor(value / minUnit) * minUnit;
        }
    }
}
