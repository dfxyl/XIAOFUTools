#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using OSGeo.GDAL;
using OgrSpatialReference = OSGeo.OSR.SpatialReference;
using XIAOFUTools.Tools.HistoricalImageryDownload.Core;
using XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    internal sealed class HistoricalImageryDownloadEngine
    {
        private static readonly int TileDownloadBatchSize = Math.Clamp(Environment.ProcessorCount, 4, 12);
        private readonly HistoricalImageryProviderFactory _providerFactory;

        static HistoricalImageryDownloadEngine()
        {
            HistoricalGdalEnvironment.Register();
        }

        public HistoricalImageryDownloadEngine(HistoricalImageryProviderFactory providerFactory)
        {
            _providerFactory = providerFactory;
        }

        public async Task<HistoricalDownloadResult> DownloadAsync(
            HistoricalDownloadExecutionRequest executionRequest,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(executionRequest);
            ArgumentNullException.ThrowIfNull(executionRequest.Request);
            ArgumentNullException.ThrowIfNull(executionRequest.ResolvedArea);

            var provider = _providerFactory.GetProvider(executionRequest.Request.Provider);
            var planContext = CreatePlanContext(provider, executionRequest.ResolvedArea, executionRequest.Request.ZoomLevel);

            return await ComposeAsync(
                provider,
                executionRequest,
                planContext,
                progress,
                cancellationToken);
        }

        private static HistoricalPlanContext CreatePlanContext(
            IHistoricalImageryProvider provider,
            HistoricalResolvedArea area,
            int zoomLevel)
        {
            var extent = area.Extent ?? throw new InvalidOperationException("缺少下载范围。");
            var sourceSpatialReference = provider.TileScheme == HistoricalTileScheme.WebMercator
                ? SpatialReferences.WebMercator
                : SpatialReferences.WGS84;

            var projectedExtent = GeometryEngine.Instance.Project(extent, sourceSpatialReference) as Envelope
                ?? throw new InvalidOperationException($"范围投影到 {provider.SourceSpatialReferenceText} 失败。");

            var plan = provider.TileScheme == HistoricalTileScheme.WebMercator
                ? HistoricalTilePlanner.PlanWebMercator(projectedExtent.XMin, projectedExtent.YMin, projectedExtent.XMax, projectedExtent.YMax, zoomLevel)
                : HistoricalTilePlanner.PlanGoogle(projectedExtent.XMin, projectedExtent.YMin, projectedExtent.XMax, projectedExtent.YMax, zoomLevel);

            var preciseClip = area.RequiresPreciseClip && area.AreaGeometry != null
                ? GeometryEngine.Instance.Project(area.AreaGeometry, sourceSpatialReference) as Polygon
                : null;

            return new HistoricalPlanContext(plan, preciseClip, provider.SourceSpatialReferenceText);
        }

        private static async Task<HistoricalDownloadResult> ComposeAsync(
            IHistoricalImageryProvider provider,
            HistoricalDownloadExecutionRequest executionRequest,
            HistoricalPlanContext planContext,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            var request = executionRequest.Request;
            var result = new HistoricalDownloadResult
            {
                OutputFilePath = request.OutputFilePath,
                TotalTileCount = planContext.Plan.Tiles.Count
            };

            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputFilePath)!);
            var tempFile = CreateTempRasterCachePath();

            try
            {
                using var tempDataset = CreateTempDataset(tempFile, planContext);

                var processedTileCount = 0;
                for (var batchStart = 0; batchStart < planContext.Plan.Tiles.Count; batchStart += TileDownloadBatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batchTiles = planContext.Plan.Tiles
                        .Skip(batchStart)
                        .Take(TileDownloadBatchSize)
                        .ToArray();

                    var batchResults = await Task.WhenAll(batchTiles.Select(tile => DownloadTileAsync(provider, request, tile, cancellationToken)));

                    foreach (var batchResult in batchResults)
                    {
                        processedTileCount++;

                        if (!batchResult.Result.HasData)
                        {
                            result.HasPartialCoverage = true;
                            if (!string.IsNullOrWhiteSpace(batchResult.Result.Message))
                            {
                                result.Messages.Add(batchResult.Result.Message);
                            }

                            progress?.Report(processedTileCount / (planContext.Plan.Tiles.Count + 1d));
                            continue;
                        }

                        using var sourceTileDataset = OpenDataset(batchResult.Result.ImageBytes!);
                        using var tileDataset = planContext.PreciseClip == null
                            ? null
                            : ClipTile(sourceTileDataset, batchResult.Tile, planContext.PreciseClip);
                        WriteTile(tempDataset, tileDataset ?? sourceTileDataset, batchResult.Tile);

                        result.DownloadedTileCount++;
                        if (!string.IsNullOrWhiteSpace(batchResult.Result.Message))
                        {
                            result.Messages.Add(batchResult.Result.Message);
                        }

                        progress?.Report(processedTileCount / (planContext.Plan.Tiles.Count + 1d));
                    }
                }

                tempDataset.FlushCache();
                ExportDataset(tempFile, request.OutputFilePath, planContext.SourceSpatialReferenceText, executionRequest.TargetSpatialReferenceText);
                ApplyNoData(request.OutputFilePath);

                progress?.Report(1);
                result.HasPartialCoverage |= result.DownloadedTileCount < result.TotalTileCount;
                return result;
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static Dataset CreateTempDataset(string tempFile, HistoricalPlanContext planContext)
        {
            using var driver = Gdal.GetDriverByName("GTiff");
            var dataset = driver.Create(tempFile, planContext.Plan.PixelWidth, planContext.Plan.PixelHeight, 3, DataType.GDT_Byte, null);
            using var spatialReference = new OgrSpatialReference(string.Empty);
            spatialReference.SetFromUserInput(planContext.SourceSpatialReferenceText);
            dataset.SetSpatialRef(spatialReference);
            dataset.SetGeoTransform(
            [
                planContext.Plan.OriginX,
                planContext.Plan.PixelSizeX,
                0,
                planContext.Plan.OriginY,
                0,
                planContext.Plan.PixelSizeY
            ]);
            return dataset;
        }

        private static void WriteTile(Dataset destinationDataset, Dataset tileDataset, HistoricalTileDefinition tile)
        {
            var bandCount = Math.Min(destinationDataset.RasterCount, tileDataset.RasterCount);
            if (bandCount <= 0)
            {
                return;
            }

            var width = Math.Min(tileDataset.RasterXSize, destinationDataset.RasterXSize - tile.PixelOffsetX);
            var height = Math.Min(tileDataset.RasterYSize, destinationDataset.RasterYSize - tile.PixelOffsetY);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var bandMap = Enumerable.Range(1, bandCount).ToArray();
            var buffer = GC.AllocateUninitializedArray<byte>(width * height * bandCount);
            tileDataset.ReadRaster(0, 0, width, height, buffer, width, height, bandCount, bandMap, bandCount, width * bandCount, 1);
            destinationDataset.WriteRaster(tile.PixelOffsetX, tile.PixelOffsetY, width, height, buffer, width, height, bandCount, bandMap, bandCount, width * bandCount, 1);
        }

        private static Dataset ClipTile(Dataset sourceDataset, HistoricalTileDefinition tile, Polygon preciseClip)
        {
            var width = sourceDataset.RasterXSize;
            var height = sourceDataset.RasterYSize;
            var bandCount = sourceDataset.RasterCount;
            var bandMap = Enumerable.Range(1, bandCount).ToArray();
            var buffer = GC.AllocateUninitializedArray<byte>(width * height * bandCount);

            sourceDataset.ReadRaster(0, 0, width, height, buffer, width, height, bandCount, bandMap, bandCount, width * bandCount, 1);

            var pixelWidth = (tile.MaxX - tile.MinX) / width;
            var pixelHeight = (tile.MaxY - tile.MinY) / height;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var centerX = tile.MinX + (x + 0.5) * pixelWidth;
                    var centerY = tile.MaxY - (y + 0.5) * pixelHeight;
                    var point = MapPointBuilderEx.CreateMapPoint(centerX, centerY, preciseClip.SpatialReference);
                    if (GeometryEngine.Instance.Contains(preciseClip, point))
                    {
                        continue;
                    }

                    var start = (y * width + x) * bandCount;
                    for (var bandIndex = 0; bandIndex < bandCount; bandIndex++)
                    {
                        buffer[start + bandIndex] = 0;
                    }
                }
            }

            using var driver = Gdal.GetDriverByName("MEM");
            var dataset = driver.Create(string.Empty, width, height, bandCount, DataType.GDT_Byte, null);
            dataset.WriteRaster(0, 0, width, height, buffer, width, height, bandCount, bandMap, bandCount, width * bandCount, 1);
            return dataset;
        }

        private static Dataset OpenDataset(byte[] imageBytes)
        {
            var memFile = $"/vsimem/{Guid.NewGuid():N}.img";
            Gdal.FileFromMemBuffer(memFile, imageBytes);

            try
            {
                return Gdal.Open(memFile, Access.GA_ReadOnly)
                    ?? throw new InvalidOperationException("Unable to open raster tile bytes.");
            }
            finally
            {
                Gdal.Unlink(memFile);
            }
        }

        private static void ExportDataset(
            string tempFile,
            string outputFilePath,
            string sourceSpatialReferenceText,
            string? targetSpatialReferenceText)
        {
            using var sourceDataset = Gdal.Open(tempFile, Access.GA_ReadOnly)
                ?? throw new InvalidOperationException("Unable to open temporary raster dataset.");

            var normalizedTarget = string.IsNullOrWhiteSpace(targetSpatialReferenceText)
                ? sourceSpatialReferenceText
                : targetSpatialReferenceText;

            if (!string.Equals(normalizedTarget, sourceSpatialReferenceText, StringComparison.OrdinalIgnoreCase))
            {
                using var options = CreateWarpOptions(sourceSpatialReferenceText, normalizedTarget);
                using var _ = Gdal.Warp(outputFilePath, new[] { sourceDataset }, options, null, null)
                    ?? throw new InvalidOperationException("Unable to export warped GeoTIFF.");
                return;
            }

            using var driver = Gdal.GetDriverByName("GTiff");
            using var __ = driver.CreateCopy(outputFilePath, sourceDataset, 1, CreateCopyOptions(), null, null)
                ?? throw new InvalidOperationException("Unable to export GeoTIFF.");
        }

        private static GDALWarpAppOptions CreateWarpOptions(string sourceSpatialReferenceText, string targetSpatialReferenceText)
        {
            string[] parameters =
            [
                "-multi",
                "-wo", $"NUM_THREADS={Math.Max(1, Environment.ProcessorCount / 2)}",
                "-of", "GTiff",
                "-ot", "Byte",
                "-wo", "OPTIMIZE_SIZE=TRUE",
                "-co", "COMPRESS=JPEG",
                "-co", "PHOTOMETRIC=YCBCR",
                "-co", "TILED=TRUE",
                "-r", "bilinear",
                "-s_srs", sourceSpatialReferenceText,
                "-t_srs", targetSpatialReferenceText
            ];

            return new GDALWarpAppOptions(parameters);
        }

        private static string[] CreateCopyOptions()
            =>
            [
                "COMPRESS=JPEG",
                "PHOTOMETRIC=YCBCR",
                "TILED=TRUE",
                $"NUM_THREADS={Math.Max(1, Environment.ProcessorCount / 2)}"
            ];

        private static void ApplyNoData(string outputFilePath)
        {
            using var dataset = Gdal.Open(outputFilePath, Access.GA_Update);
            if (dataset == null)
            {
                return;
            }

            for (var bandIndex = 1; bandIndex <= dataset.RasterCount; bandIndex++)
            {
                using var band = dataset.GetRasterBand(bandIndex);
                band?.SetNoDataValue(0);
            }

            dataset.FlushCache();
        }

        private static string CreateTempRasterCachePath()
            => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tif");

        private static async Task<HistoricalDownloadedTile> DownloadTileAsync(
            IHistoricalImageryProvider provider,
            HistoricalDownloadRequest request,
            HistoricalTileDefinition tile,
            CancellationToken cancellationToken)
        {
            var result = await provider.DownloadTileAsync(
                new HistoricalTileDownloadRequest
                {
                    Version = request.Version,
                    Tile = tile,
                    UseCache = request.UseCache
                },
                cancellationToken);

            return new HistoricalDownloadedTile(tile, result);
        }

        private sealed record HistoricalDownloadedTile(HistoricalTileDefinition Tile, HistoricalTileDownloadResult Result);
        private sealed record HistoricalPlanContext(HistoricalTilePlan Plan, Polygon? PreciseClip, string SourceSpatialReferenceText);
    }
}
