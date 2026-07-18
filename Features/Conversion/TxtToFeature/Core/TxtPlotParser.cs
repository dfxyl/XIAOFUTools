using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace XIAOFUTools.Features.Conversion.TxtToFeature.Core
{
    internal static class TxtPlotParser
    {
        private static readonly Regex CoordinatePattern = new(
            @"^[^,]+,\d+,[+-]?[\d.]+,[+-]?[\d.]+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        internal static IReadOnlyList<PlotData> Parse(
            IEnumerable<string> lines,
            string fieldNamesText,
            bool swapXY)
        {
            var plots = new List<PlotData>();
            var currentPlot = new PlotData();
            var isInCoordinateSection = false;
            var fieldNames = (fieldNamesText ?? string.Empty)
                .Split(',')
                .Select(field => field.Trim())
                .ToArray();

            foreach (var rawLine in lines ?? Array.Empty<string>())
            {
                var line = rawLine?.Trim() ?? string.Empty;
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith("[属性描述]", StringComparison.Ordinal))
                {
                    isInCoordinateSection = false;
                    continue;
                }

                if (line.StartsWith("[地块坐标]", StringComparison.Ordinal))
                {
                    isInCoordinateSection = true;
                    continue;
                }

                if (!isInCoordinateSection)
                {
                    continue;
                }

                if (line.Contains(',') && line.EndsWith('@'))
                {
                    AddCompletedPlot(plots, currentPlot);
                    currentPlot = new PlotData();
                    ParseAttributeLine(line, currentPlot, fieldNames);
                }
                else if (CoordinatePattern.IsMatch(line))
                {
                    ParseCoordinateLine(line, currentPlot, swapXY);
                }
            }

            AddCompletedPlot(plots, currentPlot);
            return plots;
        }

        private static void ParseAttributeLine(string line, PlotData plot, IReadOnlyList<string> fieldNames)
        {
            var values = line.TrimEnd('@', ',').Split(',');
            for (var index = 0; index < Math.Min(values.Length, fieldNames.Count); index++)
            {
                var fieldName = fieldNames[index];
                if (!string.IsNullOrEmpty(fieldName) && fieldName != "@")
                {
                    plot.Attributes[fieldName] = values[index];
                }
            }
        }

        private static void ParseCoordinateLine(string line, PlotData plot, bool swapXY)
        {
            var parts = line.Split(',');
            if (parts.Length < 4 ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ringNumber) ||
                !TryParseCoordinate(parts[2], out var y) ||
                !TryParseCoordinate(parts[3], out var x))
            {
                return;
            }

            if (swapXY)
            {
                (x, y) = (y, x);
            }

            var ring = plot.Rings.FirstOrDefault(item => item.RingNumber == ringNumber);
            if (ring == null)
            {
                ring = new CoordinateRing { RingNumber = ringNumber };
                plot.Rings.Add(ring);
            }

            ring.Points.Add(new CoordinatePoint
            {
                PointName = parts[0].Trim(),
                RingNumber = ringNumber,
                X = x,
                Y = y
            });
        }

        private static bool TryParseCoordinate(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                   double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static void AddCompletedPlot(ICollection<PlotData> plots, PlotData plot)
        {
            if (plot.Rings.Any(ring => ring.Points.Count > 0))
            {
                plots.Add(plot);
            }
        }
    }
}
