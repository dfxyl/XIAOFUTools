using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 字段质量画像工具（只读）。
    /// 通过受限扫描输出空值率、基数和数值统计，不直接导出全量明细数据。
    /// </summary>
    public sealed class FieldProfileTool : IGISTool
    {
        public const string ToolName = "field_profile";

        public string Name => ToolName;

        public string Description => "统计字段空值率、基数与基础分布（只读，受限扫描）。";

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
                ["fields"] = new JObject
                {
                    ["type"] = "array",
                    ["items"] = new JObject { ["type"] = "string" },
                    ["description"] = "需要统计的字段，留空时默认统计前20个普通字段"
                },
                ["fuzzy_match"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "是否允许模糊匹配图层名"
                },
                ["max_scan_rows"] = new JObject
                {
                    ["type"] = "integer",
                    ["default"] = 5000,
                    ["minimum"] = 100,
                    ["maximum"] = 20000,
                    ["description"] = "最多扫描记录数"
                },
                ["max_distinct"] = new JObject
                {
                    ["type"] = "integer",
                    ["default"] = 20,
                    ["minimum"] = 5,
                    ["maximum"] = 100,
                    ["description"] = "每个字段最多保留的去重样本值数量"
                },
                ["include_samples"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "是否返回去重样本值"
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

            var requestedFields = parameters?["fields"]?.ToObject<List<string>>() ?? new List<string>();
            var fuzzyMatch = parameters?["fuzzy_match"]?.ToObject<bool?>() ?? true;
            var maxScanRows = Math.Clamp(parameters?["max_scan_rows"]?.ToObject<int?>() ?? 5000, 100, 20000);
            var maxDistinct = Math.Clamp(parameters?["max_distinct"]?.ToObject<int?>() ?? 20, 5, 100);
            var includeSamples = parameters?["include_samples"]?.ToObject<bool?>() ?? true;

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
                    return ToolResult.CreateError("field_profile 仅支持要素图层");
                }

                using var featureClass = featureLayer.GetFeatureClass();
                if (featureClass == null)
                {
                    return ToolResult.CreateError($"图层 {targetLayer.Name} 无法读取要素类");
                }

                var definition = featureClass.GetDefinition();
                var selectedFields = ResolveProfileFields(definition.GetFields(), requestedFields);
                if (selectedFields.Count == 0)
                {
                    return ToolResult.CreateError("没有可统计字段，请检查 fields 参数");
                }

                var accumulators = selectedFields
                    .ToDictionary(field => field.Name, field => new FieldAccumulator(field, maxDistinct, includeSamples), StringComparer.OrdinalIgnoreCase);

                var filter = new QueryFilter
                {
                    WhereClause = "1=1",
                    SubFields = string.Join(",", selectedFields.Select(f => f.Name))
                };

                var scannedRows = 0;
                using (var cursor = featureClass.Search(filter, false))
                {
                    while (cursor.MoveNext() && scannedRows < maxScanRows)
                    {
                        using var row = cursor.Current;
                        scannedRows++;

                        foreach (var field in selectedFields)
                        {
                            var raw = row[field.Name];
                            accumulators[field.Name].Add(raw);
                        }
                    }
                }

                var profiles = selectedFields.Select(field => accumulators[field.Name].BuildProfile(scannedRows)).ToList();

                var result = new
                {
                    layerName = targetLayer.Name,
                    scannedRows,
                    maxScanRows,
                    maxDistinct,
                    includeSamples,
                    fields = profiles
                };

                return ToolResult.CreateSuccess($"字段画像完成，扫描 {scannedRows} 条记录", result);
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

        private static List<Field> ResolveProfileFields(IReadOnlyList<Field> allFields, List<string> requestedFields)
        {
            var validNames = new HashSet<string>(allFields.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            var result = new List<Field>();

            if (requestedFields != null && requestedFields.Count > 0)
            {
                foreach (var name in requestedFields.Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    if (!validNames.Contains(name) || result.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var field = allFields.First(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (IsSupportedFieldType(field.FieldType))
                    {
                        result.Add(field);
                    }
                }
            }

            if (result.Count == 0)
            {
                result = allFields
                    .Where(f => IsSupportedFieldType(f.FieldType))
                    .Take(20)
                    .ToList();
            }

            return result;
        }

        private static bool IsSupportedFieldType(FieldType fieldType)
        {
            return fieldType != FieldType.Blob
                   && fieldType != FieldType.Raster
                   && fieldType != FieldType.Geometry;
        }

        private sealed class FieldAccumulator
        {
            private readonly Field _field;
            private readonly int _maxDistinct;
            private readonly bool _includeSamples;
            private readonly HashSet<string> _distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly List<string> _samples = new List<string>();

            private int _nullCount;
            private int _nonNullCount;
            private bool _distinctOverflow;
            private double _sum;
            private double _min = double.MaxValue;
            private double _max = double.MinValue;
            private int _numericCount;

            public FieldAccumulator(Field field, int maxDistinct, bool includeSamples)
            {
                _field = field;
                _maxDistinct = maxDistinct;
                _includeSamples = includeSamples;
            }

            public void Add(object rawValue)
            {
                if (rawValue == null || rawValue == DBNull.Value)
                {
                    _nullCount++;
                    return;
                }

                _nonNullCount++;

                var normalized = NormalizeValue(rawValue);
                if (_distinct.Count < _maxDistinct)
                {
                    if (_distinct.Add(normalized) && _includeSamples)
                    {
                        _samples.Add(normalized);
                    }
                }
                else if (!_distinct.Contains(normalized))
                {
                    _distinctOverflow = true;
                }

                if (TryGetNumericValue(rawValue, out var numeric))
                {
                    _numericCount++;
                    _sum += numeric;
                    if (numeric < _min)
                    {
                        _min = numeric;
                    }

                    if (numeric > _max)
                    {
                        _max = numeric;
                    }
                }
            }

            public object BuildProfile(int scannedRows)
            {
                var nullRate = scannedRows <= 0 ? 0d : Math.Round((double)_nullCount / scannedRows, 4);

                object numericStats = null;
                if (_numericCount > 0)
                {
                    numericStats = new
                    {
                        min = _min,
                        max = _max,
                        mean = Math.Round(_sum / _numericCount, 6),
                        count = _numericCount
                    };
                }

                return new
                {
                    fieldName = _field.Name,
                    alias = _field.AliasName,
                    fieldType = _field.FieldType.ToString(),
                    scannedRows,
                    nullCount = _nullCount,
                    nonNullCount = _nonNullCount,
                    nullRate,
                    distinctCount = _distinct.Count,
                    distinctOverflow = _distinctOverflow,
                    samples = _includeSamples ? _samples : null,
                    numericStats
                };
            }

            private static bool TryGetNumericValue(object rawValue, out double value)
            {
                value = 0;
                switch (rawValue)
                {
                    case short s:
                        value = s;
                        return true;
                    case int i:
                        value = i;
                        return true;
                    case long l:
                        value = l;
                        return true;
                    case float f:
                        value = f;
                        return true;
                    case double d:
                        value = d;
                        return true;
                    case decimal m:
                        value = (double)m;
                        return true;
                    default:
                        return false;
                }
            }

            private static string NormalizeValue(object rawValue)
            {
                switch (rawValue)
                {
                    case DateTime dt:
                        return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    default:
                        var text = Convert.ToString(rawValue, CultureInfo.InvariantCulture) ?? string.Empty;
                        if (text.Length <= 120)
                        {
                            return text;
                        }

                        return text.Substring(0, 120) + "...";
                }
            }
        }
    }
}
