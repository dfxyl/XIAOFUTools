#nullable enable

using System;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Infrastructure.Google;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Core
{
    internal static class HistoricalTileSchema
    {
        internal const int TileSize = 256;
        internal const double GeographicWorldMin = -180d;
        internal const double GeographicWorldMax = 180d;
        internal const double WebMercatorOriginShift = 20037508.342789244d;

        public static int GetGoogleTileCount(int zoomLevel) => 1 << zoomLevel;

        public static double GetGoogleTileSpan(int zoomLevel) => 360d / GetGoogleTileCount(zoomLevel);

        public static int GetGoogleColumn(double longitude, int zoomLevel)
            => ClampTileIndex((int)Math.Floor((longitude - GeographicWorldMin) / GetGoogleTileSpan(zoomLevel)), zoomLevel);

        public static int GetGoogleRow(double latitude, int zoomLevel)
            => ClampTileIndex((int)Math.Floor((latitude - GeographicWorldMin) / GetGoogleTileSpan(zoomLevel)), zoomLevel);

        public static HistoricalTileDefinition CreateGoogleTile(int row, int column, int zoomLevel, int pixelOffsetX, int pixelOffsetY)
        {
            var tileSpan = GetGoogleTileSpan(zoomLevel);
            var minX = GeographicWorldMin + column * tileSpan;
            var minY = GeographicWorldMin + row * tileSpan;

            return new HistoricalTileDefinition
            {
                ZoomLevel = zoomLevel,
                Row = row,
                Column = column,
                Path = GoogleQuadtreeCodec.CreatePath(row, column, zoomLevel),
                PixelOffsetX = pixelOffsetX,
                PixelOffsetY = pixelOffsetY,
                MinX = minX,
                MinY = minY,
                MaxX = minX + tileSpan,
                MaxY = minY + tileSpan
            };
        }

        public static int GetWebMercatorTileCount(int zoomLevel) => 1 << zoomLevel;

        public static double GetWebMercatorTileSpan(int zoomLevel) => (WebMercatorOriginShift * 2d) / GetWebMercatorTileCount(zoomLevel);

        public static int GetWebMercatorColumn(double x, int zoomLevel)
            => ClampWebMercatorIndex((int)Math.Floor((x + WebMercatorOriginShift) / GetWebMercatorTileSpan(zoomLevel)), zoomLevel);

        public static int GetWebMercatorRow(double y, int zoomLevel)
            => ClampWebMercatorIndex((int)Math.Floor((WebMercatorOriginShift - y) / GetWebMercatorTileSpan(zoomLevel)), zoomLevel);

        public static HistoricalTileDefinition CreateWebMercatorTile(int row, int column, int zoomLevel, int pixelOffsetX, int pixelOffsetY)
        {
            var tileSpan = GetWebMercatorTileSpan(zoomLevel);
            var minX = -WebMercatorOriginShift + column * tileSpan;
            var maxY = WebMercatorOriginShift - row * tileSpan;

            return new HistoricalTileDefinition
            {
                ZoomLevel = zoomLevel,
                Row = row,
                Column = column,
                PixelOffsetX = pixelOffsetX,
                PixelOffsetY = pixelOffsetY,
                MinX = minX,
                MinY = maxY - tileSpan,
                MaxX = minX + tileSpan,
                MaxY = maxY
            };
        }

        private static int ClampTileIndex(int value, int zoomLevel)
        {
            var tileCount = GetGoogleTileCount(zoomLevel);
            return Math.Clamp(value, 0, tileCount - 1);
        }

        private static int ClampWebMercatorIndex(int value, int zoomLevel)
        {
            var tileCount = GetWebMercatorTileCount(zoomLevel);
            return Math.Clamp(value, 0, tileCount - 1);
        }
    }
}
