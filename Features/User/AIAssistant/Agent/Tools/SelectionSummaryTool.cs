using System;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 只读选择集摘要工具。
    /// </summary>
    public sealed class SelectionSummaryTool : IGISTool
    {
        public const string ToolName = "selection_summary";

        public string Name => ToolName;

        public string Description => "汇总当前地图选择集（各图层选中数量），可选返回 OID 样本。";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["include_oid_samples"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = false,
                    ["description"] = "是否返回每个图层的 OID 样本"
                },
                ["sample_limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["default"] = 20,
                    ["minimum"] = 1,
                    ["maximum"] = 200,
                    ["description"] = "每个图层返回的 OID 样本数量上限"
                }
            }
        };

        public Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            var includeOidSamples = parameters?["include_oid_samples"]?.ToObject<bool?>() ?? false;
            var sampleLimit = parameters?["sample_limit"]?.ToObject<int?>() ?? 20;
            sampleLimit = Math.Clamp(sampleLimit, 1, 200);

            return QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    return ToolResult.CreateError("当前没有活动地图视图");
                }

                var map = mapView.Map;
                var selection = map.GetSelection();
                if (selection == null)
                {
                    return ToolResult.CreateSuccess("当前无选择集", new
                    {
                        mapName = map.Name,
                        selectedLayerCount = 0,
                        selectedFeatureCount = 0,
                        layers = Array.Empty<object>()
                    });
                }

                var selectionDict = selection.ToDictionary();
                if (selectionDict.Count == 0)
                {
                    return ToolResult.CreateSuccess("当前无选择集", new
                    {
                        mapName = map.Name,
                        selectedLayerCount = 0,
                        selectedFeatureCount = 0,
                        layers = Array.Empty<object>()
                    });
                }

                var layers = selectionDict
                    .Select(kvp => new
                    {
                        layerName = kvp.Key.Name,
                        mapMemberType = kvp.Key.GetType().Name,
                        selectedCount = kvp.Value?.Count ?? 0,
                        oidSamples = includeOidSamples
                            ? (kvp.Value ?? Enumerable.Empty<long>()).Take(sampleLimit).ToList()
                            : null
                    })
                    .OrderByDescending(item => item.selectedCount)
                    .ToList();

                var totalSelected = layers.Sum(item => item.selectedCount);
                var result = new
                {
                    mapName = map.Name,
                    selectedLayerCount = layers.Count,
                    selectedFeatureCount = totalSelected,
                    includeOidSamples,
                    sampleLimit,
                    layers
                };

                return ToolResult.CreateSuccess($"已汇总 {layers.Count} 个图层的选择集", result);
            });
        }
    }
}
