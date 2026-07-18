using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 叠加求交面积汇总工具（只读，不写入数据）。
    /// </summary>
    public sealed class OverlayIntersectSummaryTool : IGISTool
    {
        public const string ToolName = "overlay_intersect_summary";

        public string Name => ToolName;

        public string Description => "对两个面图层进行叠加求交并汇总面积（只读）。";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["target_layer"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "主图层名称（面图层）"
                },
                ["overlay_layer"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "叠加图层名称（面图层）"
                },
                ["target_group_field"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "主图层分组字段，留空则统一汇总"
                },
                ["overlay_group_field"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "叠加图层分组字段，留空则统一汇总"
                },
                ["fuzzy_match"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "是否允许模糊匹配图层名"
                },
                ["max_features_per_layer"] = new JObject
                {
                    ["type"] = "integer",
                    ["default"] = 0,
                    ["minimum"] = 0,
                    ["maximum"] = 200000,
                    ["description"] = "每个图层最多参与计算的要素数量；0 表示不设上限"
                },
                ["max_result_rows"] = new JObject
                {
                    ["type"] = "integer",
                    ["default"] = 500,
                    ["minimum"] = 20,
                    ["maximum"] = 2000,
                    ["description"] = "最多返回的汇总行数"
                },
                ["area_unit"] = new JObject
                {
                    ["type"] = "string",
                    ["default"] = "square_meter",
                    ["description"] = "面积单位：square_meter/hectare/mu（支持中文：平方米/公顷/亩）"
                },
                ["decimal_places"] = new JObject
                {
                    ["type"] = "integer",
                    ["default"] = 2,
                    ["minimum"] = 0,
                    ["maximum"] = 8,
                    ["description"] = "面积保留小数位数"
                }
            },
            ["required"] = new JArray { "target_layer", "overlay_layer" }
        };

        public Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            var targetLayerName = parameters?["target_layer"]?.ToString()?.Trim();
            var overlayLayerName = parameters?["overlay_layer"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(targetLayerName) || string.IsNullOrWhiteSpace(overlayLayerName))
            {
                return Task.FromResult(ToolResult.CreateError("参数 target_layer 和 overlay_layer 不能为空"));
            }

            var targetGroupField = parameters?["target_group_field"]?.ToString()?.Trim();
            var overlayGroupField = parameters?["overlay_group_field"]?.ToString()?.Trim();
            var fuzzyMatch = parameters?["fuzzy_match"]?.ToObject<bool?>() ?? true;
            var maxFeaturesParameter = parameters?["max_features_per_layer"]?.ToObject<int?>();
            int? maxFeaturesPerLayer = null;
            if (maxFeaturesParameter.HasValue && maxFeaturesParameter.Value > 0)
            {
                maxFeaturesPerLayer = Math.Clamp(maxFeaturesParameter.Value, 100, 200000);
            }
            var maxResultRows = Math.Clamp(parameters?["max_result_rows"]?.ToObject<int?>() ?? 500, 20, 2000);
            var areaUnit = ToolSafetyHelpers.NormalizeAreaUnit(parameters?["area_unit"]?.ToString());
            var decimalPlaces = Math.Clamp(parameters?["decimal_places"]?.ToObject<int?>() ?? 2, 0, 8);

            return QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    return ToolResult.CreateError("当前没有活动地图视图");
                }

                var map = mapView.Map;
                var targetLayer = ToolSafetyHelpers.FindFeatureLayer(map, targetLayerName, fuzzyMatch);
                var overlayLayer = ToolSafetyHelpers.FindFeatureLayer(map, overlayLayerName, fuzzyMatch);

                if (targetLayer == null)
                {
                    return ToolResult.CreateError($"未找到主图层: {targetLayerName}");
                }

                if (overlayLayer == null)
                {
                    return ToolResult.CreateError($"未找到叠加图层: {overlayLayerName}");
                }

                using var targetClass = targetLayer.GetFeatureClass();
                using var overlayClass = overlayLayer.GetFeatureClass();
                if (targetClass == null || overlayClass == null)
                {
                    return ToolResult.CreateError("无法读取图层要素类");
                }

                var targetDef = targetClass.GetDefinition();
                var overlayDef = overlayClass.GetDefinition();
                if (targetDef.GetShapeType() != GeometryType.Polygon || overlayDef.GetShapeType() != GeometryType.Polygon)
                {
                    return ToolResult.CreateError("overlay_intersect_summary 仅支持两个面图层");
                }

                if (!string.IsNullOrWhiteSpace(targetGroupField) && !HasField(targetDef, targetGroupField))
                {
                    return ToolResult.CreateError($"主图层不存在字段: {targetGroupField}");
                }

                if (!string.IsNullOrWhiteSpace(overlayGroupField) && !HasField(overlayDef, overlayGroupField))
                {
                    return ToolResult.CreateError($"叠加图层不存在字段: {overlayGroupField}");
                }

                var warnings = new List<string>();
                var overlayRead = ReadPolygonFeatures(overlayClass, overlayDef, overlayGroupField, maxFeaturesPerLayer, null);
                if (overlayRead.Items.Count == 0)
                {
                    return ToolResult.CreateError($"叠加图层 {overlayLayer.Name} 没有可计算的面要素");
                }

                var overlayItemsForCalculation = overlayRead.Items;
                var targetSpatialReference = targetDef.GetSpatialReference();
                var overlaySpatialReference = overlayDef.GetSpatialReference();
                if (!ToolSafetyHelpers.IsSameSpatialReference(targetSpatialReference, overlaySpatialReference))
                {
                    if (targetSpatialReference == null)
                    {
                        return ToolResult.CreateError("主图层坐标系未知，无法与叠加图层进行坐标统一计算。");
                    }

                    var projectedItems = new List<PolygonFeatureItem>(overlayRead.Items.Count);
                    foreach (var overlayItem in overlayRead.Items)
                    {
                        try
                        {
                            var projected = GeometryEngine.Instance.Project(overlayItem.Geometry, targetSpatialReference) as Polygon;
                            if (projected == null || projected.IsEmpty)
                            {
                                continue;
                            }

                            projectedItems.Add(new PolygonFeatureItem
                            {
                                Group = overlayItem.Group,
                                Geometry = projected
                            });
                        }
                        catch (Exception ex)
                        {
                            return ToolResult.CreateError($"叠加图层坐标系与主图层不一致，投影失败: {ex.Message}");
                        }
                    }

                    overlayItemsForCalculation = projectedItems;
                    warnings.Add("检测到图层坐标系不一致，已先投影到主图层坐标系后再计算。");
                }

                if (overlayItemsForCalculation.Count == 0)
                {
                    return ToolResult.CreateError("叠加图层在投影后无可计算要素，请检查数据坐标系和几何有效性。");
                }

                var overlayExtent = BuildCombinedExtent(overlayItemsForCalculation);
                if (overlayExtent == null || overlayExtent.IsEmpty)
                {
                    return ToolResult.CreateError("无法从叠加图层构建有效范围，请检查叠加图层几何。");
                }

                var targetRead = ReadPolygonFeatures(targetClass, targetDef, targetGroupField, maxFeaturesPerLayer, overlayExtent);
                if (targetRead.Items.Count == 0)
                {
                    var emptyResult = new
                    {
                        mapName = map.Name,
                        targetLayer = targetLayer.Name,
                        overlayLayer = overlayLayer.Name,
                        targetGroupField = string.IsNullOrWhiteSpace(targetGroupField) ? null : targetGroupField,
                        overlayGroupField = string.IsNullOrWhiteSpace(overlayGroupField) ? null : overlayGroupField,
                        areaUnit = areaUnit,
                        areaUnitLabel = ToolSafetyHelpers.GetAreaUnitLabel(areaUnit),
                        decimalPlaces,
                        stats = new
                        {
                            testedPairs = 0,
                            intersectedPairs = 0,
                            targetFeatureCount = 0,
                            overlayFeatureCount = overlayRead.Items.Count,
                            summaryGroupCount = 0,
                            totalArea = 0d,
                            totalAreaSquareMeters = 0d
                        },
                        rows = Array.Empty<object>(),
                        warnings = new[] { "叠加范围内未检索到主图层候选要素，本次结果为空。" }
                    };

                    return ToolResult.CreateSuccess("叠加汇总完成，返回 0 条记录", emptyResult);
                }

                var summary = new Dictionary<string, SummaryBucket>(StringComparer.OrdinalIgnoreCase);
                var testedPairs = 0;
                var intersectedPairs = 0;

                foreach (var targetItem in targetRead.Items)
                {
                    foreach (var overlayItem in overlayItemsForCalculation)
                    {
                        testedPairs++;

                        if (!GeometryEngine.Instance.Intersects(targetItem.Geometry.Extent, overlayItem.Geometry.Extent))
                        {
                            continue;
                        }

                        if (!GeometryEngine.Instance.Intersects(targetItem.Geometry, overlayItem.Geometry))
                        {
                            continue;
                        }

                        Polygon intersected;
                        try
                        {
                            intersected = GeometryEngine.Instance.Intersection(targetItem.Geometry, overlayItem.Geometry) as Polygon;
                        }
                        catch
                        {
                            continue;
                        }

                        if (intersected == null || intersected.IsEmpty)
                        {
                            continue;
                        }

                        var areaSquareMeters = CalculateAreaSquareMeters(intersected);
                        if (areaSquareMeters <= 0)
                        {
                            continue;
                        }

                        intersectedPairs++;
                        var bucketKey = targetItem.Group + "\u001F" + overlayItem.Group;
                        if (!summary.TryGetValue(bucketKey, out var bucket))
                        {
                            bucket = new SummaryBucket
                            {
                                TargetGroup = targetItem.Group,
                                OverlayGroup = overlayItem.Group
                            };
                            summary[bucketKey] = bucket;
                        }

                        bucket.AreaSquareMeters += areaSquareMeters;
                        bucket.IntersectionCount += 1;
                    }
                }

                var orderedBuckets = summary.Values
                    .OrderByDescending(item => item.AreaSquareMeters)
                    .ToList();

                var rowCount = Math.Min(maxResultRows, orderedBuckets.Count);
                var rows = orderedBuckets
                    .Take(maxResultRows)
                    .Select(item => new
                    {
                        target_group = item.TargetGroup,
                        overlay_group = item.OverlayGroup,
                        intersection_count = item.IntersectionCount,
                        area = Math.Round(ToolSafetyHelpers.ConvertAreaFromSquareMeters(item.AreaSquareMeters, areaUnit), decimalPlaces),
                        area_square_meters = Math.Round(item.AreaSquareMeters, 4)
                    })
                    .ToList();

                var totalAreaSquareMeters = orderedBuckets.Sum(item => item.AreaSquareMeters);
                if (targetRead.Truncated)
                {
                    warnings.Add($"主图层在叠加范围内已按上限 {maxFeaturesPerLayer} 截断");
                }

                if (overlayRead.Truncated)
                {
                    warnings.Add($"叠加图层已按上限 {maxFeaturesPerLayer} 截断");
                }

                if (orderedBuckets.Count > rowCount)
                {
                    warnings.Add($"结果行数超过上限，仅返回前 {rowCount} 行");
                }

                if (intersectedPairs == 0 && (targetRead.Truncated || overlayRead.Truncated))
                {
                    warnings.Add("当前结果为 0，且存在截断，建议提高 max_features_per_layer 后复算以排除漏检。");
                }

                var result = new
                {
                    mapName = map.Name,
                    targetLayer = targetLayer.Name,
                    overlayLayer = overlayLayer.Name,
                    targetGroupField = string.IsNullOrWhiteSpace(targetGroupField) ? null : targetGroupField,
                    overlayGroupField = string.IsNullOrWhiteSpace(overlayGroupField) ? null : overlayGroupField,
                    areaUnit = areaUnit,
                    areaUnitLabel = ToolSafetyHelpers.GetAreaUnitLabel(areaUnit),
                    decimalPlaces,
                    stats = new
                    {
                        testedPairs,
                        intersectedPairs,
                        targetFeatureCount = targetRead.Items.Count,
                        overlayFeatureCount = overlayRead.Items.Count,
                        summaryGroupCount = orderedBuckets.Count,
                        totalArea = Math.Round(ToolSafetyHelpers.ConvertAreaFromSquareMeters(totalAreaSquareMeters, areaUnit), decimalPlaces),
                        totalAreaSquareMeters = Math.Round(totalAreaSquareMeters, 4)
                    },
                    rows,
                    warnings
                };

                return ToolResult.CreateSuccess($"叠加汇总完成，返回 {rowCount} 条记录", result);
            });
        }

        private static bool HasField(FeatureClassDefinition definition, string fieldName)
        {
            return definition.GetFields().Any(f =>
                f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        }

        private static ReadResult ReadPolygonFeatures(
            FeatureClass featureClass,
            FeatureClassDefinition definition,
            string groupField,
            int? maxFeatures,
            Geometry filterGeometry)
        {
            var items = new List<PolygonFeatureItem>();
            var shapeField = definition.GetShapeField();
            var subFields = string.IsNullOrWhiteSpace(groupField)
                ? shapeField
                : shapeField + "," + groupField;

            QueryFilter filter;
            if (filterGeometry != null)
            {
                filter = new SpatialQueryFilter
                {
                    WhereClause = "1=1",
                    SubFields = subFields,
                    FilterGeometry = filterGeometry,
                    SpatialRelationship = SpatialRelationship.Intersects
                };
            }
            else
            {
                filter = new QueryFilter
                {
                    WhereClause = "1=1",
                    SubFields = subFields
                };
            }

            var truncated = false;
            using var cursor = featureClass.Search(filter, false);
            while (cursor.MoveNext())
            {
                using var feature = cursor.Current as Feature;
                if (feature?.GetShape() is not Polygon polygon || polygon.IsEmpty)
                {
                    continue;
                }

                var groupValue = string.IsNullOrWhiteSpace(groupField)
                    ? "全部"
                    : ToolSafetyHelpers.NormalizeGroupValue(feature[groupField]);

                var cloned = polygon.Clone() as Polygon;
                if (cloned == null || cloned.IsEmpty)
                {
                    continue;
                }

                items.Add(new PolygonFeatureItem
                {
                    Group = groupValue,
                    Geometry = cloned
                });

                if (maxFeatures.HasValue && items.Count >= maxFeatures.Value)
                {
                    truncated = true;
                    break;
                }
            }

            return new ReadResult
            {
                Items = items,
                Truncated = truncated
            };
        }

        private static double CalculateAreaSquareMeters(Geometry geometry)
        {
            try
            {
                return Math.Abs(GeometryEngine.Instance.GeodesicArea(geometry));
            }
            catch
            {
                return Math.Abs(GeometryEngine.Instance.Area(geometry));
            }
        }

        private static Envelope BuildCombinedExtent(IEnumerable<PolygonFeatureItem> items)
        {
            double xmin = double.MaxValue;
            double ymin = double.MaxValue;
            double xmax = double.MinValue;
            double ymax = double.MinValue;
            SpatialReference spatialReference = null;
            var found = false;

            foreach (var item in items)
            {
                var extent = item?.Geometry?.Extent;
                if (extent == null || extent.IsEmpty)
                {
                    continue;
                }

                xmin = Math.Min(xmin, extent.XMin);
                ymin = Math.Min(ymin, extent.YMin);
                xmax = Math.Max(xmax, extent.XMax);
                ymax = Math.Max(ymax, extent.YMax);
                spatialReference ??= extent.SpatialReference;
                found = true;
            }

            if (!found)
            {
                return null;
            }

            return EnvelopeBuilderEx.CreateEnvelope(xmin, ymin, xmax, ymax, spatialReference);
        }

        private sealed class PolygonFeatureItem
        {
            public string Group { get; set; }
            public Polygon Geometry { get; set; }
        }

        private sealed class ReadResult
        {
            public List<PolygonFeatureItem> Items { get; set; } = new List<PolygonFeatureItem>();
            public bool Truncated { get; set; }
        }

        private sealed class SummaryBucket
        {
            public string TargetGroup { get; set; }
            public string OverlayGroup { get; set; }
            public double AreaSquareMeters { get; set; }
            public int IntersectionCount { get; set; }
        }
    }
}
