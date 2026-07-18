#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload.Infrastructure.Google
{
    internal static class GoogleQuadtreeCodec
    {
        private const int PacketChunkLength = 4;

        public static string CreatePath(int row, int column, int level)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(row);
            ArgumentOutOfRangeException.ThrowIfNegative(column);
            ArgumentOutOfRangeException.ThrowIfNegative(level);

            var tileCount = 1 << level;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(row, tileCount - 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(column, tileCount - 1);

            var chars = new char[level + 1];
            for (var index = level; index >= 0; index--)
            {
                var rowBit = row & 1;
                var columnBit = column & 1;
                row >>= 1;
                column >>= 1;

                chars[index] = (char)((rowBit << 1) | (rowBit ^ columnBit) | 0x30);
            }

            return new string(chars);
        }

        public static int EncodeDate(DateOnly date)
            => ((date.Year & 0x7FF) << 9) | ((date.Month & 0xF) << 5) | (date.Day & 0x1F);

        public static DateOnly DecodeDate(int encodedDate)
            => new(encodedDate >> 9, (encodedDate >> 5) & 0xF, encodedDate & 0x1F);

        public static int GetSubIndex(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            if (path.Any(character => character is not '0' and not '1' and not '2' and not '3'))
            {
                throw new ArgumentException("Quad tree path can only contain 0, 1, 2, and 3.", nameof(path));
            }

            if (path[0] != '0')
            {
                throw new ArgumentException("Quad tree path must start with 0.", nameof(path));
            }

            if (path.Length <= PacketChunkLength)
            {
                return GetRootSubIndex(path);
            }

            var chunkStart = ((path.Length - 1) / PacketChunkLength) * PacketChunkLength;
            var chunk = path.Substring(chunkStart);
            return GetRootSubIndex(chunk) + (chunk[0] - '0') * 85 + 1;
        }

        public static IEnumerable<string> EnumeratePacketPaths(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            for (var end = PacketChunkLength; end < path.Length; end += PacketChunkLength)
            {
                yield return path.Substring(0, end);
            }
        }

        private static int GetRootSubIndex(string path)
        {
            var index = 0;

            for (var position = 1; position < path.Length; position++)
            {
                index *= 4;
                index += path[position] - '0' + 1;
            }

            return index;
        }
    }
}
