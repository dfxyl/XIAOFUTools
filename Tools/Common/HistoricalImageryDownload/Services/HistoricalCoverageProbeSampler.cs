#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    internal static class HistoricalCoverageProbeSampler
    {
        public static IReadOnlyList<HistoricalTileDefinition> CreateSample(
            IReadOnlyList<HistoricalTileDefinition> tiles,
            int maxSampleCount = 16)
        {
            if (tiles.Count <= maxSampleCount)
            {
                return tiles.ToArray();
            }

            var selectedIndices = new HashSet<int>();
            AddIndex(selectedIndices, 0, tiles.Count);
            AddIndex(selectedIndices, tiles.Count - 1, tiles.Count);
            AddIndex(selectedIndices, tiles.Count / 2, tiles.Count);
            AddIndex(selectedIndices, tiles.Count / 4, tiles.Count);
            AddIndex(selectedIndices, (tiles.Count * 3) / 4, tiles.Count);

            var step = Math.Max(1, tiles.Count / maxSampleCount);
            for (var index = 0; index < tiles.Count && selectedIndices.Count < maxSampleCount; index += step)
            {
                AddIndex(selectedIndices, index, tiles.Count);
            }

            return selectedIndices
                .OrderBy(index => index)
                .Take(maxSampleCount)
                .Select(index => tiles[index])
                .ToArray();
        }

        private static void AddIndex(ISet<int> indices, int index, int count)
        {
            if (count <= 0)
            {
                return;
            }

            indices.Add(Math.Clamp(index, 0, count - 1));
        }
    }
}
