#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure.Google
{
    internal sealed class GoogleTileNodeState
    {
        public string Path { get; init; } = string.Empty;

        public int DefaultImageryEpoch { get; init; }

        public IReadOnlyList<GoogleDatedTileInfo> DatedTiles { get; init; } = Array.Empty<GoogleDatedTileInfo>();
    }

    internal sealed class GoogleTimeMachineClient
    {
        private const string DbRootUrl = "https://khmdb.google.com/dbRoot.v5?db=tm&hl=en&gl=us&output=proto";
        private readonly Dictionary<string, GoogleQuadtreePacketInfo> _packetCache = new(StringComparer.Ordinal);
        private readonly HistoricalHttpClient _httpClient = new();
        private readonly string? _cacheDirectory;
        private byte[]? _encryptionData;
        private uint? _quadtreeVersion;

        public GoogleTimeMachineClient(string? cacheDirectory)
        {
            _cacheDirectory = cacheDirectory;
        }

        public async Task<GoogleTileNodeState?> GetNodeAsync(HistoricalTileDefinition tile, CancellationToken cancellationToken = default)
        {
            var path = string.IsNullOrWhiteSpace(tile.Path)
                ? GoogleQuadtreeCodec.CreatePath(tile.Row, tile.Column, tile.ZoomLevel)
                : tile.Path;

            var rootEpoch = (int)await GetQuadtreeVersionAsync(cancellationToken);
            var packet = await GetPacketAsync("0", rootEpoch, cancellationToken);

            foreach (var packetPath in GoogleQuadtreeCodec.EnumeratePacketPaths(path))
            {
                var packetNode = packet.Nodes.FirstOrDefault(node => node.Index == GoogleQuadtreeCodec.GetSubIndex(packetPath));
                if (packetNode == null)
                {
                    return null;
                }

                packet = await GetPacketAsync(packetPath, packetNode.CacheNodeEpoch, cancellationToken);
            }

            var targetNode = packet.Nodes.FirstOrDefault(node => node.Index == GoogleQuadtreeCodec.GetSubIndex(path));
            if (targetNode == null)
            {
                return null;
            }

            var defaultImageryEpoch = targetNode.Layers
                .FirstOrDefault(layer => layer.Type == GoogleLayerType.Imagery)
                ?.LayerEpoch ?? 0;

            var datedTiles = targetNode.Layers
                .Where(layer => layer.Type == GoogleLayerType.ImageryHistory)
                .SelectMany(layer => layer.DatedTiles)
                .GroupBy(tileInfo => tileInfo.Date)
                .Select(group => group.OrderByDescending(tileInfo => tileInfo.Provider).First())
                .OrderByDescending(tileInfo => tileInfo.Date)
                .ToArray();

            return new GoogleTileNodeState
            {
                Path = path,
                DefaultImageryEpoch = defaultImageryEpoch,
                DatedTiles = datedTiles
            };
        }

        public async Task<byte[]?> DownloadTileAsync(
            GoogleTileNodeState node,
            GoogleDatedTileInfo datedTile,
            CancellationToken cancellationToken = default)
        {
            await EnsureDbRootLoadedAsync(cancellationToken);

            var url = BuildTileUrl(node, datedTile);
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var encryptedPayload = await _httpClient.GetBytesAsync(
                url,
                GetCacheDirectory("tiles"),
                cacheKey: url,
                cacheDuration: TimeSpan.FromDays(30),
                cancellationToken);

            return GooglePacketCodec.Decrypt(encryptedPayload, _encryptionData!);
        }

        private async Task<uint> GetQuadtreeVersionAsync(CancellationToken cancellationToken)
        {
            await EnsureDbRootLoadedAsync(cancellationToken);
            return _quadtreeVersion ?? throw new InvalidOperationException("Google dbroot did not provide a quadtree version.");
        }

        private async Task<GoogleQuadtreePacketInfo> GetPacketAsync(string path, int epoch, CancellationToken cancellationToken)
        {
            var cacheKey = $"{path}:{epoch}";
            if (_packetCache.TryGetValue(cacheKey, out var cachedPacket))
            {
                return cachedPacket;
            }

            var url = $"https://khmdb.google.com/flatfile?db=tm&qp-{path}-q.{epoch}";
            await EnsureDbRootLoadedAsync(cancellationToken);

            var encryptedPayload = await _httpClient.GetBytesAsync(
                url,
                GetCacheDirectory("packets"),
                cacheKey,
                cacheDuration: TimeSpan.FromDays(30),
                cancellationToken);

            var decryptedPayload = GooglePacketCodec.DecryptAndDecompress(encryptedPayload, _encryptionData!);
            var packet = GoogleQuadtreePacketParser.Parse(decryptedPayload);
            _packetCache[cacheKey] = packet;
            return packet;
        }

        private async Task EnsureDbRootLoadedAsync(CancellationToken cancellationToken)
        {
            if (_quadtreeVersion.HasValue && _encryptionData is { Length: > 0 })
            {
                return;
            }

            var payload = await _httpClient.GetBytesAsync(
                DbRootUrl,
                GetCacheDirectory("dbroot"),
                cacheKey: DbRootUrl,
                cacheDuration: TimeSpan.FromDays(1),
                cancellationToken);

            var envelope = GoogleDbRootParser.ParseEnvelope(payload);
            _encryptionData = envelope.EncryptionData;
            var dbRootBytes = GooglePacketCodec.DecryptAndDecompress(envelope.EncryptedDbRootData, envelope.EncryptionData);
            _quadtreeVersion = GoogleDbRootParser.ParseDbRoot(dbRootBytes).QuadtreeVersion;
        }

        private string? BuildTileUrl(GoogleTileNodeState node, GoogleDatedTileInfo datedTile)
        {
            if (datedTile.Provider != 0)
            {
                return $"https://khmdb.google.com/flatfile?db=tm&f1-{node.Path}-i.{datedTile.TileEpoch}-{GoogleQuadtreeCodec.EncodeDate(datedTile.Date):x}";
            }

            if (node.DefaultImageryEpoch <= 0)
            {
                return null;
            }

            return $"https://kh.google.com/flatfile?f1-{node.Path}-i.{node.DefaultImageryEpoch}";
        }

        private string? GetCacheDirectory(string segment)
            => string.IsNullOrWhiteSpace(_cacheDirectory) ? null : Path.Combine(_cacheDirectory, segment);
    }
}
