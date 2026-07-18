using System;
using System.Collections.Generic;

namespace XIAOFUTools.Features.Editing.Boundary.Shared.Core
{
    internal static class BoundaryRingNormalizer
    {
        public static IReadOnlyList<T> Normalize<T>(
            IEnumerable<T> source,
            Func<T, BoundaryVertex> coordinateSelector,
            double tolerance)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(coordinateSelector);
            if (tolerance <= 0 || double.IsNaN(tolerance) || double.IsInfinity(tolerance))
            {
                throw new ArgumentOutOfRangeException(nameof(tolerance), "容差必须是大于零的有限数值。");
            }

            var result = new List<T>();
            foreach (var item in source)
            {
                if (result.Count == 0 ||
                    !AreSame(coordinateSelector(result[^1]), coordinateSelector(item), tolerance))
                {
                    result.Add(item);
                }
            }

            if (result.Count > 1 &&
                AreSame(coordinateSelector(result[0]), coordinateSelector(result[^1]), tolerance))
            {
                result.RemoveAt(result.Count - 1);
            }

            return result;
        }

        private static bool AreSame(BoundaryVertex first, BoundaryVertex second, double tolerance)
        {
            return Math.Abs(first.X - second.X) < tolerance &&
                   Math.Abs(first.Y - second.Y) < tolerance;
        }
    }
}
