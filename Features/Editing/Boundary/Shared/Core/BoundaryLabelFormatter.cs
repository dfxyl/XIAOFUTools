using System;
using System.Globalization;

namespace XIAOFUTools.Features.Editing.Boundary.Shared.Core
{
    internal static class BoundaryLabelFormatter
    {
        public static string FormatPointLabel(int index, string prefix, string suffix)
        {
            return $"{prefix}{index}{suffix}";
        }

        public static string FormatEdgeLabel(
            double length,
            int decimals,
            bool padZeros,
            string prefix,
            string suffix)
        {
            if (decimals < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(decimals), "小数位数不能小于零。");
            }

            var number = padZeros
                ? length.ToString($"F{decimals}", CultureInfo.CurrentCulture)
                : Math.Round(length, decimals).ToString(CultureInfo.CurrentCulture);
            return $"{prefix}{number}{suffix}";
        }
    }
}
