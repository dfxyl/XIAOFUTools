using System;
using System.Collections.Generic;

namespace XIAOFUTools.Features.Analysis.LandClassTable.Core
{
    internal readonly record struct LandClassTableRowRange(int StartIndex, int EndIndex);

    internal static class LandClassTableRowGrouping
    {
        internal static IReadOnlyList<LandClassTableRowRange> GetPlotNameMergeRanges(
            IReadOnlyList<LandClassTableRow> rows)
        {
            var ranges = new List<LandClassTableRowRange>();
            if (rows == null)
            {
                return ranges;
            }

            int startIndex = 0;
            while (startIndex < rows.Count)
            {
                var firstRow = rows[startIndex];
                string projectName = firstRow.ProjectName?.Trim() ?? string.Empty;
                string plotName = firstRow.PlotName?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(plotName))
                {
                    startIndex++;
                    continue;
                }

                int endIndex = startIndex;
                while (endIndex + 1 < rows.Count &&
                       string.Equals(rows[endIndex + 1].ProjectName?.Trim(), projectName, StringComparison.Ordinal) &&
                       string.Equals(rows[endIndex + 1].PlotName?.Trim(), plotName, StringComparison.Ordinal))
                {
                    endIndex++;
                }

                if (endIndex > startIndex)
                {
                    ranges.Add(new LandClassTableRowRange(startIndex, endIndex));
                }

                startIndex = endIndex + 1;
            }

            return ranges;
        }
    }
}
