using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ProtocolLineExtract
{
    internal partial class ProtocolLineExtractViewModel
    {

        public void Cleanup()
        {
            if (_mapSelectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_mapSelectionChangedToken);
                _mapSelectionChangedToken = null;
            }
        }

        /// <summary>
        /// 从地图中移除过程数据图层（兜底清理）
        /// </summary>
        private static async Task RemoveIntermediateLayersAsync(string tempWS, params string[] datasetPaths)
        {
            void removeCore()
            {
                try
                {
                    var map = MapView.Active?.Map;
                    if (map == null) return;
                    // 名称候选：固定过程名 + 由路径推导出的文件名（无扩展名）
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "poly2line", "proto_lines", "proto_lines_sel", "proto_merged"
                    };
                    foreach (var p in (datasetPaths ?? Array.Empty<string>()))
                    {
                        if (string.IsNullOrEmpty(p)) continue;
                        var n = Path.GetFileNameWithoutExtension(p);
                        if (!string.IsNullOrEmpty(n)) names.Add(n);
                    }
                    var layerList = map.GetLayersAsFlattenedList();
                    if (layerList == null || layerList.Count == 0)
                        return;
                    bool nameMatch(string layerName)
                    {
                        if (string.IsNullOrEmpty(layerName)) return false;
                        foreach (var n in names)
                        {
                            if (string.IsNullOrEmpty(n)) continue;
                            if (layerName.Equals(n, StringComparison.OrdinalIgnoreCase)) return true;
                            if (layerName.Equals(n + ".shp", StringComparison.OrdinalIgnoreCase)) return true;
                            // 处理 ArcGIS 自动追加的“ (2)”之类后缀
                            if (layerName.StartsWith(n + " ", StringComparison.OrdinalIgnoreCase)) return true;
                            if (layerName.StartsWith(n + "(", StringComparison.OrdinalIgnoreCase)) return true;
                        }
                        return false;
                    }

                    var toRemove = layerList
                        .OfType<FeatureLayer>()
                        .Where(fl => fl != null && nameMatch(fl.Name))
                        .ToList();
                    foreach (var lyr in toRemove)
                    {
                        try { map.RemoveLayer(lyr); } catch { }
                    }
                }
                catch { }
            }

            if (QueuedTask.OnWorker)
            {
                removeCore();
                return;
            }
            await QueuedTask.Run(() => removeCore());
        }
    }
}
