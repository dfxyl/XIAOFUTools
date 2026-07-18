using System;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// 裁剪分析工具（仅创建新输出，不修改输入数据）。
    /// </summary>
    public sealed class LayerClipTool : IGISTool
    {
        public const string ToolName = "clip_analysis";

        public string Name => ToolName;

        public string Description => "按裁剪图层生成新的裁剪结果要素类（不会修改输入）。";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["input_layer"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "待裁剪输入图层名称"
                },
                ["clip_layer"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "裁剪范围图层名称（面图层）"
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
            ["required"] = new JArray { "input_layer", "clip_layer" }
        };

        public async Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            var inputLayerName = parameters?["input_layer"]?.ToString()?.Trim();
            var clipLayerName = parameters?["clip_layer"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(inputLayerName) || string.IsNullOrWhiteSpace(clipLayerName))
            {
                return ToolResult.CreateError("参数 input_layer 和 clip_layer 不能为空");
            }

            var outputName = parameters?["output_name"]?.ToString()?.Trim();
            var fuzzyMatch = parameters?["fuzzy_match"]?.ToObject<bool?>() ?? true;

            var resolved = await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    return (Input: (FeatureLayer)null, Clip: (FeatureLayer)null, Error: "当前没有活动地图视图", MapName: (string)null, OutputSpatialReference: (SpatialReference)null);
                }

                var map = mapView.Map;
                var inputLayer = ToolSafetyHelpers.FindFeatureLayer(map, inputLayerName, fuzzyMatch);
                if (inputLayer == null)
                {
                    return (Input: (FeatureLayer)null, Clip: (FeatureLayer)null, Error: $"未找到输入图层: {inputLayerName}", MapName: map.Name, OutputSpatialReference: (SpatialReference)null);
                }

                var clipLayer = ToolSafetyHelpers.FindFeatureLayer(map, clipLayerName, fuzzyMatch);
                if (clipLayer == null)
                {
                    return (Input: (FeatureLayer)null, Clip: (FeatureLayer)null, Error: $"未找到裁剪图层: {clipLayerName}", MapName: map.Name, OutputSpatialReference: (SpatialReference)null);
                }

                using var clipFeatureClass = clipLayer.GetFeatureClass();
                var clipDef = clipFeatureClass?.GetDefinition();
                if (clipDef == null || clipDef.GetShapeType() != GeometryType.Polygon)
                {
                    return (Input: (FeatureLayer)null, Clip: (FeatureLayer)null, Error: "clip_layer 必须是面图层", MapName: map.Name, OutputSpatialReference: (SpatialReference)null);
                }

                using var inputFeatureClass = inputLayer.GetFeatureClass();
                var outputSpatialReference = inputFeatureClass?.GetDefinition()?.GetSpatialReference();

                return (Input: inputLayer, Clip: clipLayer, Error: (string)null, MapName: map.Name, OutputSpatialReference: outputSpatialReference);
            });

            if (!string.IsNullOrWhiteSpace(resolved.Error))
            {
                return ToolResult.CreateError(resolved.Error);
            }

            string outputPath;
            string finalOutputName;
            try
            {
                outputPath = ToolSafetyHelpers.BuildOutputFeatureClassPath("ai_clip", outputName, out finalOutputName);
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError(ex.Message);
            }

            var gpParams = Geoprocessing.MakeValueArray(
                resolved.Input,
                resolved.Clip,
                outputPath,
                string.Empty);
            var env = resolved.OutputSpatialReference != null
                ? Geoprocessing.MakeEnvironmentArray(
                    "overwriteoutput", "False",
                    "addOutputsToMap", "True",
                    "outputCoordinateSystem", resolved.OutputSpatialReference)
                : Geoprocessing.MakeEnvironmentArray("overwriteoutput", "False", "addOutputsToMap", "True");

            var gpResult = await Geoprocessing.ExecuteToolAsync(
                "analysis.Clip",
                gpParams,
                env,
                null,
                null,
                GPExecuteToolFlags.AddToHistory | GPExecuteToolFlags.AddOutputsToMap);
            if (gpResult == null)
            {
                return ToolResult.CreateError("裁剪分析失败：未返回地理处理结果");
            }

            if (gpResult.IsFailed)
            {
                var errorText = string.Join(" | ", gpResult.Messages.Select(m => m.Text));
                if (string.IsNullOrWhiteSpace(errorText))
                {
                    errorText = "地理处理工具执行失败";
                }

                return ToolResult.CreateError("裁剪分析失败: " + errorText);
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
                inputLayer = resolved.Input?.Name,
                clipLayer = resolved.Clip?.Name,
                outputFeatureClass = outputPath,
                outputName = finalOutputName,
                outputSpatialReference = resolved.OutputSpatialReference?.Name,
                outputSpatialReferenceWkid = resolved.OutputSpatialReference?.Wkid ?? 0,
                addedToMap,
                overwriteInput = false,
                deleteInput = false,
                warnings
            };

            return ToolResult.CreateSuccess($"裁剪分析完成，输出: {finalOutputName}", result);
        }
    }
}
