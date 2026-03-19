#nullable enable

using System;
using System.Globalization;
using System.IO;

namespace XIAOFUTools.Tools.Common.RasterExport
{
    internal static class ArcGisRasterTileCache
    {
        internal readonly record struct PixelWindow(int StartX, int EndXExclusive, int StartY, int EndYExclusive)
        {
            public bool IsEmpty => EndXExclusive <= StartX || EndYExclusive <= StartY;
        }

        public static string BuildTilePath(string tileDirectory, string levelId, int row, int column)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tileDirectory);
            ArgumentException.ThrowIfNullOrWhiteSpace(levelId);

            return Path.Combine(tileDirectory, $"{levelId}_r{row}_c{column}.png");
        }

        public static string GetWorldFilePath(string rasterPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rasterPath);
            return Path.ChangeExtension(rasterPath, ".pgw");
        }

        public static string BuildWorldFileContent(
            double minX,
            double minY,
            double maxX,
            double maxY,
            int pixelWidth,
            int pixelHeight)
        {
            if (pixelWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            }

            if (pixelHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelHeight));
            }

            double pixelSizeX = (maxX - minX) / pixelWidth;
            double pixelSizeY = (maxY - minY) / pixelHeight;
            double upperLeftCenterX = minX + pixelSizeX / 2d;
            double upperLeftCenterY = maxY - pixelSizeY / 2d;

            return string.Join(
                Environment.NewLine,
                pixelSizeX.ToString("R", CultureInfo.InvariantCulture),
                "0",
                "0",
                (-pixelSizeY).ToString("R", CultureInfo.InvariantCulture),
                upperLeftCenterX.ToString("R", CultureInfo.InvariantCulture),
                upperLeftCenterY.ToString("R", CultureInfo.InvariantCulture));
        }

        public static PixelWindow GetPixelWindow(
            double rasterMinX,
            double rasterMinY,
            double rasterMaxX,
            double rasterMaxY,
            int pixelWidth,
            int pixelHeight,
            double clipMinX,
            double clipMinY,
            double clipMaxX,
            double clipMaxY)
        {
            if (pixelWidth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelWidth));
            }

            if (pixelHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pixelHeight));
            }

            double intersectMinX = Math.Max(rasterMinX, clipMinX);
            double intersectMinY = Math.Max(rasterMinY, clipMinY);
            double intersectMaxX = Math.Min(rasterMaxX, clipMaxX);
            double intersectMaxY = Math.Min(rasterMaxY, clipMaxY);
            if (intersectMinX >= intersectMaxX || intersectMinY >= intersectMaxY)
            {
                return new PixelWindow(0, 0, 0, 0);
            }

            double pixelSizeX = (rasterMaxX - rasterMinX) / pixelWidth;
            double pixelSizeY = (rasterMaxY - rasterMinY) / pixelHeight;
            int startX = ClampToPixelIndex(Math.Floor((intersectMinX - rasterMinX) / pixelSizeX), pixelWidth);
            int endXExclusive = ClampToPixelIndex(Math.Ceiling((intersectMaxX - rasterMinX) / pixelSizeX), pixelWidth);
            int startY = ClampToPixelIndex(Math.Floor((rasterMaxY - intersectMaxY) / pixelSizeY), pixelHeight);
            int endYExclusive = ClampToPixelIndex(Math.Ceiling((rasterMaxY - intersectMinY) / pixelSizeY), pixelHeight);
            return new PixelWindow(startX, endXExclusive, startY, endYExclusive);
        }

        private static int ClampToPixelIndex(double value, int limit)
        {
            if (value <= 0)
            {
                return 0;
            }

            if (value >= limit)
            {
                return limit;
            }

            return (int)value;
        }
    }
}
