using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Tools.User.AIAssistant.Agent.Tools
{
    internal static class ToolSafetyHelpers
    {
        public static FeatureLayer FindFeatureLayer(Map map, string layerName, bool fuzzyMatch)
        {
            if (map == null || string.IsNullOrWhiteSpace(layerName))
            {
                return null;
            }

            var allLayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
            var exact = allLayers.FirstOrDefault(l =>
                l.Name.Equals(layerName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                return exact;
            }

            if (!fuzzyMatch)
            {
                return null;
            }

            return allLayers.FirstOrDefault(l =>
                l.Name.IndexOf(layerName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static string NormalizeGroupValue(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "(空值)";
            }

            var text = value.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(text) ? "(空值)" : text;
        }

        public static string NormalizeAreaUnit(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return "square_meter";
            }

            var normalized = unit.Trim().ToLowerInvariant();
            return normalized switch
            {
                "平方米" => "square_meter",
                "m2" => "square_meter",
                "sq_m" => "square_meter",
                "square_meters" => "square_meter",
                "hectare" => "hectare",
                "hectares" => "hectare",
                "公顷" => "hectare",
                "ha" => "hectare",
                "亩" => "mu",
                _ => normalized
            };
        }

        public static string GetAreaUnitLabel(string normalizedAreaUnit)
        {
            return normalizedAreaUnit switch
            {
                "hectare" => "公顷",
                "mu" => "亩",
                _ => "平方米"
            };
        }

        public static double ConvertAreaFromSquareMeters(double areaSquareMeters, string normalizedAreaUnit)
        {
            return normalizedAreaUnit switch
            {
                "hectare" => areaSquareMeters / 10000d,
                "mu" => areaSquareMeters / 666.6666666667d,
                _ => areaSquareMeters
            };
        }

        public static string NormalizeDistanceUnit(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit))
            {
                return "Meters";
            }

            var normalized = unit.Trim().ToLowerInvariant();
            return normalized switch
            {
                "米" => "Meters",
                "m" => "Meters",
                "meter" => "Meters",
                "meters" => "Meters",
                "千米" => "Kilometers",
                "公里" => "Kilometers",
                "km" => "Kilometers",
                "kilometer" => "Kilometers",
                "kilometers" => "Kilometers",
                "英尺" => "Feet",
                "ft" => "Feet",
                "foot" => "Feet",
                "feet" => "Feet",
                "英里" => "Miles",
                "mile" => "Miles",
                "miles" => "Miles",
                _ => "Meters"
            };
        }

        public static string BuildOutputFeatureClassPath(string prefix, string requestedOutputName, out string outputName)
        {
            var project = Project.Current;
            var gdbPath = project?.DefaultGeodatabasePath;
            if (string.IsNullOrWhiteSpace(gdbPath))
            {
                throw new InvalidOperationException("当前工程默认地理数据库不可用，无法创建输出数据。");
            }

            var safePrefix = SanitizeName(string.IsNullOrWhiteSpace(prefix) ? "ai_output" : prefix);
            var safeRequestedName = SanitizeName(requestedOutputName);
            var baseName = string.IsNullOrWhiteSpace(safeRequestedName) ? safePrefix : safeRequestedName;
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            outputName = SanitizeName($"{baseName}_{timestamp}");
            if (outputName.Length > 120)
            {
                outputName = outputName.Substring(0, 120);
            }

            return Path.Combine(gdbPath, outputName);
        }

        public static bool IsSameSpatialReference(ArcGIS.Core.Geometry.SpatialReference first, ArcGIS.Core.Geometry.SpatialReference second)
        {
            if (first == null && second == null)
            {
                return true;
            }

            if (first == null || second == null)
            {
                return false;
            }

            if (first.Wkid > 0 && second.Wkid > 0)
            {
                return first.Wkid == second.Wkid && first.VcsWkid == second.VcsWkid;
            }

            return string.Equals(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<bool> EnsureOutputLayerVisibleAsync(string outputPath, string outputName, string preferredMapName = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return false;
            }

            try
            {
                return await QueuedTask.Run(() =>
                {
                    var map = ResolveTargetMap(preferredMapName);
                    if (map == null)
                    {
                        return false;
                    }

                    var exists = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().Any(layer =>
                        layer.Name.Equals(outputName, StringComparison.OrdinalIgnoreCase));
                    if (exists)
                    {
                        return true;
                    }

                    var outputUri = new Uri(outputPath, UriKind.Absolute);
                    LayerFactory.Instance.CreateLayer(outputUri, map, layerName: outputName);
                    return true;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ToolSafetyHelpers] 添加输出到地图失败: {ex.Message}");
                return false;
            }
        }

        private static Map ResolveTargetMap(string preferredMapName)
        {
            var activeMap = MapView.Active?.Map;
            if (activeMap != null)
            {
                return activeMap;
            }

            var project = Project.Current;
            if (project == null)
            {
                return null;
            }

            var maps = project.GetItems<MapProjectItem>()
                .Select(item => item.GetMap())
                .Where(map => map != null)
                .ToList();

            if (!string.IsNullOrWhiteSpace(preferredMapName))
            {
                var namedMap = maps.FirstOrDefault(map =>
                    map.Name.Equals(preferredMapName, StringComparison.OrdinalIgnoreCase));
                if (namedMap != null)
                {
                    return namedMap;
                }
            }

            return maps.FirstOrDefault();
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var sanitized = Regex.Replace(name.Trim(), "[^a-zA-Z0-9_]", "_");
            sanitized = Regex.Replace(sanitized, "_+", "_");
            sanitized = sanitized.Trim('_');

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return string.Empty;
            }

            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "n_" + sanitized;
            }

            return sanitized;
        }
    }
}
