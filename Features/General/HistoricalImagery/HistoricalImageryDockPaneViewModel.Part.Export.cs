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
        /// 构建树形结构
        /// </summary>
        private void BuildTreeStructure(List<WaybackVersion> versions)
        {
            var yearGroups = versions
                .Where(v => v.Year > 0)
                .GroupBy(v => v.Year)
                .OrderByDescending(g => g.Key);

            TreeNodes.Clear();

            foreach (var yearGroup in yearGroups)
            {
                var yearNode = new TreeNode
                {
                    Header = $"{yearGroup.Key}年 ({yearGroup.Count()}个版本)",
                    IsExpanded = yearGroup.Key == DateTime.Now.Year || versions.Count <= 50 // 搜索结果少时全部展开
                };

                foreach (var version in yearGroup.OrderByDescending(v => v.ReleaseDate))
                {
                    yearNode.Children.Add(new TreeNode
                    {
                        Header = version.DisplayName,
                        Version = version
                    });
                }

                TreeNodes.Add(yearNode);
            }
        }


        internal bool TryCreateLayerRequest(WaybackVersion version, out HistoricalImageryLayerRequest request, out string errorMessage)
        {
            return HistoricalImageryLayerRequestFactory.TryCreate(
                version?.ReleaseDate ?? string.Empty,
                version?.ItemTitle ?? string.Empty,
                version?.Url ?? string.Empty,
                version?.ReleaseNum ?? 0,
                out request,
                out errorMessage);
        }


        private async Task ApplyPendingDragRequestAsync(dynamic args)
        {
            if (_pendingDragRequest == null)
            {
                return;
            }

            if ((DateTime.UtcNow - _pendingDragRequestTimeUtc).TotalSeconds > 15)
            {
                _pendingDragRequest = null;
                return;
            }

            var layers = args?.Layers as IEnumerable<Layer>;
            if (layers == null)
            {
                return;
            }

            var request = _pendingDragRequest;
            _pendingDragRequest = null;
            await _mapLoadService.FinalizeDraggedLayerAsync(request, layers);
        }


        private async Task<HistoricalVersionQuery> BuildCurrentMapQueryAsync()
        {
            return await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    throw new InvalidOperationException("当前没有活动地图，无法检测当前位置的变化版本");
                }

                var center = mapView.Extent.Center;
                var centerWgs84 = GeometryEngine.Instance.Project(center, SpatialReferences.WGS84) as MapPoint
                    ?? throw new InvalidOperationException("无法获取当前地图中心点");

                var zoomLevel = (int)Math.Round(Math.Log(591657550.5 / mapView.Camera.Scale, 2));
                zoomLevel = Math.Clamp(zoomLevel, 1, 23);

                return new HistoricalVersionQuery
                {
                    Longitude = centerWgs84.X,
                    Latitude = centerWgs84.Y,
                    ZoomLevel = zoomLevel,
                    IncludeAllVersions = false
                };
            });
        }


        private static string BuildMetadataDisplay(WaybackMetadataSourceCandidate candidate, ImageryMetadata metadata)
        {
            return $"{BuildSourceHeader(candidate)}\n" +
                   $"采集日期: {metadata.AcquisitionDate ?? "N/A"} | " +
                   $"卫星: {metadata.Satellite ?? "N/A"} | " +
                   $"分辨率: {metadata.Resolution}m | " +
                   $"级别: {metadata.ZoomLevel}";
        }


        private static string BuildNoDataDisplay(WaybackMetadataSourceCandidate candidate)
            => $"{BuildSourceHeader(candidate)}\n该位置无影像数据";


        private static string BuildSourceHeader(WaybackMetadataSourceCandidate candidate)
        {
            var versionText = string.IsNullOrWhiteSpace(candidate.Version.DisplayDate)
                ? candidate.Version.VersionId
                : candidate.Version.DisplayDate;

            return candidate.SourceType == WaybackMetadataSourceType.GlobalLatest
                ? $"查询来源: 全局最新版本\n版本: {versionText}"
                : $"查询来源: 当前地图图层\n图层: {candidate.LayerName ?? "未命名图层"}\n版本: {versionText}";
        }

    }
}
