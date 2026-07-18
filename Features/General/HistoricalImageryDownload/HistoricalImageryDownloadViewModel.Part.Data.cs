#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    internal sealed partial class HistoricalImageryDownloadViewModel
    {

        private int GetDefaultZoomLevel()
        {
            var mapView = MapView.Active;
            if (mapView == null)
            {
                return 18;
            }

            var mapScale = mapView.Camera.Scale;
            var zoomLevel = (int)Math.Round(Math.Log(591657550.5 / mapScale, 2));
            return Math.Clamp(zoomLevel, 1, 23);
        }


        private async Task LoadFeatureLayersAsync()
        {
            await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                var layers = map?.GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .Where(layer => layer.ShapeType == ArcGIS.Core.CIM.esriGeometryType.esriGeometryPolygon)
                    .ToList() ?? [];

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    FeatureLayers.Clear();
                    foreach (var layer in layers)
                    {
                        FeatureLayers.Add(layer);
                    }

                    if (SelectedFeatureLayer == null && FeatureLayers.Count > 0)
                    {
                        SelectedFeatureLayer = FeatureLayers[0];
                    }
                });
            });
        }


        private async Task QueryVersionsAsync()
        {
            if (SelectedProviderOption == null)
            {
                return;
            }

            try
            {
                IsProcessing = true;
                StatusMessage = "正在查询当前位置历史版本...";
                Progress = 0;
                ResetVersions();

                var query = await GetCurrentCenterQueryAsync();
                var provider = _providerFactory.GetProvider(SelectedProviderOption.ProviderType);
                var versions = await provider.QueryVersionsAsync(query);

                ApplyVersions(versions);

                StatusMessage = $"查询完成：{Versions.Count} 个历史版本";
                Progress = 100;
                AppendLog($"[{SelectedProviderOption.DisplayName}] {QueryModeDisplayText}：{Versions.Count} 个版本");
            }
            catch (Exception ex)
            {
                StatusMessage = $"查询失败：{ex.Message}";
                AppendLog($"查询失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }


        private async Task<HistoricalVersionQuery> GetCurrentCenterQueryAsync()
        {
            return await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active ?? throw new InvalidOperationException("当前没有活动地图视图。");
                var center = mapView.Extent.Center;
                var centerWgs84 = GeometryEngine.Instance.Project(center, SpatialReferences.WGS84) as MapPoint
                    ?? throw new InvalidOperationException("无法获取当前地图中心点。");

                return new HistoricalVersionQuery
                {
                    Longitude = centerWgs84.X,
                    Latitude = centerWgs84.Y,
                    ZoomLevel = SelectedZoomLevel,
                    IncludeAllVersions = IsQueryAllVersions
                };
            });
        }


        private async Task DownloadAsync()
        {
            if (SelectedProviderOption == null)
            {
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            try
            {
                IsProcessing = true;
                Progress = 0;
                StatusMessage = "正在准备下载...";

                var area = await _areaResolver.ResolveAsync(
                    new HistoricalAreaContext
                    {
                        SourceType = SelectedAreaSourceOption?.AreaSourceType ?? HistoricalAreaSourceType.CurrentView,
                        PreferSelection = true
                    },
                    _customExtent,
                    SelectedFeatureLayer,
                    _cancellationTokenSource.Token);

                var requests = HistoricalBatchDownloadPlanner.CreateRequests(
                    SelectedProviderOption.ProviderType,
                    area.SourceType,
                    Versions,
                    SelectedZoomLevel,
                    OutputFolderPath,
                    SelectedProviderOption.ProviderType == HistoricalImageryProviderType.GoogleEarth
                        ? GoogleFallbackMode
                        : GoogleNearestDateFallbackMode.SeparateOutputs);

                if (requests.Count == 0)
                {
                    throw new InvalidOperationException("请至少勾选一个历史版本。");
                }

                var completedCount = 0;
                var partialCount = 0;
                var targetSpatialReferenceText = GetTargetSpatialReferenceText();
                var inspections = new List<(HistoricalDownloadRequest Request, HistoricalDownloadExecutionRequest ExecutionRequest, HistoricalDownloadInspectionResult Inspection)>();

                foreach (var request in requests)
                {
                    var executionRequest = new HistoricalDownloadExecutionRequest
                    {
                        Request = request,
                        ResolvedArea = area,
                        TargetSpatialReferenceText = targetSpatialReferenceText
                    };

                    var inspection = await _downloadEngine.InspectAsync(executionRequest, _cancellationTokenSource.Token);
                    inspections.Add((request, executionRequest, inspection));
                }

                var blockedInspection = inspections.FirstOrDefault(item => item.Inspection.ShouldBlock);
                if (blockedInspection.Inspection != null && blockedInspection.Inspection.ShouldBlock)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, blockedInspection.Inspection.Messages));
                }

                foreach (var inspection in inspections.Where(item => !item.Inspection.CanDownload))
                {
                    foreach (var message in inspection.Inspection.Messages)
                    {
                        AppendLog(message);
                    }
                }

                var runnableRequests = inspections.Where(item => item.Inspection.CanDownload).ToList();
                if (runnableRequests.Count == 0)
                {
                    throw new InvalidOperationException("所选版本在当前范围和级别下没有可用影像，已全部跳过。");
                }

                if (SelectedProviderOption.ProviderType == HistoricalImageryProviderType.GoogleEarth)
                {
                    AppendLog(GoogleFallbackMode == GoogleNearestDateFallbackMode.SeparateOutputs
                        ? "Google 缺失瓦片将按实际日期分别输出多个结果文件。"
                        : "Google 缺失瓦片将回退到相近日期，并混合写入同一个结果文件。");
                }

                var overallEvaluation = HistoricalDownloadPerformanceAdvisor.Evaluate(
                    runnableRequests.Max(item => item.Inspection.TotalTileCount),
                    runnableRequests.Count,
                    area.RequiresPreciseClip);

                foreach (var message in overallEvaluation.Messages)
                {
                    AppendLog(message);
                }

                if (overallEvaluation.ShouldWarn)
                {
                    var warningText = string.Join(Environment.NewLine, overallEvaluation.Messages);
                    var dialogResult = PresentationServices.Dialogs.Show(
                        $"{warningText}{Environment.NewLine}{Environment.NewLine}是否继续下载？",
                        "大范围下载提示",
                        System.Windows.MessageBoxButton.OKCancel,
                        System.Windows.MessageBoxImage.Warning);
                    if (dialogResult != System.Windows.MessageBoxResult.OK)
                    {
                        StatusMessage = "下载已取消";
                        AppendLog("用户取消了大范围下载。");
                        return;
                    }
                }

                foreach (var item in runnableRequests)
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    var request = item.Request;
                    var executionRequest = item.ExecutionRequest;
                    StatusMessage = $"正在下载 {completedCount + 1}/{runnableRequests.Count}: {request.Version.DisplayDate}";
                    AppendLog($"开始下载: {request.OutputFilePath}");
                    AppendLog($"预计瓦片数: {item.Inspection.TotalTileCount}，自动并发: {item.Inspection.RecommendedTileConcurrency}");

                    await ReleaseOutputFileLocksAsync(request.OutputFilePath);

                    var result = await _downloadEngine.DownloadAsync(
                        executionRequest,
                        new Progress<double>(value => Progress = ((completedCount + value) / runnableRequests.Count) * 100d),
                        _cancellationTokenSource.Token);

                    completedCount++;
                    partialCount += result.HasPartialCoverage ? 1 : 0;

                    IEnumerable<string> outputFilePaths = result.OutputFilePaths.Count > 0
                        ? result.OutputFilePaths
                        : new[] { result.OutputFilePath };
                    foreach (var outputFilePath in outputFilePaths)
                    {
                        await AddOutputToMapAsync(outputFilePath);
                        AppendLog($"[{completedCount}/{requests.Count}] 下载完成: {outputFilePath}");
                    }
                    foreach (var message in result.Messages.Take(20))
                    {
                        AppendLog(message);
                    }
                }

                StatusMessage = partialCount > 0
                    ? $"批量下载完成：{completedCount} 个文件，{partialCount} 个存在缺图"
                    : $"批量下载完成：{completedCount} 个文件";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "下载已取消";
                AppendLog("下载已取消");
            }
            catch (Exception ex)
            {
                StatusMessage = $"下载失败：{ex.Message}";
                AppendLog($"下载失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                Progress = 0;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }


        private string? GetTargetSpatialReferenceText()
        {
            var sr = _selectedOutputSpatialReference ?? MapView.Active?.Map?.SpatialReference;
            if (sr == null)
            {
                return null;
            }

            if (sr.Wkid > 0)
            {
                return $"EPSG:{sr.Wkid}";
            }

            return sr.Wkt;
        }

    }
}
