using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using XIAOFUTools.Features.User.AIAssistant.Agent;
using XIAOFUTools.Features.User.AIAssistant.Agent.Tools;

namespace XIAOFUTools.Features.User.AIAssistant.Agent.Tooling
{
    /// <summary>
    /// 解析用户消息中的工具意图，避免核心编排类承担字符串解析职责。
    /// </summary>
    internal static class ToolRequestResolver
    {
        public static ToolExecutionRequest Resolve(string userMessage, string selectedTool, string webFetchToolName)
        {
            if (!string.IsNullOrWhiteSpace(selectedTool))
            {
                var normalizedName = selectedTool.Trim().ToLowerInvariant();
                if (string.Equals(normalizedName, webFetchToolName, StringComparison.OrdinalIgnoreCase))
                {
                    var fetchParameters = BuildFetchParameters(userMessage);
                    if (fetchParameters != null)
                    {
                        return new ToolExecutionRequest
                        {
                            ToolName = webFetchToolName,
                            Query = fetchParameters["url"]?.ToString(),
                            Parameters = fetchParameters
                        };
                    }
                }
            }

            var trimmed = (userMessage ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            if (trimmed.StartsWith("/联网", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/search", StringComparison.OrdinalIgnoreCase))
            {
                var query = trimmed
                    .Replace("/联网", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("/search", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .TrimStart(':', '：', ' ')
                    .Trim();

                if (!string.IsNullOrWhiteSpace(query))
                {
                    var directUrl = ExtractFirstUrl(query);
                    if (!string.IsNullOrWhiteSpace(directUrl))
                    {
                        var fetchParameters = BuildFetchParameters(query, directUrl);
                        return new ToolExecutionRequest
                        {
                            ToolName = webFetchToolName,
                            Query = directUrl,
                            Parameters = fetchParameters
                        };
                    }

                    return new ToolExecutionRequest
                    {
                        ToolName = null,
                        Query = query,
                        Parameters = null
                    };
                }
            }

            if (trimmed.StartsWith("/抓取", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/fetch", StringComparison.OrdinalIgnoreCase))
            {
                var directText = trimmed
                    .Replace("/抓取", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("/fetch", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .TrimStart(':', '：', ' ')
                    .Trim();

                var url = ExtractFirstUrl(directText);
                var fetchParameters = BuildFetchParameters(directText, url);
                if (fetchParameters != null)
                {
                    return new ToolExecutionRequest
                    {
                        ToolName = webFetchToolName,
                        Query = fetchParameters["url"]?.ToString(),
                        Parameters = fetchParameters
                    };
                }
            }

            if (trimmed.StartsWith("/缓冲", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/buffer", StringComparison.OrdinalIgnoreCase))
            {
                var raw = trimmed
                    .Replace("/缓冲", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("/buffer", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .TrimStart(':', '：', ' ')
                    .Trim();

                var bufferParameters = BuildBufferParameters(raw);
                if (bufferParameters != null)
                {
                    return new ToolExecutionRequest
                    {
                        ToolName = LayerBufferTool.ToolName,
                        Query = raw,
                        Parameters = bufferParameters
                    };
                }
            }

            if (trimmed.StartsWith("/裁剪", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/clip", StringComparison.OrdinalIgnoreCase))
            {
                var raw = trimmed
                    .Replace("/裁剪", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("/clip", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .TrimStart(':', '：', ' ')
                    .Trim();

                var clipParameters = BuildClipParameters(raw);
                if (clipParameters != null)
                {
                    return new ToolExecutionRequest
                    {
                        ToolName = LayerClipTool.ToolName,
                        Query = raw,
                        Parameters = clipParameters
                    };
                }
            }

            if (trimmed.StartsWith("/叠加", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/intersect", StringComparison.OrdinalIgnoreCase))
            {
                var raw = trimmed
                    .Replace("/叠加", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("/intersect", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .TrimStart(':', '：', ' ')
                    .Trim();

                var overlayParameters = BuildOverlayParameters(raw);
                if (overlayParameters != null)
                {
                    return new ToolExecutionRequest
                    {
                        ToolName = OverlayIntersectSummaryTool.ToolName,
                        Query = raw,
                        Parameters = overlayParameters
                    };
                }
            }

            var autoUrl = ExtractFirstUrl(trimmed);
            if (!string.IsNullOrWhiteSpace(autoUrl))
            {
                var looksLikeFetchIntent = trimmed.Equals(autoUrl, StringComparison.OrdinalIgnoreCase)
                                           || trimmed.Contains("抓取", StringComparison.OrdinalIgnoreCase)
                                           || trimmed.Contains("fetch", StringComparison.OrdinalIgnoreCase)
                                           || trimmed.Contains("网页", StringComparison.OrdinalIgnoreCase)
                                           || trimmed.Contains("链接", StringComparison.OrdinalIgnoreCase)
                                           || trimmed.Contains("读取", StringComparison.OrdinalIgnoreCase)
                                           || trimmed.Contains("总结", StringComparison.OrdinalIgnoreCase)
                                           || trimmed.Contains("提炼", StringComparison.OrdinalIgnoreCase)
                                           || trimmed.Contains("概括", StringComparison.OrdinalIgnoreCase);

                if (looksLikeFetchIntent)
                {
                    var fetchParameters = BuildFetchParameters(trimmed, autoUrl);
                    return new ToolExecutionRequest
                    {
                        ToolName = webFetchToolName,
                        Query = autoUrl,
                        Parameters = fetchParameters
                    };
                }
            }

            return null;
        }

        private static string ExtractFirstUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = Regex.Match(text, "https?://[^\\s\\]\\)\\\"'<>]+", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            return match.Value.TrimEnd('.', ',', ';', '。', '，', '；');
        }

        private static JObject BuildFetchParameters(string text, string fallbackUrl = null)
        {
            var url = ExtractFirstUrl(text) ?? fallbackUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var startIndex = TryMatchInt(text, "(?:start|from|offset|start_index)\\s*[:=]\\s*(\\d+)") ?? 0;
            startIndex = Math.Max(0, startIndex);

            var maxLength = TryMatchInt(text, "(?:len|length|max|max_length|size)\\s*[:=]\\s*(\\d+)") ?? 5000;
            maxLength = Math.Clamp(maxLength, 200, 50000);

            var raw = Regex.IsMatch(text ?? string.Empty, "(?:^|\\s)raw(?:\\s*[:=]\\s*(?:1|true|yes))?(?:\\s|$)", RegexOptions.IgnoreCase);

            return new JObject
            {
                ["url"] = url,
                ["max_length"] = maxLength,
                ["start_index"] = startIndex,
                ["raw"] = raw
            };
        }

        private static int? TryMatchInt(string text, string pattern)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                return null;
            }

            if (!int.TryParse(match.Groups[1].Value, out var value))
            {
                return null;
            }

            return value;
        }

        private static JObject BuildBufferParameters(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var kv = ParseKeyValueArguments(text);
            var layerName = GetFirstValue(kv, "layer", "input", "layer_name");
            var outputName = GetFirstValue(kv, "output", "output_name", "name");
            var dissolve = GetFirstValue(kv, "dissolve", "dissolve_option");
            var unit = GetFirstValue(kv, "unit", "distance_unit");

            var distanceText = GetFirstValue(kv, "distance", "dist");
            var distance = TryParseDouble(distanceText);
            if (distance == null)
            {
                var match = Regex.Match(text, "(?<value>\\d+(?:\\.\\d+)?)\\s*(?<unit>米|千米|公里|m|km|meters?|kilometers?|feet|ft|miles?)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    distance = TryParseDouble(match.Groups["value"].Value);
                    if (string.IsNullOrWhiteSpace(unit))
                    {
                        unit = match.Groups["unit"].Value;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(layerName) || distance == null || distance <= 0)
            {
                var positional = SplitArguments(text);
                if (string.IsNullOrWhiteSpace(layerName) && positional.Count > 0)
                {
                    layerName = positional[0];
                }

                if ((distance == null || distance <= 0) && positional.Count > 1)
                {
                    distance = TryParseDouble(positional[1]);
                }

                if (string.IsNullOrWhiteSpace(unit) && positional.Count > 2)
                {
                    unit = positional[2];
                }
            }

            if (string.IsNullOrWhiteSpace(layerName) || distance == null || distance <= 0)
            {
                return null;
            }

            var useGeodesic = ParseBool(GetFirstValue(kv, "geodesic", "use_geodesic"));
            return new JObject
            {
                ["layer_name"] = layerName,
                ["distance"] = distance.Value,
                ["distance_unit"] = string.IsNullOrWhiteSpace(unit) ? "Meters" : unit,
                ["dissolve_option"] = string.IsNullOrWhiteSpace(dissolve) ? "NONE" : dissolve,
                ["use_geodesic"] = useGeodesic,
                ["output_name"] = outputName
            };
        }

        private static JObject BuildClipParameters(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var kv = ParseKeyValueArguments(text);
            var input = GetFirstValue(kv, "input", "layer", "input_layer");
            var clip = GetFirstValue(kv, "clip", "mask", "clip_layer");
            var output = GetFirstValue(kv, "output", "output_name", "name");

            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(clip))
            {
                var positional = SplitArguments(text);
                if (string.IsNullOrWhiteSpace(input) && positional.Count > 0)
                {
                    input = positional[0];
                }

                if (string.IsNullOrWhiteSpace(clip) && positional.Count > 1)
                {
                    clip = positional[1];
                }
            }

            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(clip))
            {
                return null;
            }

            return new JObject
            {
                ["input_layer"] = input,
                ["clip_layer"] = clip,
                ["output_name"] = output
            };
        }

        private static JObject BuildOverlayParameters(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var kv = ParseKeyValueArguments(text);
            var target = GetFirstValue(kv, "target", "main", "target_layer");
            var overlay = GetFirstValue(kv, "overlay", "other", "overlay_layer");
            var targetField = GetFirstValue(kv, "target_field", "main_field", "group1");
            var overlayField = GetFirstValue(kv, "overlay_field", "group2");
            var areaUnit = GetFirstValue(kv, "unit", "area_unit");
            var decimalPlaces = TryParseInt(GetFirstValue(kv, "decimal", "decimal_places"));

            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(overlay))
            {
                var positional = SplitArguments(text);
                if (string.IsNullOrWhiteSpace(target) && positional.Count > 0)
                {
                    target = positional[0];
                }

                if (string.IsNullOrWhiteSpace(overlay) && positional.Count > 1)
                {
                    overlay = positional[1];
                }
            }

            if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(overlay))
            {
                return null;
            }

            var parameters = new JObject
            {
                ["target_layer"] = target,
                ["overlay_layer"] = overlay
            };

            if (!string.IsNullOrWhiteSpace(targetField))
            {
                parameters["target_group_field"] = targetField;
            }

            if (!string.IsNullOrWhiteSpace(overlayField))
            {
                parameters["overlay_group_field"] = overlayField;
            }

            if (!string.IsNullOrWhiteSpace(areaUnit))
            {
                parameters["area_unit"] = areaUnit;
            }

            if (decimalPlaces != null)
            {
                parameters["decimal_places"] = decimalPlaces.Value;
            }

            return parameters;
        }

        private static Dictionary<string, string> ParseKeyValueArguments(string text)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(text))
            {
                return dict;
            }

            var matches = Regex.Matches(text, "(?<key>[a-zA-Z_\\u4e00-\\u9fa5]+)\\s*[:=]\\s*(?<value>\"[^\"]+\"|'[^']+'|[^\\s]+)");
            foreach (Match match in matches)
            {
                var key = match.Groups["key"].Value?.Trim();
                var value = match.Groups["value"].Value?.Trim().Trim('"', '\'');
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                dict[key] = value;
            }

            return dict;
        }

        private static string GetFirstValue(Dictionary<string, string> kv, params string[] keys)
        {
            if (kv == null || keys == null)
            {
                return null;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (kv.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private static List<string> SplitArguments(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return Regex.Matches(text, "\"[^\"]+\"|'[^']+'|[^\\s]+")
                .Cast<Match>()
                .Select(m => m.Value.Trim().Trim('"', '\''))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .ToList();
        }

        private static bool ParseBool(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalized = text.Trim().ToLowerInvariant();
            return normalized is "1" or "true" or "yes" or "y" or "是" or "开启" or "开";
        }

        private static double? TryParseDouble(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return value;
            }

            return null;
        }

        private static int? TryParseInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            return int.TryParse(text.Trim(), out var value) ? value : null;
        }
    }
}
