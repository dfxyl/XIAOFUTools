using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent.Tools
{
    /// <summary>
    /// GIS工具示例 - 展示如何创建和注册GIS工具
    /// </summary>
    public class ExampleGISTool : IGISTool
    {
        public string Name => "example_tool";

        public string Description => "这是一个GIS工具示例,演示如何创建自定义工具";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["parameter1"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "第一个参数的描述"
                },
                ["parameter2"] = new JObject
                {
                    ["type"] = "number",
                    ["description"] = "第二个参数的描述"
                }
            },
            ["required"] = new JArray { "parameter1" }
        };

        public async Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            try
            {
                // 获取参数
                var param1 = parameters["parameter1"]?.ToString();
                var param2 = parameters["parameter2"]?.ToObject<double?>() ?? 0;

                // 执行工具逻辑
                await Task.Delay(100); // 模拟异步操作

                // 返回结果
                return ToolResult.CreateSuccess(
                    $"工具执行成功: param1={param1}, param2={param2}",
                    new { param1, param2 });
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"工具执行失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 获取地图图层列表工具示例
    /// </summary>
    public class GetMapLayersTool : IGISTool
    {
        public string Name => "get_map_layers";

        public string Description => "获取当前地图中的所有图层列表";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["include_sublayers"] = new JObject
                {
                    ["type"] = "boolean",
                    ["description"] = "是否包含子图层",
                    ["default"] = true
                }
            }
        };

        public async Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            try
            {
                // TODO: 调用ArcGIS Pro API获取图层列表
                // var map = MapView.Active?.Map;
                // var layers = map?.GetLayersAsFlattenedList();

                await Task.CompletedTask;

                // 暂时返回示例数据
                return ToolResult.CreateSuccess(
                    "获取图层列表成功",
                    new
                    {
                        layers = new[]
                        {
                            new { name = "图层1", type = "FeatureLayer" },
                            new { name = "图层2", type = "RasterLayer" }
                        }
                    });
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"获取图层列表失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 缓冲区分析工具示例
    /// </summary>
    public class BufferAnalysisTool : IGISTool
    {
        public string Name => "buffer_analysis";

        public string Description => "对选中的要素执行缓冲区分析";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["distance"] = new JObject
                {
                    ["type"] = "number",
                    ["description"] = "缓冲区距离(米)"
                },
                ["layer_name"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "目标图层名称"
                }
            },
            ["required"] = new JArray { "distance", "layer_name" }
        };

        public async Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            try
            {
                var distance = parameters["distance"]?.ToObject<double>() ?? 0;
                var layerName = parameters["layer_name"]?.ToString();

                // TODO: 实现缓冲区分析
                // 1. 获取目标图层
                // 2. 获取选中要素
                // 3. 执行缓冲区分析
                // 4. 创建结果图层

                await Task.CompletedTask;

                return ToolResult.CreateSuccess(
                    $"缓冲区分析完成: 距离={distance}米, 图层={layerName}",
                    new { distance, layerName, result_count = 0 });
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"缓冲区分析失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 坐标转换工具示例
    /// </summary>
    public class CoordinateTransformTool : IGISTool
    {
        public string Name => "coordinate_transform";

        public string Description => "转换坐标系统";

        public JObject ParametersSchema => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["source_crs"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "源坐标系统(如: EPSG:4326)"
                },
                ["target_crs"] = new JObject
                {
                    ["type"] = "string",
                    ["description"] = "目标坐标系统(如: EPSG:3857)"
                },
                ["x"] = new JObject
                {
                    ["type"] = "number",
                    ["description"] = "X坐标或经度"
                },
                ["y"] = new JObject
                {
                    ["type"] = "number",
                    ["description"] = "Y坐标或纬度"
                }
            },
            ["required"] = new JArray { "source_crs", "target_crs", "x", "y" }
        };

        public async Task<ToolResult> ExecuteAsync(JObject parameters)
        {
            try
            {
                var sourceCrs = parameters["source_crs"]?.ToString();
                var targetCrs = parameters["target_crs"]?.ToString();
                var x = parameters["x"]?.ToObject<double>() ?? 0;
                var y = parameters["y"]?.ToObject<double>() ?? 0;

                // TODO: 实现坐标转换
                // 使用ArcGIS Pro的投影转换API

                await Task.CompletedTask;

                return ToolResult.CreateSuccess(
                    "坐标转换完成",
                    new
                    {
                        source = new { crs = sourceCrs, x, y },
                        target = new { crs = targetCrs, x = x + 0.001, y = y + 0.001 } // 示例结果
                    });
            }
            catch (Exception ex)
            {
                return ToolResult.CreateError($"坐标转换失败: {ex.Message}");
            }
        }
    }
}
