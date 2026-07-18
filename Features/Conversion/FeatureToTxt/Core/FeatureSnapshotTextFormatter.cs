using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt.Core
{
    internal sealed class FeatureSnapshotTextFormatter
    {
        private readonly FeatureCoordinateTextFormatter _coordinateFormatter = new();

        internal void Write(
            FeatureExportSnapshot snapshot,
            StringBuilder content,
            int featureIndex,
            FeatureToTxtExportConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(content);
            ArgumentNullException.ThrowIfNull(configuration);

            var normalizedIndex = Math.Max(1, featureIndex);
            var fieldValues = new List<string>
            {
                string.Empty,
                GetPlotAreaValue(snapshot),
                GetValueOrDefault(snapshot.GetMappedValue("地块编号"), normalizedIndex.ToString()),
                GetValueOrDefault(snapshot.GetMappedValue("地块名称"), $"地块{normalizedIndex}"),
                "面",
                snapshot.GetMappedValue("图幅号"),
                snapshot.GetMappedValue("地块用途"),
                snapshot.GetMappedValue("地类编码")
            };

            _coordinateFormatter.Write(snapshot.Rings, content, fieldValues, configuration);
        }

        internal static string GetPlotAreaValue(FeatureExportSnapshot snapshot)
        {
            var areaValue = snapshot.GetMappedValue("地块面积");
            if (TryParsePositiveNumber(areaValue, out var parsedArea))
            {
                if (snapshot.Area > 0d)
                {
                    var squareMeterRatio = Math.Abs(parsedArea - snapshot.Area) / snapshot.Area;
                    if (squareMeterRatio <= 0.01d)
                    {
                        return (parsedArea / 10000d).ToString("F4");
                    }

                    var hectareRatio = Math.Abs(parsedArea * 10000d - snapshot.Area) / snapshot.Area;
                    if (hectareRatio <= 0.01d)
                    {
                        return parsedArea.ToString("F4");
                    }
                }

                return parsedArea >= 1000d
                    ? (parsedArea / 10000d).ToString("F4")
                    : parsedArea.ToString("F4");
            }

            return (snapshot.Area / 10000d).ToString("F4");
        }

        private static string GetValueOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static bool TryParsePositiveNumber(string value, out double result)
        {
            result = 0d;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var styles = NumberStyles.Float | NumberStyles.AllowThousands;
            var trimmed = value.Trim();
            if (double.TryParse(trimmed, styles, CultureInfo.InvariantCulture, out result) && result > 0d)
            {
                return true;
            }

            if (double.TryParse(trimmed, styles, CultureInfo.CurrentCulture, out result) && result > 0d)
            {
                return true;
            }

            return double.TryParse(
                       trimmed.Replace(",", string.Empty),
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out result) && result > 0d;
        }
    }
}
