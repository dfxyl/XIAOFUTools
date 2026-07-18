using System;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 只读图层结构工具。
    /// </summary>
    public sealed class LayerSchemaTool : IGISTool
    {
        public const string ToolName = "describe_layer_schema";

        public string Name => ToolName;

        public string Description => "读取指定图层的结构信息（字段、几何类型、OID 字段），仅做只读检查。";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["layer_name"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "目标图层名称"
                },
                ["fuzzy_match"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "是否允许模糊匹配图层名"
                },
                ["max_fields"] = new JObject
                {
                    ["type"] = "integer",
                    ["default"] = 200,
                    ["minimum"] = 10,
                    ["maximum"] = 500,
                    ["description"] = "最多返回字段数量"
                }
            },
            ["required"] = new JArray { "layer_name" }
        };

        public Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            var layerName = parameters?["layer_name"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return Task.FromResult(ToolResult.CreateError("参数 layer_name 不能为空"));
            }

            var fuzzyMatch = parameters?["fuzzy_match"]?.ToObject<bool?>() ?? true;
            var maxFields = parameters?["max_fields"]?.ToObject<int?>() ?? 200;
            maxFields = Math.Clamp(maxFields, 10, 500);

            return QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    return ToolResult.CreateError("当前没有活动地图视图");
                }

                var map = mapView.Map;
                var allLayers = map.GetLayersAsFlattenedList();

                var targetLayer = allLayers.FirstOrDefault(l =>
                    l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));

                if (targetLayer == null && fuzzyMatch)
                {
                    targetLayer = allLayers.FirstOrDefault(l =>
                        l.Name.IndexOf(layerName, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                if (targetLayer == null)
                {
                    return ToolResult.CreateError($"未找到图层: {layerName}");
                }

                var selectedCount = 0;
                var selection = map.GetSelection();
                if (selection != null)
                {
                    var selectionDict = selection.ToDictionary();
                    if (selectionDict.TryGetValue(targetLayer, out var selectedIds))
                    {
                        selectedCount = selectedIds?.Count ?? 0;
                    }
                }

                if (targetLayer is FeatureLayer featureLayer)
                {
                    using var featureClass = featureLayer.GetFeatureClass();
                    if (featureClass == null)
                    {
                        return ToolResult.CreateError($"图层 {targetLayer.Name} 无法读取要素类");
                    }

                    var definition = featureClass.GetDefinition();
                    var fields = definition.GetFields()
                        .Take(maxFields)
                        .Select(field => new
                        {
                            name = field.Name,
                            alias = field.AliasName,
                            fieldType = field.FieldType.ToString(),
                            length = field.Length,
                            isNullable = field.IsNullable
                        })
                        .ToList();

                    var result = new
                    {
                        layer = new
                        {
                            name = targetLayer.Name,
                            layerType = targetLayer.GetType().Name,
                            isVisible = targetLayer.IsVisible,
                            selectedCount
                        },
                        geometry = new
                        {
                            shapeType = definition.GetShapeType().ToString(),
                            shapeField = definition.GetShapeField(),
                            objectIdField = definition.GetObjectIDField()
                        },
                        fields = new
                        {
                            total = definition.GetFields().Count,
                            returned = fields.Count,
                            maxFields,
                            items = fields
                        }
                    };

                    return ToolResult.CreateSuccess($"已读取图层 {targetLayer.Name} 的结构信息", result);
                }

                var genericResult = new
                {
                    layer = new
                    {
                        name = targetLayer.Name,
                        layerType = targetLayer.GetType().Name,
                        isVisible = targetLayer.IsVisible,
                        selectedCount
                    },
                    note = "该图层不是 FeatureLayer，当前工具仅详细支持要素图层结构读取"
                };

                return ToolResult.CreateSuccess($"已读取图层 {targetLayer.Name} 的基础信息", genericResult);
            });
        }
    }
}
