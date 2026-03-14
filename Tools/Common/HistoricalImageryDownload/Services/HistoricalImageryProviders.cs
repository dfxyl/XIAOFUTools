#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Tools.HistoricalImageryDownload.Core;
using XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure;
using XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure.Google;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    internal interface IHistoricalImageryProvider
    {
        HistoricalImageryProviderType ProviderType { get; }

        HistoricalTileScheme TileScheme { get; }

        string SourceSpatialReferenceText { get; }

        Task<IReadOnlyList<HistoricalVersionItem>> QueryVersionsAsync(
            HistoricalVersionQuery query,
            CancellationToken cancellationToken = default);

        Task<HistoricalCoverageProbeResult> ProbeCoverageAsync(
            HistoricalCoverageProbeRequest request,
            CancellationToken cancellationToken = default);

        Task<HistoricalTileDownloadResult> DownloadTileAsync(
            HistoricalTileDownloadRequest request,
            CancellationToken cancellationToken = default);
    }

    internal sealed class HistoricalVersionQuery
    {
        public double Longitude { get; set; }

        public double Latitude { get; set; }

        public int ZoomLevel { get; set; }

        public bool IncludeAllVersions { get; set; }
    }

    internal sealed class HistoricalImageryProviderFactory
    {
        private readonly Dictionary<HistoricalImageryProviderType, IHistoricalImageryProvider> _providers;

        public HistoricalImageryProviderFactory(
            IHistoricalImageryProvider googleProvider,
            IHistoricalImageryProvider waybackProvider)
        {
            _providers = new Dictionary<HistoricalImageryProviderType, IHistoricalImageryProvider>
            {
                [googleProvider.ProviderType] = googleProvider,
                [waybackProvider.ProviderType] = waybackProvider
            };
        }

        public IHistoricalImageryProvider GetProvider(HistoricalImageryProviderType providerType)
        {
            if (_providers.TryGetValue(providerType, out var provider))
            {
                return provider;
            }

            throw new InvalidOperationException($"Provider '{providerType}' is not registered.");
        }
    }

    internal sealed class GoogleHistoricalImageryProvider : IHistoricalImageryProvider
    {
        private readonly GoogleTimeMachineClient _client;

        internal sealed record GoogleTileSelectionResolution(
            GoogleDatedTileInfo DatedTile,
            bool UsedNearestDateFallback);

        public GoogleHistoricalImageryProvider(string? cacheDirectory = null)
        {
            var providerCacheDirectory = cacheDirectory ?? CachePathProvider.GetProviderPath(
                CachePathProvider.GetRootPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
                HistoricalImageryProviderType.GoogleEarth);
            _client = new GoogleTimeMachineClient(providerCacheDirectory);
        }

        public HistoricalImageryProviderType ProviderType => HistoricalImageryProviderType.GoogleEarth;

        public HistoricalTileScheme TileScheme => HistoricalTileScheme.GoogleGeographic;

        public string SourceSpatialReferenceText => "EPSG:4326";

        public async Task<IReadOnlyList<HistoricalVersionItem>> QueryVersionsAsync(
            HistoricalVersionQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = HistoricalTileSchema.GetGoogleRow(query.Latitude, query.ZoomLevel);
            var column = HistoricalTileSchema.GetGoogleColumn(query.Longitude, query.ZoomLevel);
            var tile = HistoricalTileSchema.CreateGoogleTile(row, column, query.ZoomLevel, 0, 0);
            var node = await _client.GetNodeAsync(tile, cancellationToken);
            if (node == null)
            {
                return Array.Empty<HistoricalVersionItem>();
            }

            return node.DatedTiles
                .Where(tileInfo => tileInfo.Date != default)
                .GroupBy(tileInfo => tileInfo.Date)
                .Select(group => new HistoricalVersionItem
                {
                    Provider = HistoricalImageryProviderType.GoogleEarth,
                    VersionId = group.Key.ToString("yyyy-MM-dd"),
                    DisplayDate = group.Key.ToString("yyyy-MM-dd"),
                    Summary = $"Google historical imagery ({group.Count()} tile entries)",
                    HasFullCoverage = false
                })
                .OrderByDescending(item => item.DisplayDate, StringComparer.Ordinal)
                .ToArray();
        }

        public async Task<HistoricalTileDownloadResult> DownloadTileAsync(
            HistoricalTileDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!DateOnly.TryParse(request.Version.DisplayDate, out var desiredDate))
            {
                return new HistoricalTileDownloadResult
                {
                    Message = $"Invalid Google imagery date: {request.Version.DisplayDate}"
                };
            }

            var node = await _client.GetNodeAsync(request.Tile, cancellationToken);
            if (node == null)
            {
                return new HistoricalTileDownloadResult
                {
                    Message = $"No Google imagery metadata found for tile {request.Tile.Path ?? $"{request.Tile.Row}/{request.Tile.Column}"}"
                };
            }

            var resolution = ResolveTileSelection(node.DatedTiles, desiredDate, allowNearestDateFallback: true);
            if (resolution == null)
            {
                return new HistoricalTileDownloadResult
                {
                    Message = $"No Google imagery for {desiredDate:yyyy-MM-dd} at tile {node.Path}"
                };
            }

            try
            {
                var bytes = await _client.DownloadTileAsync(node, resolution.DatedTile, cancellationToken);
                var resolvedDisplayDate = resolution.DatedTile.Date.ToString("yyyy-MM-dd");
                var message = bytes == null || bytes.Length == 0
                    ? $"Failed to download Google tile {node.Path}"
                    : resolution.UsedNearestDateFallback
                        ? $"Google tile fallback: {node.Path} {desiredDate:yyyy-MM-dd} -> {resolvedDisplayDate}"
                        : null;
                return new HistoricalTileDownloadResult
                {
                    ImageBytes = bytes,
                    Message = message,
                    UsedNearestDateFallback = resolution.UsedNearestDateFallback,
                    ResolvedDisplayDate = resolvedDisplayDate
                };
            }
            catch (HttpRequestException ex)
            {
                return new HistoricalTileDownloadResult
                {
                    Message = $"Failed to download Google tile {node.Path}: {ex.Message}"
                };
            }
        }

        internal static GoogleTileSelectionResolution? ResolveTileSelection(
            IReadOnlyList<GoogleDatedTileInfo> datedTiles,
            DateOnly desiredDate,
            bool allowNearestDateFallback)
        {
            ArgumentNullException.ThrowIfNull(datedTiles);

            var exactMatch = datedTiles.FirstOrDefault(tile => tile.Date == desiredDate);
            if (exactMatch != null)
            {
                return new GoogleTileSelectionResolution(exactMatch, UsedNearestDateFallback: false);
            }

            if (!allowNearestDateFallback || datedTiles.Count == 0)
            {
                return null;
            }

            var nearestTile = datedTiles
                .OrderBy(tile => Math.Abs(tile.Date.DayNumber - desiredDate.DayNumber))
                .ThenByDescending(tile => tile.Date <= desiredDate)
                .ThenByDescending(tile => tile.Date.DayNumber)
                .First();

            return new GoogleTileSelectionResolution(nearestTile, UsedNearestDateFallback: true);
        }

        public async Task<HistoricalCoverageProbeResult> ProbeCoverageAsync(
            HistoricalCoverageProbeRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!DateOnly.TryParse(request.Version.DisplayDate, out var desiredDate))
            {
                return new HistoricalCoverageProbeResult
                {
                    CheckedTileCount = request.Tiles.Count,
                    AvailableTileCount = 0
                };
            }

            var availableTileCount = 0;
            foreach (var tile in request.Tiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var node = await _client.GetNodeAsync(tile, cancellationToken);
                if (node?.DatedTiles.Any(tileInfo => tileInfo.Date == desiredDate) == true)
                {
                    availableTileCount++;
                }
            }

            return new HistoricalCoverageProbeResult
            {
                CheckedTileCount = request.Tiles.Count,
                AvailableTileCount = availableTileCount
            };
        }
    }

    internal sealed class WaybackHistoricalImageryProvider : IHistoricalImageryProvider
    {
        private const string WaybackConfigUrl = "https://s3-us-west-2.amazonaws.com/config.maptiles.arcgis.com/waybackconfig.json";
        private const string WaybackTilemapUrlTemplate = "https://wayback.maptiles.arcgis.com/arcgis/rest/services/World_Imagery/MapServer/tilemap/{0}/{1}/{2}/{3}?f=json";
        private const int TilemapConcurrency = 24;
        private const int MetadataConcurrency = 24;
        private const double MaxMercatorLatitude = 85.0511287798066d;
        private readonly HistoricalHttpClient _httpClient = new();
        private readonly string _cacheDirectory;

        private sealed record WaybackMetadataInfo(
            string? AcquisitionDate,
            string? Provider,
            string? SourceName,
            string? Resolution,
            string? Accuracy)
        {
            public string? ChangeKey
                => string.Join(
                    "|",
                    new[] { AcquisitionDate, Provider, SourceName, Resolution, Accuracy }
                        .Select(value => value ?? string.Empty));
        }

        public WaybackHistoricalImageryProvider(string? cacheDirectory = null)
        {
            _cacheDirectory = cacheDirectory ?? CachePathProvider.GetProviderPath(
                CachePathProvider.GetRootPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),
                HistoricalImageryProviderType.Wayback);
        }

        public HistoricalImageryProviderType ProviderType => HistoricalImageryProviderType.Wayback;

        public HistoricalTileScheme TileScheme => HistoricalTileScheme.WebMercator;

        public string SourceSpatialReferenceText => "EPSG:3857";

        public async Task<IReadOnlyList<HistoricalVersionItem>> QueryVersionsAsync(
            HistoricalVersionQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var versions = (await GetCatalogAsync(cancellationToken)).ToArray();
            if (query.IncludeAllVersions)
            {
                return versions;
            }

            await PopulateTilemapInfoAsync(versions, query, cancellationToken);
            var changedVersions = WaybackVersionFilter.FilterVersions(versions, includeAllVersions: false).ToArray();
            if (changedVersions.Length == 0)
            {
                return Array.Empty<HistoricalVersionItem>();
            }

            var metadataLevel = await DetermineMetadataLevelAsync(changedVersions, query, cancellationToken);
            await PopulateAcquisitionDatesAsync(
                changedVersions,
                query.Longitude,
                query.Latitude,
                metadataLevel,
                cancellationToken);

            return changedVersions;
        }

        public async Task<HistoricalTileDownloadResult> DownloadTileAsync(
            HistoricalTileDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            var template = request.Version.TileUrlTemplate;
            if (string.IsNullOrWhiteSpace(template))
            {
                template = (await GetCatalogAsync(cancellationToken))
                    .FirstOrDefault(item => item.VersionId == request.Version.VersionId)
                    ?.TileUrlTemplate;
            }

            if (string.IsNullOrWhiteSpace(template))
            {
                return new HistoricalTileDownloadResult
                {
                    Message = $"Unable to resolve Wayback tile template for version {request.Version.VersionId}"
                };
            }

            var url = ExpandTileUrlTemplate(template, request.Tile);

            try
            {
                var bytes = await _httpClient.GetBytesAsync(
                    url,
                    Path.Combine(_cacheDirectory, "tiles"),
                    cacheKey: url,
                    cacheDuration: request.UseCache ? TimeSpan.FromDays(30) : TimeSpan.Zero,
                    cancellationToken);

                return new HistoricalTileDownloadResult
                {
                    ImageBytes = bytes,
                    Message = bytes.Length == 0 ? $"No Wayback imagery for tile {request.Tile.Row}/{request.Tile.Column}" : null
                };
            }
            catch (HttpRequestException ex)
            {
                return new HistoricalTileDownloadResult
                {
                    Message = $"Failed to download Wayback tile {request.Tile.Row}/{request.Tile.Column}: {ex.Message}"
                };
            }
        }

        public async Task<HistoricalCoverageProbeResult> ProbeCoverageAsync(
            HistoricalCoverageProbeRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Tiles.Count == 0)
            {
                return new HistoricalCoverageProbeResult();
            }

            const int maxConcurrency = 12;
            using var semaphore = new SemaphoreSlim(Math.Min(maxConcurrency, request.Tiles.Count));
            var tasks = request.Tiles.Select(async tile =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await QueryTilemapInfoAsync(request.Version.VersionId, tile, cancellationToken) != null ? 1 : 0;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            return new HistoricalCoverageProbeResult
            {
                CheckedTileCount = request.Tiles.Count,
                AvailableTileCount = results.Sum()
            };
        }

        private async Task<IReadOnlyList<HistoricalVersionItem>> GetCatalogAsync(CancellationToken cancellationToken)
        {
            var json = await _httpClient.GetStringAsync(
                WaybackConfigUrl,
                Path.Combine(_cacheDirectory, "catalog"),
                cacheKey: WaybackConfigUrl,
                cacheDuration: TimeSpan.FromDays(1),
                cancellationToken);

            return WaybackCatalogParser.Parse(json);
        }

        private async Task PopulateAcquisitionDatesAsync(
            IReadOnlyList<HistoricalVersionItem> versions,
            double longitude,
            double latitude,
            int metadataLevel,
            CancellationToken cancellationToken)
        {
            if (versions.Count == 0)
            {
                return;
            }

            using var semaphore = new SemaphoreSlim(Math.Min(MetadataConcurrency, versions.Count));
            var tasks = versions.Select(async version =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var metadata = await QueryMetadataInfoAsync(
                        version.MetadataLayerUrl,
                        longitude,
                        latitude,
                        metadataLevel,
                        cancellationToken);
                    version.AcquisitionDate = metadata?.AcquisitionDate;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private async Task PopulateTilemapInfoAsync(
            IReadOnlyList<HistoricalVersionItem> versions,
            HistoricalVersionQuery query,
            CancellationToken cancellationToken)
        {
            if (versions.Count == 0)
            {
                return;
            }

            var tileAddress = CreateTileAddress(query.Longitude, query.Latitude, query.ZoomLevel);

            using var semaphore = new SemaphoreSlim(Math.Min(TilemapConcurrency, versions.Count));
            var tasks = versions.Select(async version =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var tilemapInfo = await QueryTilemapInfoAsync(version.VersionId, tileAddress, cancellationToken);
                    version.ChangeKey = tilemapInfo?.EffectiveVersionId;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }

        private static string ExpandTileUrlTemplate(string template, HistoricalTileDefinition tile)
            => template
                .Replace("{level}", tile.ZoomLevel.ToString(), StringComparison.Ordinal)
                .Replace("{row}", tile.Row.ToString(), StringComparison.Ordinal)
                .Replace("{col}", tile.Column.ToString(), StringComparison.Ordinal)
                .Replace("{TileMatrix}", tile.ZoomLevel.ToString(), StringComparison.Ordinal)
                .Replace("{TileRow}", tile.Row.ToString(), StringComparison.Ordinal)
                .Replace("{TileCol}", tile.Column.ToString(), StringComparison.Ordinal);

        private static HistoricalTileDefinition CreateTileAddress(double longitude, double latitude, int zoomLevel)
        {
            var clampedLatitude = Math.Clamp(latitude, -MaxMercatorLatitude, MaxMercatorLatitude);
            var mercatorX = HistoricalTileSchema.WebMercatorOriginShift * longitude / 180d;
            var mercatorY = LatitudeToWebMercatorY(clampedLatitude);
            var column = HistoricalTileSchema.GetWebMercatorColumn(mercatorX, zoomLevel);
            var row = HistoricalTileSchema.GetWebMercatorRow(mercatorY, zoomLevel);
            return HistoricalTileSchema.CreateWebMercatorTile(row, column, zoomLevel, 0, 0);
        }

        private static double LatitudeToWebMercatorY(double latitude)
        {
            var radians = latitude * Math.PI / 180d;
            var mercator = Math.Log(Math.Tan(Math.PI / 4d + radians / 2d));
            return HistoricalTileSchema.WebMercatorOriginShift * mercator / Math.PI;
        }

        private static int MapZoomToMetadataLevel(int zoomLevel)
        {
            if (zoomLevel >= 19) return 4;
            if (zoomLevel == 18) return 5;
            if (zoomLevel == 17) return 6;
            if (zoomLevel == 16) return 7;
            if (zoomLevel == 15) return 8;
            if (zoomLevel == 14) return 9;
            if (zoomLevel == 13) return 10;
            if (zoomLevel == 12) return 11;
            return 12;
        }

        private async Task<int> DetermineMetadataLevelAsync(
            IReadOnlyList<HistoricalVersionItem> versions,
            HistoricalVersionQuery query,
            CancellationToken cancellationToken)
        {
            var preferredLevel = MapZoomToMetadataLevel(query.ZoomLevel);
            var metadataLayerUrl = versions.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.MetadataLayerUrl))?.MetadataLayerUrl;
            if (string.IsNullOrWhiteSpace(metadataLayerUrl))
            {
                return preferredLevel;
            }

            foreach (var level in EnumerateCandidateMetadataLevels(preferredLevel))
            {
                var metadata = await QueryMetadataInfoAsync(metadataLayerUrl, query.Longitude, query.Latitude, level, cancellationToken);
                if (metadata != null)
                {
                    return level;
                }
            }

            return preferredLevel;
        }

        private static IEnumerable<int> EnumerateCandidateMetadataLevels(int preferredLevel)
        {
            const int minLevel = 0;
            const int maxLevel = 13;

            yield return preferredLevel;

            for (var offset = 1; offset <= maxLevel; offset++)
            {
                var lower = preferredLevel - offset;
                if (lower >= minLevel)
                {
                    yield return lower;
                }

                var upper = preferredLevel + offset;
                if (upper <= maxLevel)
                {
                    yield return upper;
                }
            }
        }

        private async Task<WaybackTilemapInfo?> QueryTilemapInfoAsync(
            string releaseId,
            HistoricalTileDefinition tile,
            CancellationToken cancellationToken)
        {
            try
            {
                var url = string.Format(
                    CultureInfo.InvariantCulture,
                    WaybackTilemapUrlTemplate,
                    releaseId,
                    tile.ZoomLevel,
                    tile.Row,
                    tile.Column);

                var responseJson = await _httpClient.GetStringAsync(
                    url,
                    Path.Combine(_cacheDirectory, "tilemap"),
                    cacheKey: url,
                    cacheDuration: TimeSpan.FromDays(7),
                    cancellationToken);

                using var document = JsonDocument.Parse(responseJson);
                var root = document.RootElement;

                if (root.TryGetProperty("valid", out var validElement) &&
                    validElement.ValueKind == JsonValueKind.False)
                {
                    return null;
                }

                if (!HasTileData(root))
                {
                    return null;
                }

                var effectiveVersionId = ReadSelectedVersionId(root) ?? releaseId;
                return new WaybackTilemapInfo(effectiveVersionId);
            }
            catch
            {
                return null;
            }
        }

        private async Task<WaybackMetadataInfo?> QueryMetadataInfoAsync(
            string? metadataLayerUrl,
            double longitude,
            double latitude,
            int metadataLevel,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(metadataLayerUrl))
            {
                return null;
            }

            try
            {
                var queryUrl = $"{metadataLayerUrl}/{metadataLevel}/query";
                var geometry = new
                {
                    spatialReference = new { wkid = 4326 },
                    x = longitude,
                    y = latitude
                };

                var queryParams = new Dictionary<string, string>
                {
                    { "f", "json" },
                    { "where", "1=1" },
                    { "outFields", "SRC_DATE2,NICE_DESC,SRC_DESC,SAMP_RES,SRC_ACC" },
                    { "geometry", JsonSerializer.Serialize(geometry) },
                    { "returnGeometry", "false" },
                    { "geometryType", "esriGeometryPoint" },
                    { "spatialRel", "esriSpatialRelIntersects" }
                };

                var responseJson = await _httpClient.PostFormAsync(queryUrl, queryParams, cancellationToken);
                using var document = JsonDocument.Parse(responseJson);
                if (!document.RootElement.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
                {
                    return null;
                }

                var attributes = features[0].GetProperty("attributes");
                return new WaybackMetadataInfo(
                    ReadDate(attributes, "SRC_DATE2"),
                    ReadString(attributes, "NICE_DESC"),
                    ReadString(attributes, "SRC_DESC"),
                    ReadString(attributes, "SAMP_RES"),
                    ReadString(attributes, "SRC_ACC"));
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadDate(JsonElement attributes, string propertyName)
        {
            if (!attributes.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.Number
                ? DateTimeOffset.FromUnixTimeMilliseconds(value.GetInt64()).DateTime.ToString("yyyy-MM-dd")
                : value.GetString();
        }

        private static string? ReadString(JsonElement attributes, string propertyName)
        {
            if (!attributes.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.TryGetDouble(out var number)
                    ? number.ToString("0.###", CultureInfo.InvariantCulture)
                    : value.ToString(),
                _ => value.ToString()
            };
        }

        private static bool HasTileData(JsonElement root)
        {
            if (!root.TryGetProperty("data", out var dataElement) ||
                dataElement.ValueKind != JsonValueKind.Array ||
                dataElement.GetArrayLength() == 0)
            {
                return false;
            }

            var first = dataElement[0];
            return first.ValueKind switch
            {
                JsonValueKind.Number => first.TryGetInt32(out var number) && number > 0,
                JsonValueKind.String => int.TryParse(first.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0,
                _ => false
            };
        }

        private static string? ReadSelectedVersionId(JsonElement root)
        {
            if (!root.TryGetProperty("select", out var selectElement) ||
                selectElement.ValueKind != JsonValueKind.Array ||
                selectElement.GetArrayLength() == 0)
            {
                return null;
            }

            var first = selectElement[0];
            return first.ValueKind switch
            {
                JsonValueKind.Number => first.TryGetInt32(out var number)
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : first.ToString(),
                JsonValueKind.String => first.GetString(),
                _ => null
            };
        }

        private sealed record WaybackTilemapInfo(string EffectiveVersionId);
    }
}
