using System;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Common
{
    public static class SmallScaleMapSheetCalculator
    {
        private static readonly SmallScaleMapSheetOption[] Options =
        {
            new("100万", string.Empty, 1, 1),
            new("50万", "B", 2, 2),
            new("25万", "C", 4, 4),
            new("10万", "D", 12, 12),
            new("5万", "E", 24, 24),
            new("2.5万", "F", 48, 48),
            new("1万", "G", 96, 96),
            new("5千", "H", 192, 192)
        };

        public static IReadOnlyList<SmallScaleMapSheetOption> GetOptions()
        {
            return Options;
        }

        public static SmallScaleMapSheetOption GetOption(string name)
        {
            var option = Options.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(option.Name))
            {
                throw new ArgumentException($"Unsupported small-scale map sheet option: {name}", nameof(name));
            }

            return option;
        }

        public static IReadOnlyList<MapSheetCell> GetCellsForExtent(
            double minLon,
            double maxLon,
            double minLat,
            double maxLat,
            SmallScaleMapSheetOption option)
        {
            var normalizedMinLon = Math.Min(minLon, maxLon);
            var normalizedMaxLon = Math.Max(minLon, maxLon);
            var normalizedMinLat = Math.Min(minLat, maxLat);
            var normalizedMaxLat = Math.Max(minLat, maxLat);

            var baseCodes = Compute100kCodes(normalizedMinLon, normalizedMaxLon, normalizedMinLat, normalizedMaxLat);
            var cells = new List<MapSheetCell>();

            foreach (var baseCode in baseCodes)
            {
                if (string.IsNullOrEmpty(option.ScaleCode))
                {
                    var extent = Get100kMapExtent(baseCode);
                    cells.Add(new MapSheetCell(baseCode, extent.xmin, extent.xmax, extent.ymin, extent.ymax));
                    continue;
                }

                for (var row = 1; row <= option.Rows; row++)
                {
                    for (var column = 1; column <= option.Columns; column++)
                    {
                        var extent = GetExtentByScale(baseCode, option, row, column);
                        if (!ExtentsIntersect(
                                normalizedMinLon,
                                normalizedMaxLon,
                                normalizedMinLat,
                                normalizedMaxLat,
                                extent.xmin,
                                extent.xmax,
                                extent.ymin,
                                extent.ymax))
                        {
                            continue;
                        }

                        cells.Add(
                            new MapSheetCell(
                                $"{baseCode}{option.ScaleCode}{row:000}{column:000}",
                                extent.xmin,
                                extent.xmax,
                                extent.ymin,
                                extent.ymax));
                    }
                }
            }

            return cells
                .OrderByDescending(cell => cell.YMax)
                .ThenBy(cell => cell.XMin)
                .ToArray();
        }

        public static IReadOnlyList<MapSheetCell> GetCellsForPoint(double lon, double lat, SmallScaleMapSheetOption option)
        {
            const double epsilon = 1e-10;

            return GetCellsForExtent(
                lon - epsilon,
                lon + epsilon,
                lat - epsilon,
                lat + epsilon,
                option);
        }

        private static string[] Compute100kCodes(double xmin, double xmax, double ymin, double ymax)
        {
            var normalizedMinLat = Math.Max(0, ymin);
            var normalizedMaxLat = Math.Max(0, ymax);
            var codes = new HashSet<string>(StringComparer.Ordinal);
            var (yStart, yEnd) = MapSheetCoverageUtils.GetInclusiveIndexRange(normalizedMinLat, normalizedMaxLat, 4.0);

            for (var yIndex = yStart; yIndex <= yEnd; yIndex++)
            {
                var bandLat = yIndex * 4.0 + 2.0;
                var lonStep = bandLat < 60.0 ? 6.0 : bandLat < 76.0 ? 12.0 : 24.0;
                var (xStart, xEnd) = MapSheetCoverageUtils.GetInclusiveIndexRange(xmin + 180.0, xmax + 180.0, lonStep);

                for (var xIndex = xStart; xIndex <= xEnd; xIndex++)
                {
                    var rowLetter = (char)('A' + yIndex);
                    codes.Add($"{rowLetter}{xIndex + 1}");
                }
            }

            return codes
                .OrderBy(code => code[0])
                .ThenBy(code => int.Parse(code[1..]))
                .ToArray();
        }

        private static (double xmin, double xmax, double ymin, double ymax) Get100kMapExtent(string mapCode)
        {
            var rowLetter = mapCode[0];
            var colNumber = int.Parse(mapCode[1..]);
            var rowIndex = Math.Max(0, rowLetter - 'A');
            var baseLat = rowIndex * 4.0;
            var bandCenterLat = baseLat + 2.0;
            var lonStep = Math.Abs(bandCenterLat) < 60.0 ? 6.0 : Math.Abs(bandCenterLat) < 76.0 ? 12.0 : 24.0;
            var baseLon = (colNumber - 1) * lonStep - 180.0;
            var xmax = baseLon + lonStep;
            var ymax = baseLat + 4.0;

            if (Math.Abs(baseLat) >= 88)
            {
                xmax = 180.0;
                ymax = baseLat >= 0 ? 90.0 : -90.0;
            }

            return (baseLon, xmax, baseLat, ymax);
        }

        private static (double xmin, double xmax, double ymin, double ymax) GetExtentByScale(
            string baseCode,
            SmallScaleMapSheetOption option,
            int row,
            int column)
        {
            var extent = Get100kMapExtent(baseCode);
            var latStep = (extent.ymax - extent.ymin) / option.Rows;
            var lonStep = (extent.xmax - extent.xmin) / option.Columns;
            var xmin = extent.xmin + (column - 1) * lonStep;
            var xmax = extent.xmin + column * lonStep;
            var ymin = extent.ymax - row * latStep;
            var ymax = extent.ymax - (row - 1) * latStep;
            return (xmin, xmax, ymin, ymax);
        }

        private static bool ExtentsIntersect(
            double minLon,
            double maxLon,
            double minLat,
            double maxLat,
            double cellMinLon,
            double cellMaxLon,
            double cellMinLat,
            double cellMaxLat)
        {
            return cellMaxLon >= minLon &&
                   cellMinLon <= maxLon &&
                   cellMaxLat >= minLat &&
                   cellMinLat <= maxLat;
        }
    }
}
