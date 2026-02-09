using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 按图层与条件读取有限条记录（只读）。
    /// 默认限制返回条数与文本长度，避免一次性导出大量真实数据。
    /// </summary>
    public sealed class LayerQueryTool : IGISTool
    {
        public const string ToolName = "layer_query";

        public string Name => ToolName;

        public string Description => "按图层名与 where 条件读取记录样本（只读，受限输出）。";

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
                ["where"] = new JObject
                {
                    ["type"] = "string",
                    ["default"] = "1=1",
                    ["description"] = "筛选条件，默认全量"
                },
                ["fields"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "返回字段列表，留空时自动返回前10个普通字段"
                },
                ["fuzzy_match"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "是否允许模糊匹配图层名"
                },
                ["limit"] = new JObject
                {
                    ["type"] = "integer",
                    ["default"] = 50,
                    ["minimum"] = 1,
                    ["maximum"] = 200,
                    ["description"] = "最多返回记录数"
                },
                ["max_string_length"] = new JObject
                {
                    ["type"] = "integer",
                    ["default"] = 200,
                    ["minimum"] = 20,
                    ["maximum"] = 1000,
                    ["description"] = "单个文本字段最大返回长度"
                },
                ["include_geometry"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = false,
                    ["description"] = "是否返回几何包络（仅FeatureLayer）"
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

            var whereClause = parameters?["where"]?.ToString();
            if (string.IsNullOrWhiteSpace(whereClause))
            {
                whereClause = "1=1";
            }

            var fuzzyMatch = parameters?["fuzzy_match"]?.ToObject<bool?>() ?? true;
            var includeGeometry = parameters?["include_geometry"]?.ToObject<bool?>() ?? false;
            var limit = Math.Clamp(parameters?["limit"]?.ToObject<int?>() ?? 50, 1, 200);
            var maxStringLength = Math.Clamp(parameters?["max_string_length"]?.ToObject<int?>() ?? 200, 20, 1000);
            var requestedFields = parameters?["fields"]?.ToObject<List<string>>() ?? new List<string>();

            return QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    return ToolResult.CreateError("当前没有活动地图视图");
                }

                var targetLayer = FindLayer(mapView.Map, layerName, fuzzyMatch);
                if (targetLayer == null)
                {
                    return ToolResult.CreateError($"未找到图层: {layerName}");
                }

                if (targetLayer is not FeatureLayer featureLayer)
                {
                    return ToolResult.CreateError("layer_query 仅支持要素图层");
                }

                using var featureClass = featureLayer.GetFeatureClass();
                if (featureClass == null)
                {
                    return ToolResult.CreateError($"图层 {targetLayer.Name} 无法读取要素类");
                }

                var definition = featureClass.GetDefinition();
                var allFields = definition.GetFields();
                var shapeField = definition.GetShapeField();

                var fieldNames = ResolveOutputFields(allFields, requestedFields, includeGeometry ? shapeField : null);
                if (fieldNames.Count == 0)
                {
                    return ToolResult.CreateError("没有可返回的字段，请检查 fields 参数");
                }

                var filter = new QueryFilter
                {
                    WhereClause = whereClause,
                    SubFields = string.Join(",", fieldNames)
                };

                var rows = new List<Dictionary<string, object>>();

                try
                {
                    using var cursor = featureClass.Search(filter, false);
                    while (cursor.MoveNext() && rows.Count < limit)
                    {
                        using var row = cursor.Current;
                        var item = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                        foreach (var fieldName in fieldNames)
                        {
                            var rawValue = row[fieldName];
                            item[fieldName] = NormalizeValue(rawValue, maxStringLength, includeGeometry && string.Equals(fieldName, shapeField, StringComparison.OrdinalIgnoreCase));
                        }

                        rows.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    return ToolResult.CreateError($"查询失败，请检查 where 条件: {ex.Message}");
                }

                var result = new
                {
                    layerName = targetLayer.Name,
                    where = whereClause,
                    returnedCount = rows.Count,
                    limit,
                    fields = fieldNames,
                    includeGeometry,
                    rows
                };

                return ToolResult.CreateSuccess($"已返回 {rows.Count} 条记录", result);
            });
        }

        private static Layer FindLayer(Map map, string layerName, bool fuzzyMatch)
        {
            var allLayers = map.GetLayersAsFlattenedList();
            var exact = allLayers.FirstOrDefault(l => l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }

            if (!fuzzyMatch)
            {
                return null;
            }

            return allLayers.FirstOrDefault(l => l.Name.IndexOf(layerName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static List<string> ResolveOutputFields(IReadOnlyList<Field> allFields, List<string> requestedFields, string geometryFieldName)
        {
            var result = new List<string>();
            var validNames = new HashSet<string>(allFields.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

            if (requestedFields != null && requestedFields.Count > 0)
            {
                foreach (var name in requestedFields.Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    if (!validNames.Contains(name) || result.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result.Add(name);
                }
            }

            if (result.Count == 0)
            {
                result = allFields
                    .Where(field => field.FieldType != FieldType.Blob
                                    && field.FieldType != FieldType.Raster
                                    && field.FieldType != FieldType.Geometry)
                    .Take(10)
                    .Select(field => field.Name)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(geometryFieldName)
                && validNames.Contains(geometryFieldName)
                && !result.Contains(geometryFieldName, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(geometryFieldName);
            }

            return result;
        }

        private static object NormalizeValue(object rawValue, int maxStringLength, bool isGeometryField)
        {
            if (rawValue == null || rawValue == DBNull.Value)
            {
                return null;
            }

            if (isGeometryField && rawValue is Geometry geometry)
            {
                var envelope = geometry.Extent;
                if (envelope == null)
                {
                    return null;
                }

                return new
                {
                    xmin = envelope.XMin,
                    ymin = envelope.YMin,
                    xmax = envelope.XMax,
                    ymax = envelope.YMax
                };
            }

            switch (rawValue)
            {
                case DateTime time:
                    return time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                case string text:
                    return text.Length <= maxStringLength ? text : text.Substring(0, maxStringLength) + "...";
                default:
                    return rawValue;
            }
        }
    }
}
