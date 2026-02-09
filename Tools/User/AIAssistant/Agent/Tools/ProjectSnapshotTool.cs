using System;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 只读工程快照工具。
    /// </summary>
    public sealed class ProjectSnapshotTool : IGISTool
    {
        public const string ToolName = "project_snapshot";

        public string Name => ToolName;

        public string Description => "获取当前 ArcGIS Pro 工程与活动地图的只读快照（工程、坐标系、范围、图层统计、选择集统计）。";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["include_extent"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "是否返回当前视图范围"
                },
                ["include_layer_summary"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "是否返回图层类型统计"
                }
            }
        };

        public Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            var includeExtent = parameters?["include_extent"]?.ToObject<bool?>() ?? true;
            var includeLayerSummary = parameters?["include_layer_summary"]?.ToObject<bool?>() ?? true;

            return QueuedTask.Run(() =>
            {
                var project = Project.Current;
                if (project == null)
                {
                    return ToolResult.CreateError("当前未打开 ArcGIS Pro 工程");
                }

                var mapView = MapView.Active;
                var map = mapView?.Map;
                var spatialReference = map?.SpatialReference;

                object extentInfo = null;
                if (includeExtent && mapView?.Extent != null)
                {
                    var extent = mapView.Extent;
                    extentInfo = new
                    {
                        xmin = extent.XMin,
                        ymin = extent.YMin,
                        xmax = extent.XMax,
                        ymax = extent.YMax,
                        width = extent.Width,
                        height = extent.Height
                    };
                }

                object layerSummaryInfo = null;
                if (includeLayerSummary && map != null)
                {
                    var layers = map.GetLayersAsFlattenedList();
                    var typeStats = layers
                        .GroupBy(l => l.GetType().Name)
                        .OrderByDescending(g => g.Count())
                        .Select(g => new
                        {
                            layerType = g.Key,
                            count = g.Count()
                        })
                        .Take(12)
                        .ToList();

                    layerSummaryInfo = new
                    {
                        totalLayers = layers.Count,
                        visibleLayers = layers.Count(l => l.IsVisible),
                        hiddenLayers = layers.Count(l => !l.IsVisible),
                        byType = typeStats
                    };
                }

                var selectionLayers = 0;
                var selectionFeatures = 0;
                if (map != null)
                {
                    var selection = map.GetSelection();
                    if (selection != null)
                    {
                        var selectionDict = selection.ToDictionary();
                        selectionLayers = selectionDict.Count;
                        selectionFeatures = selectionDict.Values.Sum(ids => ids?.Count ?? 0);
                    }
                }

                var result = new
                {
                    project = new
                    {
                        name = project.Name,
                        path = project.URI,
                        defaultGeodatabase = project.DefaultGeodatabasePath,
                        defaultToolbox = project.DefaultToolboxPath
                    },
                    map = map == null
                        ? null
                        : new
                        {
                            name = map.Name,
                            mapType = map.MapType.ToString(),
                            spatialReference = spatialReference == null
                                ? null
                                : new
                                {
                                    name = spatialReference.Name,
                                    wkid = spatialReference.Wkid,
                                    unit = spatialReference.Unit?.Name
                                }
                        },
                    extent = extentInfo,
                    layerSummary = layerSummaryInfo,
                    selectionSummary = new
                    {
                        selectedLayerCount = selectionLayers,
                        selectedFeatureCount = selectionFeatures
                    },
                    generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                return ToolResult.CreateSuccess("已生成工程快照", result);
            });
        }
    }
}
