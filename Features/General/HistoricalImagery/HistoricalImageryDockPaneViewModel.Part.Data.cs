using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using XIAOFUTools.Features.General.HistoricalImageryDownload;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Features.General.HistoricalImagery
{
    internal partial class HistoricalImageryDockPaneViewModel
    {

        /// <summary>
        /// 将地图缩放级别转换为查询级别
        /// </summary>
        private int MapZoomToQueryLevel(double mapZoom)
        {
            int zoom = (int)Math.Round(mapZoom);
            
            if (zoom >= 19) return 4;
            if (zoom == 18) return 5;
            if (zoom == 17) return 6;
            if (zoom == 16) return 7;
            if (zoom == 15) return 8;
            if (zoom == 14) return 9;
            if (zoom == 13) return 10;
            if (zoom == 12) return 11;
            if (zoom == 11) return 12;
            
            // 无12以上
            return 12;
        }


        /// <summary>
        /// 查询当前位置的影像元数据
        /// </summary>
        private async Task QueryCurrentMetadataAsync()
        {
            try
            {
                if (MapView.Active?.Map == null)
                {
                    MetadataDisplay = "当前没有活动地图";
                    return;
                }

                // 获取最新版本
                if (_catalogVersions == null || _catalogVersions.Count == 0)
                {
                    MetadataDisplay = "请先加载历史影像列表";
                    return;
                }

                MetadataDisplay = "正在查询...";

                var queryContext = await CaptureMetadataQueryContextAsync();
                var catalog = _catalogVersions.Select(ToHistoricalVersionItem).ToList();
                var resolution = WaybackMetadataSourceResolver.Resolve(catalog, queryContext.MapLayers);
                var candidate = await ResolveMetadataSourceCandidateAsync(resolution);
                if (candidate == null)
                {
                    MetadataDisplay = "已取消查询";
                    return;
                }

                if (string.IsNullOrEmpty(candidate.Version.MetadataLayerUrl))
                {
                    MetadataDisplay = "所选版本没有元数据服务";
                    return;
                }

                var metadata = await QueryWaybackMetadata(
                    queryContext.Longitude,
                    queryContext.Latitude,
                    queryContext.QueryLevel,
                    candidate.Version.MetadataLayerUrl,
                    candidate.Version.Summary ?? candidate.Version.DisplayDate ?? candidate.Version.VersionId);

                if (metadata != null)
                {
                    CurrentMetadata = metadata;
                    MetadataDisplay = BuildMetadataDisplay(candidate, metadata);
                }
                else
                {
                    MetadataDisplay = BuildNoDataDisplay(candidate);
                }
            }
            catch (Exception ex)
            {
                MetadataDisplay = $"查询失败: {ex.Message}";
            }
        }


        /// <summary>
        /// 查询单个版本的元数据
        /// </summary>
        private async Task QuerySingleVersionMetadataAsync(WaybackVersion version, double lon, double lat, int zoomLevel)
        {
            if (string.IsNullOrEmpty(version.MetadataLayerUrl))
                return;

            try
            {
                var metadata = await QueryWaybackMetadata(lon, lat, zoomLevel, version.MetadataLayerUrl, version.ItemTitle);
                if (metadata != null)
                {
                    version.AcquisitionDate = metadata.AcquisitionDate;
                }
            }
            catch
            {
                // 忽略单个查询失败
            }
        }


        /// <summary>
        /// 加载 Wayback 版本列表
        /// </summary>
        private async Task LoadVersionsAsync()
        {
            IsLoading = true;
            StatusMessage = "正在获取历史影像列表...";

            try
            {
                var catalogVersions = await GetWaybackVersionsAsync(includeAllVersions: true);
                _catalogVersions = catalogVersions;

                if (ShowChangedOnly)
                {
                    _allVersions = await GetWaybackVersionsAsync(includeAllVersions: false);
                }
                else
                {
                    _allVersions = catalogVersions.Select(CloneVersion).ToList();
                }

                if (_allVersions != null && _allVersions.Count > 0)
                {
                    BuildTreeStructure(_allVersions);

                    var yearCount = _allVersions.Select(v => v.Year).Distinct().Count();
                    StatusMessage = ShowChangedOnly
                        ? $"共找到 {_allVersions.Count} 个当前位置发生真实变化的版本，分布在 {yearCount} 年"
                        : $"共找到 {_allVersions.Count} 个历史影像版本，分布在 {yearCount} 年";
                }
                else
                {
                    StatusMessage = "未获取到历史影像版本";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"获取失败: {ex.Message}";
                PresentationServices.Dialogs.Show(
                    $"获取历史影像列表失败：{ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }


        /// <summary>
        /// 获取 ArcGIS Wayback 历史影像版本列表
        /// </summary>
        private async Task<List<WaybackVersion>> GetWaybackVersionsAsync(bool includeAllVersions)
        {
            var query = includeAllVersions
                ? new HistoricalVersionQuery
                {
                    IncludeAllVersions = true,
                    ZoomLevel = 12
                }
                : await BuildCurrentMapQueryAsync();

            var versions = await _waybackProvider.QueryVersionsAsync(query);
            return versions.Select(MapVersion).ToList();
        }


        private async Task<MetadataQueryContext> CaptureMetadataQueryContextAsync()
        {
            return await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    throw new InvalidOperationException("当前没有活动地图");
                }

                var center = mapView.Extent.Center;
                var centerWgs84 = GeometryEngine.Instance.Project(center, SpatialReferences.WGS84) as MapPoint
                    ?? throw new InvalidOperationException("无法获取当前地图中心点");

                var mapScale = mapView.Camera.Scale;
                var zoomLevel = (int)Math.Round(Math.Log(591657550.5 / mapScale, 2));
                var queryLevel = MapZoomToQueryLevel(zoomLevel);
                var mapLayers = mapView.Map.GetLayersAsFlattenedList()
                    .Select(layer => new WaybackMapLayerReference(layer.Name, layer.URI))
                    .ToList();

                return new MetadataQueryContext(centerWgs84.X, centerWgs84.Y, queryLevel, mapLayers);
            });
        }


        private async Task<WaybackMetadataSourceCandidate?> ResolveMetadataSourceCandidateAsync(WaybackMetadataSourceResolution resolution)
        {
            if (!resolution.RequiresSelection)
            {
                return resolution.AutoSelectedCandidate;
            }

            return await _metadataSourceDialogService.SelectAsync(resolution.Candidates);
        }


        /// <summary>
        /// 查询Wayback影像元数据
        /// </summary>
        private async Task<ImageryMetadata> QueryWaybackMetadata(double lon, double lat, int zoomLevel, string metadataUrl, string releaseTitle)
        {
            try
            {
                // 构建查询URL
                var queryUrl = $"{metadataUrl}/{zoomLevel}/query";
                
                var geometry = new
                {
                    spatialReference = new { wkid = 4326 },
                    x = lon,
                    y = lat
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

                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                var content = new FormUrlEncodedContent(queryParams);
                var response = await _httpClient.PostAsync(queryUrl, content, cts.Token);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(json);

                // 检查错误
                if (result.TryGetProperty("error", out var error))
                {
                    return null;
                }

                // 检查是否有数据
                if (!result.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
                {
                    return null;
                }

                // 解析第一个要素的属性
                var attrs = features[0].GetProperty("attributes");
                
                string acquisitionDate = null;
                if (attrs.TryGetProperty("SRC_DATE2", out var srcDate) && srcDate.ValueKind != JsonValueKind.Null)
                {
                    long timestamp = srcDate.GetInt64();
                    var date = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).DateTime;
                    acquisitionDate = date.ToString("yyyy-MM-dd");
                }

                var metadata = new ImageryMetadata
                {
                    AcquisitionDate = acquisitionDate,
                    Provider = attrs.TryGetProperty("NICE_DESC", out var provider) ? provider.GetString() : "N/A",
                    Satellite = attrs.TryGetProperty("SRC_DESC", out var satellite) ? satellite.GetString() : "N/A",
                    Resolution = attrs.TryGetProperty("SAMP_RES", out var resolution) ? resolution.ToString() : "N/A",
                    Accuracy = attrs.TryGetProperty("SRC_ACC", out var accuracy) ? accuracy.ToString() : "N/A",
                    ZoomLevel = zoomLevel,
                    ReleaseTitle = releaseTitle
                };

                return metadata;
            }
            catch
            {
                return null;
            }
        }

    }
}
