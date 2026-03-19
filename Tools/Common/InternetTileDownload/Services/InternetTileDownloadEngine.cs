#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using XIAOFUTools.Tools.Common.RasterExport;
using XIAOFUTools.Tools.HistoricalImageryDownload.Services;
using XIAOFUTools.Tools.InternetTileDownload.Core;
using XIAOFUTools.Tools.InternetTileDownload.Infrastructure;
using HistoricalPerformanceAdvisor = XIAOFUTools.Tools.HistoricalImageryDownload.Services.HistoricalDownloadPerformanceAdvisor;
using HistoricalPerformanceEvaluation = XIAOFUTools.Tools.HistoricalImageryDownload.Services.HistoricalDownloadPerformanceEvaluation;

namespace XIAOFUTools.Tools.InternetTileDownload.Services
{
    internal sealed class InternetTileDownloadEngine
    {
        private readonly IInternetTileHttpClient _httpClient;

        public InternetTileDownloadEngine(IInternetTileHttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new InternetTileHttpClient();
        }

        public async Task<InternetTileDownloadInspectionResult> InspectAsync(
            InternetTileDownloadExecutionRequest executionRequest,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var planContext = CreatePlanContext(executionRequest.Request, executionRequest.ResolvedArea);
            var sampledTiles = InternetTileCoverageProbeService.CreateSample(planContext.Plan.Tiles);
            var coverage = await InternetTileCoverageProbeService.ProbeAsync(
                _httpClient,
                executionRequest.Request.ServiceDefinition,
                executionRequest.Request.LevelId,
                sampledTiles,
                cancellationToken);

            var messages = new List<string>(planContext.Evaluation.Messages);
            if (!coverage.HasCoverage)
            {
                messages.Add($"预检未检测到可用瓦片（抽样检查 {coverage.CheckedTileCount} 个，命中 0 个），已停止下载。请检查服务链接、范围、级别、权限令牌或网络连通性后重试。");
            }

            return new InternetTileDownloadInspectionResult
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

            using var exportPipeline = new ArcGisRasterExportPipeline(request.OutputFilePath);

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

                    exportPipeline.AddTile(
                        request.LevelId,
                        batchResult.Tile.Row,
                        batchResult.Tile.Column,
                        batchResult.Tile.MinX,
                        batchResult.Tile.MinY,
                        batchResult.Tile.MaxX,
                        batchResult.Tile.MaxY,
                        batchResult.ImageBytes!,
                        planContext.PreciseClip);

                    result.DownloadedTileCount++;
                    if (!string.IsNullOrWhiteSpace(batchResult.Message))
                    {
                        result.Messages.Add(batchResult.Message);
                    }

                    progress?.Report(processedTileCount / (planContext.Plan.Tiles.Count + 1d));
                }
            }

            InternetTileDownloadCompletionGuard.EnsureHasAnyTile(result.DownloadedTileCount, result.TotalTileCount);
            await exportPipeline.ExportAsync(planContext.SourceSpatialReferenceText, executionRequest.TargetSpatialReferenceText, cancellationToken);
            progress?.Report(1);
            result.HasPartialCoverage |= result.DownloadedTileCount < result.TotalTileCount;

            foreach (var message in planContext.Evaluation.Messages)
            {
                result.Messages.Add(message);
            }

            return result;
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
