#nullable enable
using System;

namespace XIAOFUTools.Shared
{
    internal static class MapSheetAssignmentUtils
    {
        public static bool ShouldUpdateValue(object? currentValue, string? nextValue)
        {
            return !string.Equals(Normalize(currentValue), Normalize(nextValue), StringComparison.Ordinal);
        }

        public static string Normalize(object? value)
        {
            return value?.ToString() ?? string.Empty;
        }
    }
}
#nullable restore
