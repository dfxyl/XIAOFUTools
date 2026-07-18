using System;
using System.Collections.Generic;

namespace XIAOFUTools.Features.Analysis.LandClassTable.Core
{
    internal static class LandClassReportHeaderBuilder
    {
        internal static string BuildRightHolderName(IEnumerable<string> values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                string normalizedValue = value?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(normalizedValue) && seen.Add(normalizedValue))
                {
                    names.Add(normalizedValue);
                }
            }

            return string.Join("、", names);
        }
    }
}
