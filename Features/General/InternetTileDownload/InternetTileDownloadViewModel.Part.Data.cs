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
using XIAOFUTools.Features.General.InternetTileDownload.Infrastructure;
using XIAOFUTools.Features.General.InternetTileDownload.Services;

namespace XIAOFUTools.Features.General.InternetTileDownload
{
    internal sealed partial class InternetTileDownloadViewModel
    {

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


        private async Task ResolveServiceAsync()
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "正在解析互联网切片服务...";
                Progress = 0;
                ClearResolvedService();

                _resolvedServiceDefinition = await _serviceResolver.ResolveAsync(ServiceUrl.Trim());
                ApplyResolvedService(_resolvedServiceDefinition);

                StatusMessage = "服务解析完成";
                Progress = 100;
                AppendLog($"已识别服务: {ServiceSummary}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"服务解析失败：{ex.Message}";
                AppendLog($"服务解析失败: {ex.Message}");
                ClearResolvedService();
            }
            finally
            {
                IsProcessing = false;
            }
        }


        private async Task DownloadAsync()
        {
            if (_resolvedServiceDefinition == null || SelectedLevelOption == null)
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
                    new InternetTileAreaContext
                    {
                        SourceType = SelectedAreaSourceOption?.AreaSourceType ?? InternetTileAreaSourceType.CurrentView,
                        PreferSelection = true
                    },
                    _customExtent,
                    SelectedFeatureLayer,
                    _cancellationTokenSource.Token);

                var outputFilePath = InternetTileOutputPathBuilder.BuildForFolder(OutputFolderPath, ServiceUrl, SelectedLevelOption.LevelId);
                await ReleaseOutputFileLocksAsync(outputFilePath);

                var executionRequest = new InternetTileDownloadExecutionRequest
                {
                    Request = new InternetTileDownloadRequest
                    {
                        ServiceDefinition = _resolvedServiceDefinition,
                        LevelId = SelectedLevelOption.LevelId,
                        OutputFilePath = outputFilePath,
                        UseCache = true
                    },
                    ResolvedArea = area,
                    TargetSpatialReferenceText = GetTargetSpatialReferenceText()
                };

                var inspection = await _downloadEngine.InspectAsync(executionRequest, _cancellationTokenSource.Token);
                foreach (var message in inspection.Messages)
                {
                    AppendLog(message);
                }

                if (inspection.ShouldBlock)
                {
                    throw new InvalidOperationException(string.Join(Environment.NewLine, inspection.Messages));
                }

                if (!inspection.CanDownload)
                {
                    StatusMessage = "未检测到可用瓦片";
                    return;
                }

                if (inspection.ShouldWarn)
                {
                    var warningText = string.Join(Environment.NewLine, inspection.Messages);
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

                StatusMessage = $"正在下载级别 {SelectedLevelOption.LevelId}...";
                AppendLog($"开始下载: {outputFilePath}");
                AppendLog($"预计瓦片数: {inspection.TotalTileCount}，自动并发: {inspection.RecommendedTileConcurrency}");

                var result = await _downloadEngine.DownloadAsync(
                    executionRequest,
                    new Progress<double>(value => Progress = value * 100d),
                    _cancellationTokenSource.Token);

                await AddOutputToMapAsync(result.OutputFilePath);
                AppendLog($"下载完成: {result.OutputFilePath}");
                foreach (var message in result.Messages.Take(20))
                {
                    AppendLog(message);
                }

                StatusMessage = result.HasPartialCoverage
                    ? $"下载完成：共 {result.DownloadedTileCount}/{result.TotalTileCount} 个瓦片，存在缺图"
                    : $"下载完成：共 {result.DownloadedTileCount} 个瓦片";
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


        private LevelOption? GetPreferredLevelOption(InternetTileServiceDefinition definition)
        {
            var defaultZoom = GetDefaultZoomLevel();
            var numericLevel = definition.Levels
                .Select(level => new
                {
                    LevelId = level.LevelId,
                    Parsed = TryExtractNumericLevel(level.LevelId)
                })
                .Where(item => item.Parsed.HasValue)
                .OrderBy(item => Math.Abs(item.Parsed!.Value - defaultZoom))
                .ThenByDescending(item => item.Parsed)
                .Select(item => item.LevelId)
                .FirstOrDefault();

            return LevelOptions.FirstOrDefault(item => item.LevelId == numericLevel)
                ?? LevelOptions.LastOrDefault();
        }


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


        private void ClearResolvedService()
        {
            _resolvedServiceDefinition = null;
            LevelOptions.Clear();
            SelectedLevelOption = null;
            ServiceSummary = "未解析服务";
            UpdateCommands();
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
