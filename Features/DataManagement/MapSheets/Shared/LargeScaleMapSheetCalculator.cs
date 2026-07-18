using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace XIAOFUTools.Shared
{
    public static class LargeScaleMapSheetCalculator
    {
        private static readonly LargeScaleMapSheetOption[] Options =
        {
            new("1:5000/40*40", 2000, 2000),
            new("1:2000/50*50", 1000, 1000),
            new("1:1000/50*50", 500, 500),
            new("1:500/50*50", 250, 250),
            new("1:5000/50*40", 2500, 2000),
            new("1:2000/50*40", 1000, 800),
            new("1:1000/50*40", 500, 400),
            new("1:500/50*40", 250, 200)
        };

        public static IReadOnlyList<LargeScaleMapSheetOption> GetOptions()
        {
            return Options;
        }

        public static LargeScaleMapSheetOption GetOption(string name)
        {
            var option = Options.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            if (option.Width <= 0 || option.Height <= 0)
            {
                throw new ArgumentException($"Unsupported large-scale map sheet option: {name}", nameof(name));
            }

            return option;
        }

        public static IReadOnlyList<MapSheetCell> GetCellsForExtent(
            double minX,
            double maxX,
            double minY,
            double maxY,
            LargeScaleMapSheetOption option,
            string namingConvention,
            int decimalPlaces)
        {
            ValidateOption(option);

            var normalizedMinX = Math.Min(minX, maxX);
            var normalizedMaxX = Math.Max(minX, maxX);
            var normalizedMinY = Math.Min(minY, maxY);
            var normalizedMaxY = Math.Max(minY, maxY);

            var (xStart, xEnd) = MapSheetCoverageUtils.GetInclusiveIndexRange(normalizedMinX, normalizedMaxX, option.Width);
            var (yStart, yEnd) = MapSheetCoverageUtils.GetInclusiveIndexRange(normalizedMinY, normalizedMaxY, option.Height);

            var cells = new List<MapSheetCell>();
            for (var yIndex = yStart; yIndex <= yEnd; yIndex++)
            {
                for (var xIndex = xStart; xIndex <= xEnd; xIndex++)
                {
                    var cellMinX = xIndex * option.Width;
                    var cellMaxX = cellMinX + option.Width;
                    var cellMinY = yIndex * option.Height;
                    var cellMaxY = cellMinY + option.Height;
                    var code = BuildCode(cellMinX, cellMinY, namingConvention, decimalPlaces);
                    cells.Add(new MapSheetCell(code, cellMinX, cellMaxX, cellMinY, cellMaxY));
                }
            }

            return cells
                .OrderByDescending(cell => cell.YMax)
                .ThenBy(cell => cell.XMin)
                .ToArray();
        }

        public static IReadOnlyList<MapSheetCell> GetCellsForPoint(
            double x,
            double y,
            LargeScaleMapSheetOption option,
            string namingConvention,
            int decimalPlaces)
        {
            ValidateOption(option);

            var toleranceX = MapSheetCoverageUtils.GetTolerance(option.Width);
            var toleranceY = MapSheetCoverageUtils.GetTolerance(option.Height);

            return GetCellsForExtent(
                x - toleranceX,
                x + toleranceX,
                y - toleranceY,
                y + toleranceY,
                option,
                namingConvention,
                decimalPlaces);
        }

        private static void ValidateOption(LargeScaleMapSheetOption option)
        {
            if (option.Width <= 0 || option.Height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(option), "Large-scale map sheet size must be greater than zero.");
            }
        }

        private static string BuildCode(double minX, double minY, string namingConvention, int decimalPlaces)
        {
            var format = $"F{Math.Max(0, decimalPlaces)}";
            var xCode = (minX / 1000d).ToString(format, CultureInfo.InvariantCulture);
            var yCode = (minY / 1000d).ToString(format, CultureInfo.InvariantCulture);

            return string.Equals(namingConvention, "Y-X", StringComparison.OrdinalIgnoreCase)
                ? $"{yCode}-{xCode}"
                : $"{xCode}-{yCode}";
        }
    }
}
