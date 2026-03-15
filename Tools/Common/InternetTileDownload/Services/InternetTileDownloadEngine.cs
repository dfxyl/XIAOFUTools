#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using OSGeo.GDAL;
using XIAOFUTools.Tools.HistoricalImageryDownload.Infrastructure;
using XIAOFUTools.Tools.InternetTileDownload.Core;
using XIAOFUTools.Tools.InternetTileDownload.Infrastructure;
using HistoricalPerformanceAdvisor = XIAOFUTools.Tools.HistoricalImageryDownload.Services.HistoricalDownloadPerformanceAdvisor;
using HistoricalPerformanceEvaluation = XIAOFUTools.Tools.HistoricalImageryDownload.Services.HistoricalDownloadPerformanceEvaluation;
using HistoricalRasterComposition = XIAOFUTools.Tools.HistoricalImageryDownload.Services.HistoricalRasterCompositionOptions;
using HistoricalRasterExport = XIAOFUTools.Tools.HistoricalImageryDownload.Services.HistoricalRasterExportOptions;
using GdalSpatialReference = OSGeo.OSR.SpatialReference;

namespace XIAOFUTools.Tools.InternetTileDownload.Services
{
    internal sealed class InternetTileDownloadEngine
    {
        private readonly IInternetTileHttpClient _httpClient;

        static InternetTileDownloadEngine()
        {
            HistoricalGdalEnvironment.Register();
        }

        public InternetTileDownloadEngine(IInternetTileHttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new InternetTileHttpClient();
        }

        public Task<InternetTileDownloadInspectionResult> InspectAsync(
            InternetTileDownloadExecutionRequest executionRequest,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var planContext = CreatePlanContext(executionRequest.Request, executionRequest.ResolvedArea);
            return Task.FromResult(new InternetTileDownloadInspectionResult
            {
                CanDownload = !planContext.Evaluation.ShouldBlock,
                ShouldWarn = planContext.Evaluation.ShouldWarn,
                ShouldBlock = planContext.Evaluation.ShouldBlock,
                UseFastClip = planContext.Evaluation.UseFastClip,
                TotalTileCount = planContext.Plan.Tiles.Count,
                RecommendedTileConcurrency = planContext.Evaluation.RecommendedTileConcurrency,
                Messages = planContext.Evaluation.Messages
            });
        }

        public async Task<InternetTileDownloadResult> DownloadAsync(
            InternetTileDownloadExecutionRequest executionRequest,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(executionRequest);
            var request = executionRequest.Request ?? throw new InvalidOperationException("缺少下载请求。");
            var area = executionRequest.ResolvedArea ?? throw new InvalidOperationException("缺少下载范围。");
            var planContext = CreatePlanContext(request, area);

            var result = new InternetTileDownloadResult
            {
                OutputFilePath = request.OutputFilePath,
                TotalTileCount = planContext.Plan.Tiles.Count
            };

            Directory.CreateDirectory(Path.GetDirectoryName(request.OutputFilePath)!);
            var tempFilePath = CreateTempRasterCachePath();
            Dataset? dataset = null;

            try
            {
                dataset = CreateTempDataset(tempFilePath, planContext);
                var processedTileCount = 0;
                var batchSize = Math.Max(1, planContext.Evaluation.RecommendedTileConcurrency);
                for (var batchStart = 0; batchStart < planContext.Plan.Tiles.Count; batchStart += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batchTiles = planContext.Plan.Tiles
                        .Skip(batchStart)
                        .Take(batchSize)
                        .ToArray();
                    var batchResults = await Task.WhenAll(batchTiles.Select(tile => DownloadTileAsync(request, tile, cancellationToken)));

                    foreach (var batchResult in batchResults)
                    {
                        processedTileCount++;

                        if (!batchResult.HasData)
                        {
                            result.HasPartialCoverage = true;
                            if (!string.IsNullOrWhiteSpace(batchResult.Message))
                            {
                                result.Messages.Add(batchResult.Message);
                            }

                            progress?.Report(processedTileCount / (planContext.Plan.Tiles.Count + 1d));
                            continue;
                        }

                        using var sourceTileDataset = OpenDataset(batchResult.ImageBytes!);
                        var clipAlphaMask = planContext.PreciseClip == null
                            ? null
                            : CreateClipAlphaMask(
                                batchResult.Tile,
                                sourceTileDataset.RasterXSize,
                                sourceTileDataset.RasterYSize,
                                planContext.PreciseClip);
                        WriteTile(dataset, sourceTileDataset, batchResult.Tile, clipAlphaMask);

                        result.DownloadedTileCount++;
                        if (!string.IsNullOrWhiteSpace(batchResult.Message))
                        {
                            result.Messages.Add(batchResult.Message);
                        }

                        progress?.Report(processedTileCount / (planContext.Plan.Tiles.Count + 1d));
                    }
                }

                InternetTileDownloadCompletionGuard.EnsureHasAnyTile(result.DownloadedTileCount, result.TotalTileCount);
                dataset.FlushCache();
                ExportDataset(tempFilePath, request.OutputFilePath, planContext.SourceSpatialReferenceText, executionRequest.TargetSpatialReferenceText);
                progress?.Report(1);
                result.HasPartialCoverage |= result.DownloadedTileCount < result.TotalTileCount;

                foreach (var message in planContext.Evaluation.Messages)
                {
                    result.Messages.Add(message);
                }

                return result;
            }
            finally
            {
                dataset?.FlushCache();
                dataset?.Dispose();
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        private static InternetTilePlanContext CreatePlanContext(InternetTileDownloadRequest request, InternetTileResolvedArea area)
        {
            var extent = area.Extent ?? throw new InvalidOperationException("缺少下载范围。");
            var sourceSpatialReference = CreateSpatialReference(request.ServiceDefinition.SourceSpatialReferenceText);
            var projectedExtent = GeometryEngine.Instance.Project(extent, sourceSpatialReference) as Envelope
                ?? throw new InvalidOperationException($"范围投影到 {request.ServiceDefinition.SourceSpatialReferenceText} 失败。");
            var plan = InternetTilePlanner.Plan(
                request.ServiceDefinition,
                request.LevelId,
                projectedExtent.XMin,
                projectedExtent.YMin,
                projectedExtent.XMax,
                projectedExtent.YMax);

            var evaluation = HistoricalPerformanceAdvisor.Evaluate(
                plan.Tiles.Count,
                versionCount: 1,
                preciseClip: area.RequiresPreciseClip && area.AreaGeometry != null);

            var preciseClip = area.RequiresPreciseClip && area.AreaGeometry != null && !evaluation.UseFastClip
                ? GeometryEngine.Instance.Project(area.AreaGeometry, sourceSpatialReference) as Polygon
                : null;

            return new InternetTilePlanContext(plan, preciseClip, request.ServiceDefinition.SourceSpatialReferenceText, evaluation);
        }

        private static ArcGIS.Core.Geometry.SpatialReference CreateSpatialReference(string spatialReferenceText)
        {
            if (string.IsNullOrWhiteSpace(spatialReferenceText))
            {
                throw new InvalidOperationException("切片服务缺少源坐标系定义。");
            }

            if (spatialReferenceText.Contains("900913", StringComparison.OrdinalIgnoreCase) ||
                spatialReferenceText.Contains("102100", StringComparison.OrdinalIgnoreCase) ||
                spatialReferenceText.Contains("102113", StringComparison.OrdinalIgnoreCase))
            {
                spatialReferenceText = "EPSG:3857";
            }

            if (spatialReferenceText.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(spatialReferenceText[5..], out var wkid))
            {
                return SpatialReferenceBuilder.CreateSpatialReference(wkid);
            }

            return SpatialReferenceBuilder.CreateSpatialReference(spatialReferenceText);
        }

        private async Task<InternetTileDownloadedTile> DownloadTileAsync(
            InternetTileDownloadRequest request,
            InternetTileDefinition tile,
            CancellationToken cancellationToken)
        {
            try
            {
                var url = InternetTileRequestExpander.Expand(request.ServiceDefinition, request.LevelId, tile.Row, tile.Column);
                var bytes = await _httpClient.GetBytesAsync(url, cancellationToken);
                return new InternetTileDownloadedTile(tile, bytes, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new InternetTileDownloadedTile(tile, null, $"瓦片下载失败 {request.LevelId}/{tile.Row}/{tile.Column}: {ex.Message}");
            }
        }

        private static Dataset CreateTempDataset(string tempFilePath, InternetTilePlanContext planContext)
        {
            using var driver = Gdal.GetDriverByName("GTiff");
            var dataset = driver.Create(
                tempFilePath,
                planContext.Plan.PixelWidth,
                planContext.Plan.PixelHeight,
                HistoricalRasterComposition.OutputBandCount,
                DataType.GDT_Byte,
                [
                    "TILED=TRUE",
                    "BIGTIFF=IF_SAFER",
                    $"NUM_THREADS={Math.Max(1, Environment.ProcessorCount / 2)}"
                ]);

            using var spatialReference = new GdalSpatialReference(string.Empty);
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

        private static void WriteTile(Dataset destinationDataset, Dataset tileDataset, InternetTileDefinition tile, byte[]? clipAlphaMask)
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

        private static byte[] CreateClipAlphaMask(InternetTileDefinition tile, int width, int height, Polygon preciseClip)
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
                    ?? throw new InvalidOperationException("无法打开切片图像数据。");
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
                ?? throw new InvalidOperationException("无法打开临时栅格数据。");

            var normalizedTarget = string.IsNullOrWhiteSpace(targetSpatialReferenceText)
                ? sourceSpatialReferenceText
                : targetSpatialReferenceText;

            if (!string.Equals(normalizedTarget, sourceSpatialReferenceText, StringComparison.OrdinalIgnoreCase))
            {
                using var options = new GDALWarpAppOptions(HistoricalRasterExport.CreateWarpParameters(sourceSpatialReferenceText, normalizedTarget));
                using var _ = Gdal.Warp(outputFilePath, [sourceDataset], options, null, null)
                    ?? throw new InvalidOperationException("输出投影转换后的 GeoTIFF 失败。");
                return;
            }

            using var driver = Gdal.GetDriverByName("GTiff");
            using var __ = driver.CreateCopy(outputFilePath, sourceDataset, 1, HistoricalRasterExport.CreateCopyOptions(), null, null)
                ?? throw new InvalidOperationException("输出 GeoTIFF 失败。");
        }

        private static void InitializeOutputBands(Dataset dataset)
        {
            if (dataset.RasterCount < HistoricalRasterComposition.OutputBandCount)
            {
                return;
            }

            for (var bandIndex = 1; bandIndex <= dataset.RasterCount; bandIndex++)
            {
                using var band = dataset.GetRasterBand(bandIndex);
                band?.Fill(0, 0);
            }

            dataset.GetRasterBand(1)?.SetColorInterpretation(ColorInterp.GCI_RedBand);
            dataset.GetRasterBand(2)?.SetColorInterpretation(ColorInterp.GCI_GreenBand);
            dataset.GetRasterBand(3)?.SetColorInterpretation(ColorInterp.GCI_BlueBand);
            dataset.GetRasterBand(4)?.SetColorInterpretation(ColorInterp.GCI_AlphaBand);
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

        private sealed record InternetTileDownloadedTile(InternetTileDefinition Tile, byte[]? ImageBytes, string? Message)
        {
            public bool HasData => ImageBytes != null && ImageBytes.Length > 0;
        }

        private sealed record InternetTilePlanContext(
            InternetTilePlan Plan,
            Polygon? PreciseClip,
            string SourceSpatialReferenceText,
            HistoricalPerformanceEvaluation Evaluation);
    }
}
