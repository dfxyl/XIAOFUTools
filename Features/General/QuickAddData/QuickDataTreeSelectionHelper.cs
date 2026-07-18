using System;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Features.General.QuickAddData
{
    public static class QuickDataTreeSelectionHelper
    {
        public static HashSet<string> BuildRangeSelection(
            IReadOnlyList<string> orderedKeys,
            string anchorKey,
            string targetKey,
            IEnumerable<string> existingKeys,
            bool additive)
        {
            var result = additive
                ? new HashSet<string>(existingKeys ?? Array.Empty<string>(), StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

            if (orderedKeys == null || string.IsNullOrWhiteSpace(anchorKey) || string.IsNullOrWhiteSpace(targetKey))
            {
                return result;
            }

            var anchorIndex = FindIndex(orderedKeys, anchorKey);
            var targetIndex = FindIndex(orderedKeys, targetKey);
            if (anchorIndex < 0 || targetIndex < 0)
            {
                return result;
            }

            var start = Math.Min(anchorIndex, targetIndex);
            var end = Math.Max(anchorIndex, targetIndex);
            for (var i = start; i <= end; i++)
            {
                result.Add(orderedKeys[i]);
            }

            return result;
        }

        private static int FindIndex(IReadOnlyList<string> orderedKeys, string key)
        {
            for (var i = 0; i < orderedKeys.Count; i++)
            {
                if (string.Equals(orderedKeys[i], key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
