using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Services;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Tooling
{
    internal static class GisToolDefinitionBuilder
    {
        internal static List<AIToolDefinition> Build(
            IReadOnlyDictionary<string, IGISTool> tools,
            string preferredToolName,
            string mode,
            Func<string, bool> isAvailable)
        {
            ArgumentNullException.ThrowIfNull(tools);
            ArgumentNullException.ThrowIfNull(isAvailable);
            if (tools.Count == 0)
            {
                return new List<AIToolDefinition>();
            }

            IEnumerable<IGISTool> selectedTools;
            if (!string.IsNullOrWhiteSpace(preferredToolName) &&
                tools.TryGetValue(preferredToolName, out var preferredTool))
            {
                selectedTools = isAvailable(preferredToolName)
                    ? new[] { preferredTool }
                    : Enumerable.Empty<IGISTool>();
            }
            else if (!string.Equals(NormalizeMode(mode), "agent", StringComparison.Ordinal))
            {
                selectedTools = Enumerable.Empty<IGISTool>();
            }
            else
            {
                selectedTools = tools.Values.Where(tool => isAvailable(tool.Name));
            }

            return selectedTools
                .Select(tool => new AIToolDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    ParametersSchema = tool.ParametersSchema
                })
                .ToList();
        }

        internal static object BuildChoice(string preferredToolName)
        {
            if (string.IsNullOrWhiteSpace(preferredToolName))
            {
                return "auto";
            }

            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = preferredToolName
                }
            };
        }

        private static string NormalizeMode(string mode)
        {
            return string.Equals(mode, "agent", StringComparison.OrdinalIgnoreCase)
                ? "agent"
                : "chat";
        }
    }
}
