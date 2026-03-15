#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Tools.InternetTileDownload.Core
{
    internal static class InternetTilePlanner
    {
        public static InternetTilePlan Plan(
            InternetTileServiceDefinition definition,
            string levelId,
            double minX,
            double minY,
            double maxX,
            double maxY)
        {
            ArgumentNullException.ThrowIfNull(definition);

            if (minX > maxX)
            {
                (minX, maxX) = (maxX, minX);
            }

            if (minY > maxY)
            {
                (minY, maxY) = (maxY, minY);
            }

            var level = definition.Levels.FirstOrDefault(item => string.Equals(item.LevelId, levelId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"未找到级别 {levelId} 的切片矩阵定义。");

            var tileSpanX = level.Resolution * level.TileWidth;
            var tileSpanY = level.Resolution * level.TileHeight;
            var epsilonX = tileSpanX * 1e-9d;
            var epsilonY = tileSpanY * 1e-9d;

            var rawMinColumn = (int)Math.Floor((minX - level.TopLeftX) / tileSpanX);
            var rawMaxColumn = (int)Math.Floor((maxX - level.TopLeftX - epsilonX) / tileSpanX);
            var rawMinRow = (int)Math.Floor((level.TopLeftY - maxY) / tileSpanY);
            var rawMaxRow = (int)Math.Floor((level.TopLeftY - minY - epsilonY) / tileSpanY);

            if (rawMaxColumn < 0 || rawMinColumn >= level.MatrixWidth || rawMaxRow < 0 || rawMinRow >= level.MatrixHeight)
            {
                throw new InvalidOperationException("下载范围超出服务覆盖范围。");
            }

            var minColumn = Clamp(rawMinColumn, 0, level.MatrixWidth - 1);
            var maxColumn = Clamp(rawMaxColumn, 0, level.MatrixWidth - 1);
            var minRow = Clamp(rawMinRow, 0, level.MatrixHeight - 1);
            var maxRow = Clamp(rawMaxRow, 0, level.MatrixHeight - 1);

            if (minColumn > maxColumn || minRow > maxRow)
            {
                throw new InvalidOperationException("下载范围超出服务覆盖范围。");
            }

            var tiles = new List<InternetTileDefinition>();
            for (var row = minRow; row <= maxRow; row++)
            {
                for (var column = minColumn; column <= maxColumn; column++)
                {
                    var tileMinX = level.TopLeftX + column * tileSpanX;
                    var tileMaxY = level.TopLeftY - row * tileSpanY;
                    tiles.Add(new InternetTileDefinition
                    {
                        LevelId = level.LevelId,
                        Row = row,
                        Column = column,
                        PixelOffsetX = (column - minColumn) * level.TileWidth,
                        PixelOffsetY = (row - minRow) * level.TileHeight,
                        MinX = tileMinX,
                        MinY = tileMaxY - tileSpanY,
                        MaxX = tileMinX + tileSpanX,
                        MaxY = tileMaxY
                    });
                }
            }

            return new InternetTilePlan
            {
                LevelId = level.LevelId,
                PixelWidth = (maxColumn - minColumn + 1) * level.TileWidth,
                PixelHeight = (maxRow - minRow + 1) * level.TileHeight,
                OriginX = level.TopLeftX + minColumn * tileSpanX,
                OriginY = level.TopLeftY - minRow * tileSpanY,
                PixelSizeX = level.Resolution,
                PixelSizeY = -level.Resolution,
                Tiles = tiles
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }

            return Math.Clamp(value, min, max);
        }
    }
}
