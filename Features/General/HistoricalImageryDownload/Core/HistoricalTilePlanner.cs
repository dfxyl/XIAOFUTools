#nullable enable

using System.Collections.Generic;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Core
{
    internal static class HistoricalTilePlanner
    {
        public static HistoricalTilePlan PlanGoogle(double minX, double minY, double maxX, double maxY, int zoomLevel)
        {
            if (minX > maxX)
            {
                (minX, maxX) = (maxX, minX);
            }

            if (minY > maxY)
            {
                (minY, maxY) = (maxY, minY);
            }

            var minColumn = HistoricalTileSchema.GetGoogleColumn(minX, zoomLevel);
            var maxColumn = HistoricalTileSchema.GetGoogleColumn(maxX, zoomLevel);
            var minRow = HistoricalTileSchema.GetGoogleRow(minY, zoomLevel);
            var maxRow = HistoricalTileSchema.GetGoogleRow(maxY, zoomLevel);

            var tiles = new List<HistoricalTileDefinition>();
            for (var row = minRow; row <= maxRow; row++)
            {
                for (var column = minColumn; column <= maxColumn; column++)
                {
                    tiles.Add(HistoricalTileSchema.CreateGoogleTile(
                        row,
                        column,
                        zoomLevel,
                        (column - minColumn) * HistoricalTileSchema.TileSize,
                        (maxRow - row) * HistoricalTileSchema.TileSize));
                }
            }

            var tileSpan = HistoricalTileSchema.GetGoogleTileSpan(zoomLevel);
            var topLeftY = HistoricalTileSchema.GeographicWorldMin + (maxRow + 1) * tileSpan;
            var pixelSize = tileSpan / HistoricalTileSchema.TileSize;

            return new HistoricalTilePlan
            {
                Scheme = HistoricalTileScheme.GoogleGeographic,
                PixelWidth = (maxColumn - minColumn + 1) * HistoricalTileSchema.TileSize,
                PixelHeight = (maxRow - minRow + 1) * HistoricalTileSchema.TileSize,
                OriginX = HistoricalTileSchema.GeographicWorldMin + minColumn * tileSpan,
                OriginY = topLeftY,
                PixelSizeX = pixelSize,
                PixelSizeY = -pixelSize,
                Tiles = tiles
            };
        }

        public static HistoricalTilePlan PlanWebMercator(double minX, double minY, double maxX, double maxY, int zoomLevel)
        {
            if (minX > maxX)
            {
                (minX, maxX) = (maxX, minX);
            }

            if (minY > maxY)
            {
                (minY, maxY) = (maxY, minY);
            }

            var minColumn = HistoricalTileSchema.GetWebMercatorColumn(minX, zoomLevel);
            var maxColumn = HistoricalTileSchema.GetWebMercatorColumn(maxX, zoomLevel);
            var minRow = HistoricalTileSchema.GetWebMercatorRow(maxY, zoomLevel);
            var maxRow = HistoricalTileSchema.GetWebMercatorRow(minY, zoomLevel);

            var tiles = new List<HistoricalTileDefinition>();
            for (var row = minRow; row <= maxRow; row++)
            {
                for (var column = minColumn; column <= maxColumn; column++)
                {
                    tiles.Add(HistoricalTileSchema.CreateWebMercatorTile(
                        row,
                        column,
                        zoomLevel,
                        (column - minColumn) * HistoricalTileSchema.TileSize,
                        (row - minRow) * HistoricalTileSchema.TileSize));
                }
            }

            var tileSpan = HistoricalTileSchema.GetWebMercatorTileSpan(zoomLevel);

            return new HistoricalTilePlan
            {
                Scheme = HistoricalTileScheme.WebMercator,
                PixelWidth = (maxColumn - minColumn + 1) * HistoricalTileSchema.TileSize,
                PixelHeight = (maxRow - minRow + 1) * HistoricalTileSchema.TileSize,
                OriginX = -HistoricalTileSchema.WebMercatorOriginShift + minColumn * tileSpan,
                OriginY = HistoricalTileSchema.WebMercatorOriginShift - minRow * tileSpan,
                PixelSizeX = tileSpan / HistoricalTileSchema.TileSize,
                PixelSizeY = -(tileSpan / HistoricalTileSchema.TileSize),
                Tiles = tiles
            };
        }
    }
}
