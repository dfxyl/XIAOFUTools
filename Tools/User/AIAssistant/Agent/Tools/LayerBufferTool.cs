using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 缓冲区分析工具（仅创建新输出，不修改输入数据）。
    /// </summary>
    public sealed class LayerBufferTool : IGISTool
    {
        public const string ToolName = "buffer_analysis";

        public string Name => ToolName;

        public string Description => "对图层执行缓冲区分析并输出新要素类（不会修改输入）。";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["layer_name"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "输入图层名称"
                },
                ["distance"] = new JObject
                {
                    ["type"] = "number",
                    ["description"] = "缓冲距离（正数）"
                },
                ["distance_unit"] = new JObject
                {
                    ["type"] = "string",
                    ["default"] = "Meters",
                    ["description"] = "距离单位：Meters/Kilometers/Feet/Miles（支持中文）"
                },
                ["dissolve_option"] = new JObject
                {
                    ["type"] = "string",
                    ["default"] = "NONE",
                    ["description"] = "融合方式：NONE 或 ALL"
                },
                ["use_geodesic"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = false,
                    ["description"] = "是否使用大地线缓冲"
                },
                ["output_name"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "输出要素类名称（会自动追加时间戳，避免覆盖）"
                },
                ["fuzzy_match"] = new JObject
                {
                    ["type"] = "boolean",
                    ["default"] = true,
                    ["description"] = "是否允许模糊匹配图层名"
                }
            },
            ["required"] = new JArray { "layer_name", "distance" }
        };

        public async Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            var layerName = parameters?["layer_name"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return ToolResult.CreateError("参数 layer_name 不能为空");
            }

            var distance = parameters?["distance"]?.ToObject<double?>() ?? 0;
            if (distance <= 0)
            {
                return ToolResult.CreateError("参数 distance 必须为大于 0 的数值");
            }

            var distanceUnit = ToolSafetyHelpers.NormalizeDistanceUnit(parameters?["distance_unit"]?.ToString());
            var dissolveOption = (parameters?["dissolve_option"]?.ToString() ?? "NONE").Trim().ToUpperInvariant();
            dissolveOption = dissolveOption == "ALL" ? "ALL" : "NONE";
            var useGeodesic = parameters?["use_geodesic"]?.ToObject<bool?>() ?? false;
            var outputName = parameters?["output_name"]?.ToString()?.Trim();
            var fuzzyMatch = parameters?["fuzzy_match"]?.ToObject<bool?>() ?? true;

            var resolved = await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    return (Layer: (FeatureLayer)null, Error: "当前没有活动地图视图", MapName: (string)null, OutputSpatialReference: (SpatialReference)null);
                }

                var layer = ToolSafetyHelpers.FindFeatureLayer(mapView.Map, layerName, fuzzyMatch);
                if (layer == null)
                {
                    return (Layer: (FeatureLayer)null, Error: $"未找到图层: {layerName}", MapName: mapView.Map.Name, OutputSpatialReference: (SpatialReference)null);
                }

                using var featureClass = layer.GetFeatureClass();
                var outputSpatialReference = featureClass?.GetDefinition()?.GetSpatialReference();

                return (Layer: layer, Error: (string)null, MapName: mapView.Map.Name, OutputSpatialReference: outputSpatialReference);
            });

            if (!string.IsNullOrWhiteSpace(resolved.Error))
            {
                return ToolResult.CreateError(resolved.Error);
            }

            string outputPath;
            string finalOutputName;
            try
            {
                outputPath = ToolSafetyHelpers.BuildOutputFeatureClassPath("ai_buffer", outputName, out finalOutputName);
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError(ex.Message);
            }

            var distanceText = distance.ToString(CultureInfo.InvariantCulture) + " " + distanceUnit;
            var method = useGeodesic ? "GEODESIC" : "PLANAR";
            var gpParams = Geoprocessing.MakeValueArray(
                resolved.Layer,
                outputPath,
                distanceText,
                "FULL",
                "ROUND",
                dissolveOption,
                string.Empty,
                method);
            var env = resolved.OutputSpatialReference != null
                ? Geoprocessing.MakeEnvironmentArray(
                    "overwriteoutput", "False",
                    "addOutputsToMap", "True",
                    "outputCoordinateSystem", resolved.OutputSpatialReference)
                : Geoprocessing.MakeEnvironmentArray("overwriteoutput", "False", "addOutputsToMap", "True");

            var gpResult = await Geoprocessing.ExecuteToolAsync(
                "analysis.Buffer",
                gpParams,
                env,
                null,
                null,
                GPExecuteToolFlags.AddToHistory | GPExecuteToolFlags.AddOutputsToMap);
            if (gpResult == null)
            {
                return ToolResult.CreateError("缓冲区分析失败：未返回地理处理结果");
            }

            if (gpResult.IsFailed)
            {
                var errorText = string.Join(" | ", gpResult.Messages.Select(m => m.Text));
                if (string.IsNullOrWhiteSpace(errorText))
                {
                    errorText = "地理处理工具执行失败";
                }

                return ToolResult.CreateError("缓冲区分析失败: " + errorText);
            }

            var warnings = gpResult.Messages
                .Where(m => m.Type == GPMessageType.Warning)
                .Select(m => m.Text)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToList();

            var addedToMap = await ToolSafetyHelpers.EnsureOutputLayerVisibleAsync(outputPath, finalOutputName, resolved.MapName);
            if (!addedToMap)
            {
                warnings.Add("输出已生成，但未能自动添加到地图，可在目录窗口手动添加。");
            }

            var result = new
            {
                mapName = resolved.MapName,
                inputLayer = resolved.Layer?.Name,
                distance,
                distanceUnit,
                dissolveOption,
                method,
                outputFeatureClass = outputPath,
                outputName = finalOutputName,
                outputSpatialReference = resolved.OutputSpatialReference?.Name,
                outputSpatialReferenceWkid = resolved.OutputSpatialReference?.Wkid ?? 0,
                addedToMap,
                overwriteInput = false,
                deleteInput = false,
                warnings
            };

            return ToolResult.CreateSuccess($"缓冲区分析完成，输出: {finalOutputName}", result);
        }
    }
}
