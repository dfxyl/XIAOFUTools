using System;

namespace XIAOFUTools.Shared
{
    internal static class MapSheetCoverageUtils
    {
        private const double RelativeToleranceFactor = 1e-6;
        private const double MinimumTolerance = 1e-9;

        public static (double start, double end) AlignToGrid(double min, double max, double cellSize)
        {
            if (cellSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Cell size must be greater than zero.");

            var start = IsOnGridLine(min, cellSize)
                ? Math.Round(min / cellSize) * cellSize
                : Math.Floor((min - GetTolerance(cellSize)) / cellSize) * cellSize;

            var end = IsOnGridLine(max, cellSize)
                ? Math.Round(max / cellSize) * cellSize
                : Math.Ceiling((max + GetTolerance(cellSize)) / cellSize) * cellSize;

            return (start, end);
        }

        public static (int start, int end) GetInclusiveIndexRange(double min, double max, double step)
        {
            if (step <= 0)
                throw new ArgumentOutOfRangeException(nameof(step), "Step must be greater than zero.");

            var start = IsOnGridLine(min, step)
                ? (int)Math.Round(min / step)
                : (int)Math.Floor((min - GetTolerance(step)) / step);

            var end = IsOnGridLine(max, step)
                ? (int)Math.Round(max / step)
                : (int)Math.Floor((max + GetTolerance(step)) / step);

            return (start, end);
        }

        public static double GetTolerance(double step)
        {
            if (step <= 0)
                throw new ArgumentOutOfRangeException(nameof(step), "Step must be greater than zero.");

            return Math.Max(Math.Abs(step) * RelativeToleranceFactor, MinimumTolerance);
        }

        private static bool IsOnGridLine(double value, double step)
        {
            var remainder = Math.Abs(value % step);
            return remainder <= MinimumTolerance || Math.Abs(step - remainder) <= MinimumTolerance;
        }
    }
}
