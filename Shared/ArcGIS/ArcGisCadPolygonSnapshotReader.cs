using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared.IO.Cad;

namespace XIAOFUTools.Shared.Gis
{
    /// <summary>
    /// 在 QueuedTask 中将面要素及其渲染器信息转换为可跨线程的 CAD 快照。
    /// </summary>
    internal sealed class ArcGisCadPolygonSnapshotReader
    {
        private const string CompositeKeyDelimiter = "\u001F";

        internal Task<IReadOnlyList<CadPolygonSnapshot>> ReadAsync(
            FeatureLayer featureLayer,
            CadPolygonReadOptions options)
        {
            ArgumentNullException.ThrowIfNull(featureLayer);
            ArgumentNullException.ThrowIfNull(options);
            return QueuedTask.Run(() => Read(featureLayer, options));
        }

        private static IReadOnlyList<CadPolygonSnapshot> Read(FeatureLayer featureLayer, CadPolygonReadOptions options)
        {
            var colorGetter = BuildColorGetter(featureLayer)
                ?? throw new InvalidOperationException("无法解析图层渲染符号，请检查图层符号是否为面填充。");
            var layerNameGetter = BuildLayerNameGetter(featureLayer, options);
            var results = new List<CadPolygonSnapshot>();

            using var table = featureLayer.GetTable();
            using var cursor = table.Search(null, false);
            while (cursor.MoveNext())
            {
                using var row = cursor.Current;
                if (row is not Feature feature || feature.GetShape() is not Polygon polygon)
                {
                    continue;
                }

                var rings = ExtractRings(polygon);
                if (rings.Count == 0)
                {
                    continue;
                }

                var color = colorGetter(row)
                    ?? throw new InvalidOperationException("存在要素颜色无法从符号系统解析，请检查渲染配置。");
                var layerName = layerNameGetter(row);
                if (string.IsNullOrWhiteSpace(layerName))
                {
                    layerName = $"Fill_{color.R:X2}{color.G:X2}{color.B:X2}";
                }

                results.Add(new CadPolygonSnapshot
                {
                    Rings = rings,
                    Color = color,
                    LayerName = SanitizeLayerName(layerName)
                });
            }

            return results;
        }

        private static List<List<CadCoordinate>> ExtractRings(Polygon polygon)
        {
            var rings = new List<List<CadCoordinate>>();
            foreach (var part in polygon.Parts)
            {
                var points = new List<CadCoordinate>();
                foreach (var segment in part)
                {
                    if (points.Count == 0)
                    {
                        points.Add(new CadCoordinate(segment.StartPoint.X, segment.StartPoint.Y));
                    }
                    points.Add(new CadCoordinate(segment.EndPoint.X, segment.EndPoint.Y));
                }

                if (points.Count < 4)
                {
                    continue;
                }
                if (points[0] != points[^1])
                {
                    points.Add(points[0]);
                }
                rings.Add(points);
            }
            return rings;
        }

        private static Func<Row, Color?> BuildColorGetter(FeatureLayer featureLayer)
        {
            var renderer = featureLayer.GetRenderer();
            if (renderer is CIMSimpleRenderer simpleRenderer)
            {
                var color = GetFillColor(simpleRenderer.Symbol?.Symbol as CIMPolygonSymbol);
                return _ => color;
            }

            if (renderer is CIMUniqueValueRenderer uniqueValueRenderer)
            {
                var colors = new Dictionary<string, Color?>(StringComparer.OrdinalIgnoreCase);
                var fields = (uniqueValueRenderer.Fields ?? Array.Empty<string>()).ToArray();
                foreach (var group in uniqueValueRenderer.Groups ?? Array.Empty<CIMUniqueValueGroup>())
                foreach (var itemClass in group.Classes ?? Array.Empty<CIMUniqueValueClass>())
                {
                    var color = GetFillColor(itemClass.Symbol?.Symbol as CIMPolygonSymbol);
                    foreach (var value in itemClass.Values ?? Array.Empty<CIMUniqueValue>())
                    {
                        colors[BuildCompositeKey(value?.FieldValues)] = color;
                    }
                }

                var defaultColor = uniqueValueRenderer.UseDefaultSymbol
                    ? GetFillColor(uniqueValueRenderer.DefaultSymbol?.Symbol as CIMPolygonSymbol)
                    : null;
                return row =>
                {
                    if (fields.Length == 0)
                    {
                        return defaultColor;
                    }
                    var key = BuildCompositeKeyFromRow(row, fields);
                    return colors.TryGetValue(key, out var color) ? color : defaultColor;
                };
            }

            if (renderer is CIMClassBreaksRenderer classBreaksRenderer)
            {
                var breaks = new List<(double? Minimum, double? Maximum, Color? Color)>();
                double? previousMaximum = null;
                foreach (var itemBreak in classBreaksRenderer.Breaks ?? Array.Empty<CIMClassBreak>())
                {
                    breaks.Add((previousMaximum, itemBreak.UpperBound, GetFillColor(itemBreak.Symbol?.Symbol as CIMPolygonSymbol)));
                    previousMaximum = itemBreak.UpperBound;
                }

                var field = classBreaksRenderer.Field ?? string.Empty;
                return row =>
                {
                    if (string.IsNullOrWhiteSpace(field) || !double.TryParse(GetFieldValue(row, field)?.ToString(), out var value))
                    {
                        return null;
                    }
                    foreach (var itemBreak in breaks)
                    {
                        if ((!itemBreak.Minimum.HasValue || value >= itemBreak.Minimum) &&
                            (!itemBreak.Maximum.HasValue || value < itemBreak.Maximum))
                        {
                            return itemBreak.Color;
                        }
                    }
                    return null;
                };
            }

            return null;
        }

        private static Func<Row, string> BuildLayerNameGetter(FeatureLayer featureLayer, CadPolygonReadOptions options)
        {
            var selectedFields = options.NamingFields?.Where(field => !string.IsNullOrWhiteSpace(field)).ToArray()
                ?? Array.Empty<string>();
            if (options.UseFieldNaming && selectedFields.Length > 0)
            {
                return row => SanitizeLayerName(BuildCompositeKeyFromRow(row, selectedFields, options.FieldNamingSeparator ?? string.Empty));
            }

            var renderer = featureLayer.GetRenderer();
            if (renderer is CIMUniqueValueRenderer uniqueValueRenderer)
            {
                var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var fields = (uniqueValueRenderer.Fields ?? Array.Empty<string>()).ToArray();
                foreach (var group in uniqueValueRenderer.Groups ?? Array.Empty<CIMUniqueValueGroup>())
                foreach (var itemClass in group.Classes ?? Array.Empty<CIMUniqueValueClass>())
                foreach (var value in itemClass.Values ?? Array.Empty<CIMUniqueValue>())
                {
                    var key = BuildCompositeKey(value?.FieldValues);
                    names[key] = SanitizeLayerName(string.IsNullOrWhiteSpace(itemClass.Label) ? key : itemClass.Label);
                }

                var defaultName = uniqueValueRenderer.UseDefaultSymbol ? "Default" : null;
                return row =>
                {
                    if (fields.Length == 0)
                    {
                        return defaultName;
                    }
                    return names.TryGetValue(BuildCompositeKeyFromRow(row, fields), out var name) ? name : defaultName;
                };
            }

            if (renderer is CIMClassBreaksRenderer classBreaksRenderer)
            {
                var breaks = (classBreaksRenderer.Breaks ?? Array.Empty<CIMClassBreak>())
                    .Select(itemBreak => (itemBreak.UpperBound, SanitizeLayerName(string.IsNullOrWhiteSpace(itemBreak.Label) ? $"<= {itemBreak.UpperBound}" : itemBreak.Label)))
                    .ToArray();
                var field = classBreaksRenderer.Field ?? string.Empty;
                return row =>
                {
                    if (!double.TryParse(GetFieldValue(row, field)?.ToString(), out var value))
                    {
                        return SanitizeLayerName(featureLayer.Name);
                    }
                    foreach (var itemBreak in breaks)
                    {
                        if (value < itemBreak.UpperBound)
                        {
                            return itemBreak.Item2;
                        }
                    }
                    return SanitizeLayerName(featureLayer.Name);
                };
            }

            return _ => SanitizeLayerName(featureLayer.Name);
        }

        private static object GetFieldValue(Row row, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName)) return null;
            var fields = row.GetFields();
            for (var index = 0; index < fields.Count; index++)
            {
                if (string.Equals(fields[index]?.Name, fieldName, StringComparison.OrdinalIgnoreCase)) return row[index];
            }
            return null;
        }

        private static string BuildCompositeKey(IEnumerable<object> values)
        {
            var canonicalValues = (values ?? Array.Empty<object>()).Select(Canonicalize).ToArray();
            return canonicalValues.Length == 0 ? "null" : string.Join(CompositeKeyDelimiter, canonicalValues);
        }

        private static string BuildCompositeKeyFromRow(Row row, IReadOnlyList<string> fields) =>
            BuildCompositeKeyFromRow(row, fields, CompositeKeyDelimiter);

        private static string BuildCompositeKeyFromRow(Row row, IReadOnlyList<string> fields, string delimiter) =>
            string.Join(delimiter, fields.Select(field => Canonicalize(GetFieldValue(row, field))));

        private static string Canonicalize(object value) => value switch
        {
            null => "null",
            string text => text.Trim(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? "null",
            _ => value.ToString()?.Trim() ?? "null"
        };

        private static Color? GetFillColor(CIMPolygonSymbol symbol)
        {
            foreach (var symbolLayer in symbol?.SymbolLayers ?? Array.Empty<CIMSymbolLayer>())
            {
                if (symbolLayer is not CIMSolidFill fill) continue;
                if (fill.Color is CIMRGBColor rgb)
                {
                    return Color.FromArgb((int)Math.Round(rgb.Alpha * 255.0 / 100.0), Math.Clamp((int)Math.Round(rgb.R), 0, 255), Math.Clamp((int)Math.Round(rgb.G), 0, 255), Math.Clamp((int)Math.Round(rgb.B), 0, 255));
                }
                if (fill.Color is CIMCMYKColor cmyk)
                {
                    var cyan = cmyk.C / 100.0;
                    var magenta = cmyk.M / 100.0;
                    var yellow = cmyk.Y / 100.0;
                    var black = cmyk.K / 100.0;
                    return Color.FromArgb((int)Math.Round(cmyk.Alpha * 255.0 / 100.0), Math.Clamp((int)Math.Round(255 * (1 - cyan) * (1 - black)), 0, 255), Math.Clamp((int)Math.Round(255 * (1 - magenta) * (1 - black)), 0, 255), Math.Clamp((int)Math.Round(255 * (1 - yellow) * (1 - black)), 0, 255));
                }
            }
            return null;
        }

        private static string SanitizeLayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Layer0";
            var result = name;
            foreach (var character in new[] { '<', '>', '/', '\\', ':', '"', '?', '*', '|', ',', ';', '=', '[', ']', '{', '}', '(', ')' }) result = result.Replace(character, '_');
            result = result.Replace(' ', '_');
            return result.Length > 60 ? result[..60] : result;
        }
    }
}
