using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace XIAOFUTools.Tools.HistoricalImagery
{
    internal sealed record HistoricalImageryMapLoadResult(bool Succeeded, string ErrorMessage = null);

    internal sealed class HistoricalImageryMapLoadService
    {
        public async Task<HistoricalImageryMapLoadResult> LoadAsync(HistoricalImageryLayerRequest request)
        {
            if (request == null)
            {
                return new HistoricalImageryMapLoadResult(false, "请选择一个历史影像版本。");
            }

            return await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    return new HistoricalImageryMapLoadResult(false, "当前没有活动地图");
                }

                var layer = LayerFactory.Instance.CreateLayer(request.LayerUri, mapView.Map, layerName: request.LayerName);
                if (layer == null)
                {
                    return new HistoricalImageryMapLoadResult(false, "未能创建历史影像图层。");
                }

                var allLayers = mapView.Map.Layers.ToList();
                if (allLayers.Count > 1)
                {
                    mapView.Map.MoveLayer(layer, allLayers.Count - 1);
                }

                return new HistoricalImageryMapLoadResult(true);
            });
        }

        public async Task FinalizeDraggedLayerAsync(HistoricalImageryLayerRequest request, IEnumerable<Layer> addedLayers)
        {
            if (request == null || addedLayers == null)
            {
                return;
            }

            await QueuedTask.Run(() =>
            {
                var candidates = addedLayers.Where(layer => layer != null).ToList();
                if (candidates.Count == 0)
                {
                    return;
                }

                var layerToUpdate = candidates.FirstOrDefault(layer =>
                    string.Equals(layer.URI, request.LayerUri.AbsoluteUri, StringComparison.OrdinalIgnoreCase));

                if (layerToUpdate == null)
                {
                    return;
                }

                layerToUpdate.SetName(request.LayerName);

                var map = MapView.Active?.Map;
                if (map == null)
                {
                    return;
                }

                var allLayers = map.Layers.ToList();
                if (allLayers.Count > 1)
                {
                    map.MoveLayer(layerToUpdate, allLayers.Count - 1);
                }
            });
        }
    }
}
