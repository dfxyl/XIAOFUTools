using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Agent.Infrastructure;
using XIAOFUTools.Features.User.AIAssistant.Agent.Prompts;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tools;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tooling;
using XIAOFUTools.Features.User.AIAssistant.Database;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Features.User.AIAssistant.Agent
{
    public partial class GISAgentCore
    {

        private static async Task<ToolResult> EnsureFeatureOutputAddedToMapAsync(ToolResult result)
        {
            if (result == null || !result.Success || result.Data == null)
            {
                return result;
            }

            JObject data;
            try
            {
                data = result.Data as JObject ?? JObject.FromObject(result.Data);
            }
            catch
            {
                return result;
            }

            var outputFeatureClass = data["outputFeatureClass"]?.ToString();
            if (string.IsNullOrWhiteSpace(outputFeatureClass))
            {
                return result;
            }

            var addedToMap = data["addedToMap"]?.ToObject<bool?>() ?? false;
            if (!addedToMap)
            {
                var outputName = data["outputName"]?.ToString();
                if (string.IsNullOrWhiteSpace(outputName))
                {
                    outputName = Path.GetFileName(outputFeatureClass);
                }

                var mapName = data["mapName"]?.ToString();
                var ensureResult = await ToolSafetyHelpers.EnsureOutputLayerVisibleAsync(outputFeatureClass, outputName, mapName);
                data["addedToMap"] = ensureResult;

                if (!ensureResult)
                {
                    if (data["warnings"] is not JArray warnings)
                    {
                        warnings = new JArray();
                        data["warnings"] = warnings;
                    }

                    warnings.Add("输出已生成，但自动添加到地图失败，可在目录窗口手动添加。");
                }
            }
            else if (data["addedToMap"] == null)
            {
                data["addedToMap"] = true;
            }

            result.Data = data;
            return result;
        }

        /// <summary>
        /// 设置工作模式
        /// </summary>
        /// <param name="mode">"chat" 或 "agent"</param>
        public void SetMode(string mode)
        {
            if (mode == "chat" || mode == "agent")
            {
                _currentMode = mode;
                System.Diagnostics.Debug.WriteLine($"已切换到 {mode} 模式");
            }
        }
    }
}
