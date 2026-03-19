#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using XIAOFUTools.Tools.Common.RasterExport;
using XIAOFUTools.Tools.HistoricalImageryDownload.Core;

namespace XIAOFUTools.Tools.HistoricalImageryDownload.Services
{
    internal sealed class HistoricalImageryDownloadEngine
    {
        private readonly HistoricalImageryProviderFactory _providerFactory;

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
            var outputSessions = new Dictionary<string, ArcGisRasterExportPipeline>(StringComparer.OrdinalIgnoreCase);

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

                        var outputFilePath = ResolveOutputFilePath(request, batchResult.Result);
                        var outputSession = GetOrCreateOutputSession(outputSessions, outputFilePath);
                        outputSession.AddTile(
                            request.ZoomLevel.ToString(),
                            batchResult.Tile.Row,
                            batchResult.Tile.Column,
                            batchResult.Tile.MinX,
                            batchResult.Tile.MinY,
                            batchResult.Tile.MaxX,
                            batchResult.Tile.MaxY,
                            batchResult.Result.ImageBytes!,
                            planContext.PreciseClip);

                        result.DownloadedTileCount++;
                        if (!string.IsNullOrWhiteSpace(batchResult.Result.Message))
                        {
                            result.Messages.Add(batchResult.Result.Message);
                        }

                        progress?.Report(processedTileCount / (planContext.Plan.Tiles.Count + 1d));
                    }
                }

                foreach (var outputSession in outputSessions.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                {
                    await outputSession.Value.ExportAsync(
                        planContext.SourceSpatialReferenceText,
                        executionRequest.TargetSpatialReferenceText,
                        cancellationToken);
                    result.OutputFilePaths.Add(outputSession.Key);
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
                }
            }
        }

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

        private static ArcGisRasterExportPipeline GetOrCreateOutputSession(
            IDictionary<string, ArcGisRasterExportPipeline> outputSessions,
            string outputFilePath)
        {
            if (outputSessions.TryGetValue(outputFilePath, out var existingSession))
            {
                return existingSession;
            }

            var createdSession = new ArcGisRasterExportPipeline(outputFilePath);
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

        private sealed record HistoricalDownloadedTile(HistoricalTileDefinition Tile, HistoricalTileDownloadResult Result);

        private sealed record HistoricalPlanContext(
            HistoricalTilePlan Plan,
            Polygon? PreciseClip,
            string SourceSpatialReferenceText,
            HistoricalDownloadPerformanceEvaluation Evaluation,
            IReadOnlyList<string> Messages);
    }
}
