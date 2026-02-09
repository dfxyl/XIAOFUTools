using System;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 只读图层清单工具。
    /// </summary>
    public sealed class LayerListTool : IGISTool
    {
        public const string ToolName = "list_map_layers";

        public string Name => ToolName;

        public string Description => "列出当前活动地图图层（只读），支持关键字过滤、可见性过滤和返回条数限制。";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["keyword"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "图层名关键字过滤"
                },
                ["include_hidden"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "是否包含隐藏图层"
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["default"] = 200,
                    ["minimum"] = 1,
                    ["maximum"] = 500,
                    ["description"] = "最多返回图层数量"
                }
            }
        };

        public Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            var keyword = parameters?["keyword"]?.ToString()?.Trim();
            var includeHidden = parameters?["include_hidden"]?.ToObject<bool?>() ?? true;
            var limit = parameters?["limit"]?.ToObject<int?>() ?? 200;
            limit = Math.Clamp(limit, 1, 500);

            return QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    return ToolResult.CreateError("当前没有活动地图视图");
                }

                var map = mapView.Map;
                var query = map.GetLayersAsFlattenedList().AsEnumerable();

                if (!includeHidden)
                {
                    query = query.Where(l => l.IsVisible);
                }

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    query = query.Where(l => l.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                var allMatches = query.ToList();
                var layers = allMatches
                    .Take(limit)
                    .Select(layer => new
                    {
                        name = layer.Name,
                        layerType = layer.GetType().Name,
                        isVisible = layer.IsVisible
                    })
                    .ToList();

                var result = new
                {
                    mapName = map.Name,
                    filter = new
                    {
                        keyword,
                        includeHidden,
                        limit
                    },
                    matchedCount = allMatches.Count,
                    returnedCount = layers.Count,
                    hasMore = allMatches.Count > layers.Count,
                    layers
                };

                return ToolResult.CreateSuccess($"已返回 {layers.Count} 个图层", result);
            });
        }
    }
}
