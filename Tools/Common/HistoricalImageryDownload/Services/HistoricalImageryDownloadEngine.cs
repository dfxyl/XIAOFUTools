#nullable enable

using System;
using System.Collections.Generic;
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

        public async Task<HistoricalDownloadInspectionResult> InspectAsync(
            HistoricalDownloadExecutionRequest executionRequest,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(executionRequest);
            ArgumentNullException.ThrowIfNull(executionRequest.Request);
            ArgumentNullException.ThrowIfNull(executionRequest.ResolvedArea);

            var provider = _providerFactory.GetProvider(executionRequest.Request.Provider);
            var planContext = CreatePlanContext(provider, executionRequest.ResolvedArea, executionRequest.Request.ZoomLevel);
            var sampledTiles = HistoricalCoverageProbeSampler.CreateSample(planContext.Plan.Tiles);
            var coverage = await provider.ProbeCoverageAsync(
                new HistoricalCoverageProbeRequest
                {
                    Version = executionRequest.Request.Version,
                    Tiles = sampledTiles
                },
                cancellationToken);

            var messages = new List<string>(planContext.Messages);
            if (!coverage.HasCoverage)
            {
                messages.Add($"版本 {executionRequest.Request.Version.DisplayDate} 在当前范围和级别下未检测到可用影像，已跳过。");
            }

            return new HistoricalDownloadInspectionResult
            {
                CanDownload = coverage.HasCoverage && !planContext.Evaluation.ShouldBlock,
                ShouldWarn = planContext.Evaluation.ShouldWarn,
                ShouldBlock = planContext.Evaluation.ShouldBlock,
                UseFastClip = planContext.Evaluation.UseFastClip,
                TotalTileCount = planContext.Plan.Tiles.Count,
                CheckedTileCount = coverage.CheckedTileCount,
                AvailableTileCount = coverage.AvailableTileCount,
                RecommendedTileConcurrency = planContext.Evaluation.RecommendedTileConcurrency,
                Messages = messages
            };
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

            var evaluation = HistoricalDownloadPerformanceAdvisor.Evaluate(
                plan.Tiles.Count,
                versionCount: 1,
                preciseClip: area.RequiresPreciseClip && area.AreaGeometry != null);

            var preciseClip = area.RequiresPreciseClip && area.AreaGeometry != null && !evaluation.UseFastClip
                ? GeometryEngine.Instance.Project(area.AreaGeometry, sourceSpatialReference) as Polygon
                : null;

            return new HistoricalPlanContext(
                plan,
                preciseClip,
                provider.SourceSpatialReferenceText,
                evaluation,
                evaluation.Messages.ToArray());
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
            var outputSessions = new Dictionary<string, HistoricalOutputSession>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var processedTileCount = 0;
                var fallbackTileCount = 0;
                var fallbackDates = new HashSet<string>(StringComparer.Ordinal);
                var tileDownloadBatchSize = planContext.Evaluation.RecommendedTileConcurrency;
                for (var batchStart = 0; batchStart < planContext.Plan.Tiles.Count; batchStart += tileDownloadBatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batchTiles = planContext.Plan.Tiles
                        .Skip(batchStart)
                        .Take(tileDownloadBatchSize)
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

                        if (batchResult.Result.UsedNearestDateFallback)
                        {
                            fallbackTileCount++;
                            if (!string.IsNullOrWhiteSpace(batchResult.Result.ResolvedDisplayDate))
                            {
                                fallbackDates.Add(batchResult.Result.ResolvedDisplayDate);
                            }
                        }

                        using var sourceTileDataset = OpenDataset(batchResult.Result.ImageBytes!);
                        var outputFilePath = ResolveOutputFilePath(request, batchResult.Result);
                        var outputSession = GetOrCreateOutputSession(outputSessions, outputFilePath, planContext);
                        var clipAlphaMask = planContext.PreciseClip == null
                            ? null
                            : CreateClipAlphaMask(
                                batchResult.Tile,
                                sourceTileDataset.RasterXSize,
                                sourceTileDataset.RasterYSize,
                                planContext.PreciseClip);
                        WriteTile(outputSession.Dataset, sourceTileDataset, batchResult.Tile, clipAlphaMask);

                        result.DownloadedTileCount++;
                        if (!string.IsNullOrWhiteSpace(batchResult.Result.Message))
                        {
                            result.Messages.Add(batchResult.Result.Message);
                        }

                        progress?.Report(processedTileCount / (planContext.Plan.Tiles.Count + 1d));
                    }
                }

                foreach (var outputSession in outputSessions.Values.OrderBy(item => item.OutputFilePath, StringComparer.OrdinalIgnoreCase))
                {
                    outputSession.Dataset.FlushCache();
                    ExportDataset(outputSession.TempFilePath, outputSession.OutputFilePath, planContext.SourceSpatialReferenceText, executionRequest.TargetSpatialReferenceText);
                    result.OutputFilePaths.Add(outputSession.OutputFilePath);
                }

                progress?.Report(1);
                if (result.OutputFilePaths.Count > 0)
                {
                    result.OutputFilePath = result.OutputFilePaths[0];
                }

                result.HasPartialCoverage |= result.DownloadedTileCount < result.TotalTileCount;
                if (fallbackTileCount > 0)
                {
                    var fallbackDateSummary = fallbackDates.Count == 0
                        ? "unknown"
                        : string.Join(", ", fallbackDates.OrderBy(value => value, StringComparer.Ordinal).Take(5));
                    result.Messages.Add(
                        $"Google nearest-date fallback used on {fallbackTileCount} tiles for requested date {request.Version.DisplayDate}; actual dates: {fallbackDateSummary}");
                }

                if (request.Provider == HistoricalImageryProviderType.GoogleEarth &&
                    request.GoogleNearestDateFallbackMode == GoogleNearestDateFallbackMode.SeparateOutputs &&
                    result.OutputFilePaths.Count > 1)
                {
                    result.Messages.Add(
                        $"Google nearest-date fallback exported {result.OutputFilePaths.Count} separate files for requested date {request.Version.DisplayDate}.");
                }

                foreach (var message in planContext.Messages)
                {
                    result.Messages.Add(message);
                }
                return result;
            }
            finally
            {
                foreach (var outputSession in outputSessions.Values)
                {
                    outputSession.Dispose();
                    if (File.Exists(outputSession.TempFilePath))
                    {
                        File.Delete(outputSession.TempFilePath);
                    }
                }
            }
        }

        private static Dataset CreateTempDataset(string tempFile, HistoricalPlanContext planContext)
        {
            using var driver = Gdal.GetDriverByName("GTiff");
            var createOptions = new[]
            {
                "TILED=TRUE",
                "BIGTIFF=IF_SAFER",
                $"NUM_THREADS={Math.Max(1, Environment.ProcessorCount / 2)}"
            };
            var dataset = driver.Create(
                tempFile,
                planContext.Plan.PixelWidth,
                planContext.Plan.PixelHeight,
                HistoricalRasterCompositionOptions.OutputBandCount,
                DataType.GDT_Byte,
                createOptions);
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
            InitializeOutputBands(dataset);
            return dataset;
        }

        private static void WriteTile(
            Dataset destinationDataset,
            Dataset tileDataset,
            HistoricalTileDefinition tile,
            byte[]? clipAlphaMask)
        {
            var colorBandCount = Math.Min(3, tileDataset.RasterCount);
            if (colorBandCount <= 0)
            {
                return;
            }

            var width = Math.Min(tileDataset.RasterXSize, destinationDataset.RasterXSize - tile.PixelOffsetX);
            var height = Math.Min(tileDataset.RasterYSize, destinationDataset.RasterYSize - tile.PixelOffsetY);
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var colorBandMap = Enumerable.Range(1, colorBandCount).ToArray();
            var colorBuffer = GC.AllocateUninitializedArray<byte>(width * height * colorBandCount);
            tileDataset.ReadRaster(0, 0, width, height, colorBuffer, width, height, colorBandCount, colorBandMap, colorBandCount, width * colorBandCount, 1);
            destinationDataset.WriteRaster(tile.PixelOffsetX, tile.PixelOffsetY, width, height, colorBuffer, width, height, colorBandCount, colorBandMap, colorBandCount, width * colorBandCount, 1);

            var alphaBuffer = CreateAlphaBuffer(tileDataset, width, height, clipAlphaMask);
            destinationDataset.WriteRaster(tile.PixelOffsetX, tile.PixelOffsetY, width, height, alphaBuffer, width, height, 1, [4], 1, width, 1);
        }

        private static byte[] CreateClipAlphaMask(HistoricalTileDefinition tile, int width, int height, Polygon preciseClip)
        {
            var alphaMask = GC.AllocateUninitializedArray<byte>(width * height);

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
                        alphaMask[y * width + x] = 255;
                        continue;
                    }
                }
            }

            return alphaMask;
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
            return new GDALWarpAppOptions(
                HistoricalRasterExportOptions.CreateWarpParameters(sourceSpatialReferenceText, targetSpatialReferenceText));
        }

        private static string[] CreateCopyOptions()
            => HistoricalRasterExportOptions.CreateCopyOptions();

        private static void InitializeOutputBands(Dataset dataset)
        {
            if (dataset.RasterCount < HistoricalRasterCompositionOptions.OutputBandCount)
            {
                return;
            }

            for (var bandIndex = 1; bandIndex <= dataset.RasterCount; bandIndex++)
            {
                using var band = dataset.GetRasterBand(bandIndex);
                band?.Fill(0, 0);
            }

            dataset.GetRasterBand(1)?.SetColorInterpretation(OSGeo.GDAL.ColorInterp.GCI_RedBand);
            dataset.GetRasterBand(2)?.SetColorInterpretation(OSGeo.GDAL.ColorInterp.GCI_GreenBand);
            dataset.GetRasterBand(3)?.SetColorInterpretation(OSGeo.GDAL.ColorInterp.GCI_BlueBand);
            dataset.GetRasterBand(4)?.SetColorInterpretation(OSGeo.GDAL.ColorInterp.GCI_AlphaBand);
            dataset.FlushCache();
        }

        private static byte[] CreateAlphaBuffer(Dataset tileDataset, int width, int height, byte[]? clipAlphaMask)
        {
            byte[] alphaBuffer;

            if (tileDataset.RasterCount >= 4)
            {
                alphaBuffer = GC.AllocateUninitializedArray<byte>(width * height);
                tileDataset.ReadRaster(0, 0, width, height, alphaBuffer, width, height, 1, [4], 1, width, 1);
            }
            else
            {
                alphaBuffer = new byte[width * height];
                Array.Fill(alphaBuffer, (byte)255);
            }

            if (clipAlphaMask == null)
            {
                return alphaBuffer;
            }

            for (var index = 0; index < alphaBuffer.Length; index++)
            {
                alphaBuffer[index] = (byte)Math.Min(alphaBuffer[index], clipAlphaMask[index]);
            }

            return alphaBuffer;
        }

        private static string CreateTempRasterCachePath()
            => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tif");

        private static string ResolveOutputFilePath(HistoricalDownloadRequest request, HistoricalTileDownloadResult tileResult)
        {
            if (request.Provider != HistoricalImageryProviderType.GoogleEarth ||
                request.GoogleNearestDateFallbackMode != GoogleNearestDateFallbackMode.SeparateOutputs)
            {
                return request.OutputFilePath;
            }

            var resolvedDate = string.IsNullOrWhiteSpace(tileResult.ResolvedDisplayDate)
                ? request.Version.DisplayDate
                : tileResult.ResolvedDisplayDate!;
            return HistoricalOutputPathBuilder.BuildForResolvedDate(request.OutputFilePath, resolvedDate);
        }

        private static HistoricalOutputSession GetOrCreateOutputSession(
            IDictionary<string, HistoricalOutputSession> outputSessions,
            string outputFilePath,
            HistoricalPlanContext planContext)
        {
            if (outputSessions.TryGetValue(outputFilePath, out var existingSession))
            {
                return existingSession;
            }

            var tempFilePath = CreateTempRasterCachePath();
            var dataset = CreateTempDataset(tempFilePath, planContext);
            var createdSession = new HistoricalOutputSession(outputFilePath, tempFilePath, dataset);
            outputSessions[outputFilePath] = createdSession;
            return createdSession;
        }

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
                    UseCache = request.UseCache,
                    GoogleNearestDateFallbackMode = request.GoogleNearestDateFallbackMode
                },
                cancellationToken);

            return new HistoricalDownloadedTile(tile, result);
        }

        private sealed record HistoricalOutputSession(string OutputFilePath, string TempFilePath, Dataset Dataset) : IDisposable
        {
            public void Dispose()
            {
                Dataset.Dispose();
            }
        }

        private sealed record HistoricalDownloadedTile(HistoricalTileDefinition Tile, HistoricalTileDownloadResult Result);
        private sealed record HistoricalPlanContext(
            HistoricalTilePlan Plan,
            Polygon? PreciseClip,
            string SourceSpatialReferenceText,
            HistoricalDownloadPerformanceEvaluation Evaluation,
            IReadOnlyList<string> Messages);
    }
}
