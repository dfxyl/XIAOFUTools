#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.General.InternetTileDownload.Services
{
    internal sealed class InternetTileCoverageProbeResult
    {
        public int CheckedTileCount { get; init; }

        public int AvailableTileCount { get; init; }

        public bool HasCoverage => AvailableTileCount > 0;
    }

    internal static class InternetTileCoverageProbeService
    {
        public static IReadOnlyList<InternetTileDefinition> CreateSample(
            IReadOnlyList<InternetTileDefinition> tiles,
            int maxSampleCount = 12)
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

        public static async Task<InternetTileCoverageProbeResult> ProbeAsync(
            IInternetTileHttpClient httpClient,
            InternetTileServiceDefinition serviceDefinition,
            string levelId,
            IReadOnlyList<InternetTileDefinition> tiles,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentNullException.ThrowIfNull(serviceDefinition);
            ArgumentException.ThrowIfNullOrWhiteSpace(levelId);

            if (tiles.Count == 0)
            {
                return new InternetTileCoverageProbeResult();
            }

            var availableTileCount = 0;
            foreach (var tile in tiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var url = InternetTileRequestExpander.Expand(serviceDefinition, levelId, tile.Row, tile.Column);
                    var bytes = await httpClient.GetBytesAsync(url, cancellationToken);
                    if (bytes.Length > 0)
                    {
                        availableTileCount++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Ignore individual probe failures and rely on aggregate availability.
                }
            }

            return new InternetTileCoverageProbeResult
            {
                CheckedTileCount = tiles.Count,
                AvailableTileCount = availableTileCount
            };
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
