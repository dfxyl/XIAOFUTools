using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Common
{
    /// <summary>
    /// 图层获取通用工具。
    /// 提供在活动地图中按几何类型检索要素图层的方法。
    /// 注意：涉及 Table/Definition 的调用需在 MCT（QueuedTask）内部执行。
    /// </summary>
    public static class LayerUtils
    {
        /// <summary>
        /// 获取活动地图中所有要素图层（可选按几何类型过滤）。
        /// </summary>
        public static async Task<List<FeatureLayer>> GetFeatureLayersAsync(GeometryType? shapeTypeFilter = null)
        {
            var result = new List<FeatureLayer>();
            await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map == null) return;
                foreach (var fl in map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
                {
                    try
                    {
                        using var table = fl.GetTable();
                        if (table == null) continue;
                        var def = table.GetDefinition() as FeatureClassDefinition;
                        if (def == null) continue;
                        if (shapeTypeFilter == null || def.GetShapeType() == shapeTypeFilter.Value)
                        {
                            result.Add(fl);
                        }
                    }
                    catch { }
                }
            });
            return result;
        }

        public static Task<List<FeatureLayer>> GetPolygonLayersAsync() => GetFeatureLayersAsync(GeometryType.Polygon);
        public static Task<List<FeatureLayer>> GetPolylineLayersAsync() => GetFeatureLayersAsync(GeometryType.Polyline);
        public static Task<List<FeatureLayer>> GetPointLayersAsync() => GetFeatureLayersAsync(GeometryType.Point);

        /// <summary>
        /// 获取图层的空间参考（在 MCT 内调用）。
        /// </summary>
        public static SpatialReference GetSpatialReference(FeatureLayer layer)
        {
            if (!QueuedTask.OnWorker)
                throw new InvalidOperationException("GetSpatialReference 必须在 QueuedTask(MCT) 中调用");
            if (layer == null) return null;
            using var table = layer.GetTable();
            var def = table?.GetDefinition() as FeatureClassDefinition;
            return def?.GetSpatialReference();
        }
    }
}
