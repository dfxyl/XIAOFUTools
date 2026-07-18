using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    public sealed record LandClassProjectArea(string ProjectName, double TotalArea, string PlotName = "", string GroupValue = "");

    public sealed record LandClassIntersectionArea(
        string ProjectName,
        string LandClassName,
        double Area,
        string OwnerUnitName = "",
        string OwnerNatureCode = "",
        string PlotName = "",
        string GroupValue = "");

    public enum LandClassTableColumnLevel
    {
        TopGroup,
        SecondGroup,
        Leaf
    }

    public sealed record LandClassTableColumn(
        string Key,
        string HeaderText,
        string TopGroupKey,
        string TopGroupText,
        string SecondGroupKey,
        string SecondGroupText,
        string Code,
        LandClassTableColumnLevel Level);

    public sealed class LandClassTableRow
    {
        public LandClassTableRow(
            string projectName,
            string plotName,
            string ownerUnitName,
            string ownerNatureName,
            double totalArea,
            Dictionary<string, double> values)
        {
            ProjectName = projectName;
            PlotName = plotName;
            OwnerUnitName = ownerUnitName;
            OwnerNatureName = ownerNatureName;
            TotalArea = totalArea;
            Values = values;
        }

        public string ProjectName { get; }
        public string PlotName { get; }
        public string OwnerUnitName { get; }
        public string OwnerNatureName { get; }
        public double TotalArea { get; }
        public Dictionary<string, double> Values { get; }
    }

    public sealed class LandClassTableResult
    {
        public LandClassTableResult(IReadOnlyList<LandClassTableColumn> columns, IReadOnlyList<LandClassTableRow> rows)
        {
            Columns = columns;
            Rows = rows;
        }

        public IReadOnlyList<LandClassTableColumn> Columns { get; }
        public IReadOnlyList<LandClassTableRow> Rows { get; }
    }

    public static class LandClassTableBuilder
    {
        private static readonly IReadOnlyList<LandClassDefinition> Definitions = CreateDefinitions();
        private static readonly IReadOnlyDictionary<string, LandClassDefinition> DefinitionsByKey =
            Definitions.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

        public static LandClassTableResult BuildTable(
            IEnumerable<LandClassProjectArea> projectAreas,
            IEnumerable<LandClassIntersectionArea> intersectionAreas,
            int decimalPlaces)
        {
            var projectTotals = (projectAreas ?? Enumerable.Empty<LandClassProjectArea>())
                .Where(x => !string.IsNullOrWhiteSpace(x.ProjectName))
                .GroupBy(x => new ProjectKey(x.ProjectName.Trim(), NormalizeTextValue(x.PlotName)), ProjectKeyComparer.Instance)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => Math.Max(0, x.TotalArea)),
                    ProjectKeyComparer.Instance);

            var rawByRow = new Dictionary<RowKey, Dictionary<string, double>>();
            foreach (var item in intersectionAreas ?? Enumerable.Empty<LandClassIntersectionArea>())
            {
                if (string.IsNullOrWhiteSpace(item.ProjectName) || item.Area <= 0)
                {
                    continue;
                }

                var definition = ResolveDefinition(item.LandClassName);
                if (definition == null)
                {
                    continue;
                }

                string projectName = item.ProjectName.Trim();
                string plotName = NormalizeTextValue(item.PlotName);
                string ownerUnitName = string.IsNullOrWhiteSpace(item.OwnerUnitName) ? projectName : item.OwnerUnitName.Trim();
                string ownerNatureName = ResolveOwnerNatureName(item.OwnerNatureCode);
                var rowKey = new RowKey(projectName, plotName, ownerUnitName, ownerNatureName);
                if (!rawByRow.TryGetValue(rowKey, out var values))
                {
                    values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    rawByRow[rowKey] = values;
                }

                values.TryGetValue(definition.Key, out double current);
                values[definition.Key] = current + item.Area;
            }

            var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<LandClassTableRow>();

            foreach (var item in rawByRow.OrderBy(x => x.Key.ProjectName, StringComparer.CurrentCulture)
                         .ThenBy(x => x.Key.PlotName, StringComparer.CurrentCulture)
                         .ThenBy(x => x.Key.OwnerUnitName, StringComparer.CurrentCulture)
                         .ThenBy(x => x.Key.OwnerNatureName, StringComparer.CurrentCulture))
            {
                double rawTotal = item.Value.Values.Sum();
                var projectKey = new ProjectKey(item.Key.ProjectName, item.Key.PlotName);
                double projectTotal = projectTotals.TryGetValue(projectKey, out double totalArea) ? totalArea : rawTotal;
                double rowTotal = rawByRow
                    .Where(x => x.Key.ProjectName.Equals(item.Key.ProjectName, StringComparison.OrdinalIgnoreCase) &&
                                x.Key.PlotName.Equals(item.Key.PlotName, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Value.Values.Sum());
                double targetTotal = rawTotal > 0 && rowTotal > 0 && projectTotal > 0
                    ? projectTotal * rawTotal / rowTotal
                    : rawTotal;

                var values = BuildProjectValues(item.Value, targetTotal, decimalPlaces);
                foreach (string key in values.Where(x => x.Value > 0).Select(x => x.Key))
                {
                    usedKeys.Add(key);
                    AddAncestorKeys(usedKeys, key);
                }

                rows.Add(new LandClassTableRow(
                    item.Key.ProjectName,
                    item.Key.PlotName,
                    item.Key.OwnerUnitName,
                    item.Key.OwnerNatureName,
                    Math.Round(targetTotal, decimalPlaces),
                    values));
            }

            foreach (var project in projectTotals.Where(x => !rawByRow.Keys.Any(k =>
                         k.ProjectName.Equals(x.Key.ProjectName, StringComparison.OrdinalIgnoreCase) &&
                         k.PlotName.Equals(x.Key.PlotName, StringComparison.OrdinalIgnoreCase))))
            {
                rows.Add(new LandClassTableRow(
                    project.Key.ProjectName,
                    project.Key.PlotName,
                    project.Key.ProjectName,
                    string.Empty,
                    Math.Round(project.Value, decimalPlaces),
                    new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)));
            }

            EnsureMinimumLeafColumns(usedKeys);
            EnsureDefaultAgriculturalColumns(usedKeys);
            EnsureDefaultConstructionColumns(usedKeys);
            RemoveSingleLeafSecondGroupColumns(usedKeys);

            var columns = BuildColumns(usedKeys).ToList();
            return new LandClassTableResult(columns, rows);
        }

        public static string ResolveOwnerNatureName(string ownerNatureCode)
        {
            string code = Normalize(ownerNatureCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                return string.Empty;
            }

            return code switch
            {
                "10" => "国有",
                "20" => "国有",
                "30" => "集体",
                "40" => "集体",
                _ when code.StartsWith("1", StringComparison.OrdinalIgnoreCase) => "国有",
                _ when code.StartsWith("2", StringComparison.OrdinalIgnoreCase) => "国有",
                _ when code.StartsWith("3", StringComparison.OrdinalIgnoreCase) => "集体",
                _ when code.StartsWith("4", StringComparison.OrdinalIgnoreCase) => "集体",
                _ => ownerNatureCode.Trim()
            };
        }

        public static LandClassDefinition? ResolveDefinition(string landClassText)
        {
            string normalizedText = Normalize(landClassText);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return null;
            }

            string normalizedMainCodeText = TrimLandClassCodeSuffix(normalizedText);
            if (!normalizedMainCodeText.Equals(normalizedText, StringComparison.OrdinalIgnoreCase))
            {
                var mainCodeMatch = Definitions
                    .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                    .FirstOrDefault(x => normalizedMainCodeText.Equals(NormalizeCode(x.Code), StringComparison.OrdinalIgnoreCase));
                if (mainCodeMatch != null)
                {
                    return mainCodeMatch;
                }
            }

            var exactCodeMatch = Definitions
                .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                .FirstOrDefault(x => normalizedText.Equals(NormalizeCode(x.Code), StringComparison.OrdinalIgnoreCase));
            if (exactCodeMatch != null)
            {
                return exactCodeMatch;
            }

            var codeMatch = Definitions
                .Where(x => !string.IsNullOrWhiteSpace(x.Code) && normalizedText.Contains(NormalizeCode(x.Code)))
                .OrderByDescending(x => x.Code.Length)
                .ThenByDescending(x => x.Level)
                .FirstOrDefault();
            if (codeMatch != null)
            {
                return codeMatch;
            }

            return Definitions
                .SelectMany(x => GetNameMatches(x, normalizedText))
                .OrderByDescending(x => x.IsExact)
                .ThenByDescending(x => x.NormalizedName.Length)
                .ThenByDescending(x => x.Definition.Level)
                .Select(x => x.Definition)
                .FirstOrDefault();
        }

        private static IEnumerable<(LandClassDefinition Definition, string NormalizedName, bool IsExact)> GetNameMatches(
            LandClassDefinition definition,
            string normalizedText)
        {
            foreach (string name in GetLandClassNames(definition))
            {
                string normalizedName = NormalizeName(name);
                if (string.IsNullOrWhiteSpace(normalizedName))
                {
                    continue;
                }

                if (normalizedText.Equals(normalizedName, StringComparison.OrdinalIgnoreCase) ||
                    normalizedText.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))
                {
                    yield return (definition, normalizedName, normalizedText.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
                }
            }
        }

        private static IEnumerable<string> GetLandClassNames(LandClassDefinition definition)
        {
            yield return definition.Name;
            if (definition.Code.Equals("1104", StringComparison.OrdinalIgnoreCase))
            {
                yield return "养殖坑塘";
            }
        }

        private static string TrimLandClassCodeSuffix(string normalizedText)
        {
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return string.Empty;
            }

            int start = -1;
            int length = 0;
            for (int i = 0; i < normalizedText.Length; i++)
            {
                if (char.IsDigit(normalizedText[i]))
                {
                    if (start < 0)
                    {
                        start = i;
                    }

                    length++;
                    continue;
                }

                if (start >= 0)
                {
                    break;
                }
            }

            if (start < 0 || length == 0)
            {
                return normalizedText;
            }

            string code = normalizedText.Substring(start, length);
            return code;
        }

        private static string NormalizeCode(string value)
        {
            return Normalize(value);
        }

        private static string NormalizeName(string value)
        {
            return Normalize(value)
                .Replace("其中", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("可调整", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, double> BuildProjectValues(
            Dictionary<string, double> rawValues,
            double projectTotal,
            int decimalPlaces)
        {
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            double rawTotal = rawValues.Values.Sum();
            double scale = rawTotal > 0 && projectTotal > 0 ? projectTotal / rawTotal : 1.0;

            foreach (var kvp in rawValues)
            {
                values[kvp.Key] = Math.Round(kvp.Value * scale, decimalPlaces);
            }

            BalanceLeafValues(rawValues, values, projectTotal, scale, decimalPlaces);

            foreach (var definition in Definitions.Where(x => x.Level == LandClassTableColumnLevel.SecondGroup))
            {
                double direct = values.TryGetValue(definition.Key, out double directValue) ? directValue : 0;
                double children = Definitions
                    .Where(x => x.Level == LandClassTableColumnLevel.Leaf && x.SecondGroupKey == definition.Key)
                    .Sum(x => values.TryGetValue(x.Key, out double value) ? value : 0);
                double total = Math.Round(direct + children, decimalPlaces);
                if (total > 0)
                {
                    values[definition.Key] = total;
                }
            }

            foreach (var definition in Definitions.Where(x => x.Level == LandClassTableColumnLevel.TopGroup))
            {
                double direct = values.TryGetValue(definition.Key, out double directValue) ? directValue : 0;
                double children = Definitions
                    .Where(x => x.TopGroupKey == definition.Key && x.Key != definition.Key)
                    .Where(x => x.Level == LandClassTableColumnLevel.SecondGroup || x.Level == LandClassTableColumnLevel.Leaf)
                    .Where(x => x.Level == LandClassTableColumnLevel.SecondGroup)
                    .Sum(x => values.TryGetValue(x.Key, out double value) ? value : 0);
                double total = Math.Round(direct + children, decimalPlaces);
                if (total > 0)
                {
                    values[definition.Key] = total;
                }
            }

            return values;
        }

        private static void BalanceLeafValues(
            Dictionary<string, double> rawValues,
            Dictionary<string, double> values,
            double projectTotal,
            double scale,
            int decimalPlaces)
        {
            var leafKeys = rawValues.Keys
                .Where(key => DefinitionsByKey.TryGetValue(key, out var definition) &&
                              definition.Level == LandClassTableColumnLevel.Leaf)
                .Where(key => values.TryGetValue(key, out double value) && value > 0)
                .ToList();
            if (leafKeys.Count == 0)
            {
                return;
            }

            double roundedTarget = Math.Round(projectTotal, decimalPlaces);
            double roundedLeafTotal = Math.Round(leafKeys.Sum(key => values[key]), decimalPlaces);
            double delta = Math.Round(roundedTarget - roundedLeafTotal, decimalPlaces);
            if (Math.Abs(delta) <= 0)
            {
                return;
            }

            string targetKey = leafKeys
                .Select(key => new
                {
                    Key = key,
                    Fraction = GetRoundingRemainder(rawValues[key] * scale, decimalPlaces, delta),
                    Value = values[key]
                })
                .OrderByDescending(x => x.Fraction)
                .ThenByDescending(x => x.Value)
                .First()
                .Key;

            values[targetKey] = Math.Round(values[targetKey] + delta, decimalPlaces);
        }

        private static double GetRoundingRemainder(double value, int decimalPlaces, double delta)
        {
            double factor = Math.Pow(10, decimalPlaces);
            double scaled = value * factor;
            double floor = Math.Floor(scaled);
            double remainder = scaled - floor;
            return delta >= 0 ? remainder : 1 - remainder;
        }

        private static IEnumerable<LandClassTableColumn> BuildColumns(HashSet<string> usedKeys)
        {
            foreach (var definition in Definitions)
            {
                if (!usedKeys.Contains(definition.Key))
                {
                    continue;
                }

                yield return new LandClassTableColumn(
                    definition.Key,
                    definition.Name,
                    definition.TopGroupKey,
                    definition.TopGroupName,
                    definition.SecondGroupKey,
                    definition.SecondGroupName,
                    definition.Code,
                    definition.Level);
            }
        }

        private static void AddAncestorKeys(HashSet<string> usedKeys, string key)
        {
            if (!DefinitionsByKey.TryGetValue(key, out var definition))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(definition.SecondGroupKey))
            {
                usedKeys.Add(definition.SecondGroupKey);
            }

            if (!string.IsNullOrWhiteSpace(definition.TopGroupKey))
            {
                usedKeys.Add(definition.TopGroupKey);
            }
        }

        private static void EnsureMinimumLeafColumns(HashSet<string> usedKeys)
        {
            var usedLeafKeys = Definitions
                .Where(x => x.Level == LandClassTableColumnLevel.Leaf && usedKeys.Contains(x.Key))
                .Select(x => x.Key)
                .ToList();
            if (usedLeafKeys.Count != 1)
            {
                return;
            }

            var usedLeaf = DefinitionsByKey[usedLeafKeys[0]];
            var sibling = Definitions
                .Where(x => x.Level == LandClassTableColumnLevel.Leaf &&
                            x.SecondGroupKey == usedLeaf.SecondGroupKey &&
                            !usedKeys.Contains(x.Key))
                .FirstOrDefault();
            if (sibling == null)
            {
                return;
            }

            usedKeys.Add(sibling.Key);
            AddAncestorKeys(usedKeys, sibling.Key);
        }

        private static void EnsureDefaultAgriculturalColumns(HashSet<string> usedKeys)
        {
            usedKeys.Add("AGRICULTURAL_TOTAL");
            usedKeys.Add("AGR_FARMLAND");
            usedKeys.Add("0101");
            usedKeys.Add("0103");
        }

        private static void EnsureDefaultConstructionColumns(HashSet<string> usedKeys)
        {
            usedKeys.Add("CONSTRUCTION_TOTAL");
            usedKeys.Add("CON_URBAN");
            usedKeys.Add("201");
            usedKeys.Add("203");
        }

        private static void RemoveSingleLeafSecondGroupColumns(HashSet<string> usedKeys)
        {
            var secondGroupKeys = Definitions
                .Where(x => x.Level == LandClassTableColumnLevel.SecondGroup && usedKeys.Contains(x.Key))
                .Select(x => x.Key)
                .ToList();

            foreach (string secondGroupKey in secondGroupKeys)
            {
                int leafCount = Definitions.Count(x =>
                    x.Level == LandClassTableColumnLevel.Leaf &&
                    x.SecondGroupKey == secondGroupKey &&
                    usedKeys.Contains(x.Key));
                if (leafCount == 1)
                {
                    usedKeys.Remove(secondGroupKey);
                }
            }
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                if ("（）()[]【】{}：:；;，,、-_/\\|".IndexOf(c) >= 0)
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(c));
            }

            return builder.ToString()
                .Replace("其中", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("可调整", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeTextValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static IReadOnlyList<LandClassDefinition> CreateDefinitions()
        {
            var list = new List<LandClassDefinition>();

            AddTop(list, "AGRICULTURAL_TOTAL", "农用地");
            AddSecond(list, "AGR_WETLAND", "湿地", "AGRICULTURAL_TOTAL", "农用地", "00");
            AddLeaf(list, "0303", "红树林地", "AGR_WETLAND", "湿地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0304", "森林沼泽", "AGR_WETLAND", "湿地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0306", "灌丛沼泽", "AGR_WETLAND", "湿地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0402", "沼泽草地", "AGR_WETLAND", "湿地", "AGRICULTURAL_TOTAL", "农用地");
            AddSecond(list, "AGR_FARMLAND", "耕地", "AGRICULTURAL_TOTAL", "农用地", "01");
            AddLeaf(list, "0101", "水田", "AGR_FARMLAND", "耕地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0102", "水浇地", "AGR_FARMLAND", "耕地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0103", "旱地", "AGR_FARMLAND", "耕地", "AGRICULTURAL_TOTAL", "农用地");
            AddSecond(list, "AGR_PLANTATION", "种植园地", "AGRICULTURAL_TOTAL", "农用地", "02");
            AddLeaf(list, "0201", "果园", "AGR_PLANTATION", "种植园地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0201K", "可调整果园", "AGR_PLANTATION", "种植园地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0202", "茶园", "AGR_PLANTATION", "种植园地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0202K", "可调整茶园", "AGR_PLANTATION", "种植园地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0203", "橡胶园", "AGR_PLANTATION", "种植园地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0203K", "可调整橡胶园", "AGR_PLANTATION", "种植园地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0204", "其他园地", "AGR_PLANTATION", "种植园地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0204K", "可调整其他园地", "AGR_PLANTATION", "种植园地", "AGRICULTURAL_TOTAL", "农用地");
            AddSecond(list, "AGR_FOREST", "林地", "AGRICULTURAL_TOTAL", "农用地", "03");
            AddLeaf(list, "0301", "乔木林地", "AGR_FOREST", "林地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0301K", "可调整乔木林地", "AGR_FOREST", "林地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0302", "竹林地", "AGR_FOREST", "林地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0302K", "可调整竹林地", "AGR_FOREST", "林地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0305", "灌木林地", "AGR_FOREST", "林地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0307", "其他林地", "AGR_FOREST", "林地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0307K", "可调整其他林地", "AGR_FOREST", "林地", "AGRICULTURAL_TOTAL", "农用地");
            AddSecond(list, "AGR_GRASS", "草地", "AGRICULTURAL_TOTAL", "农用地", "04");
            AddLeaf(list, "0401", "天然牧草地", "AGR_GRASS", "草地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0403", "人工牧草地", "AGR_GRASS", "草地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0403K", "可调整人工牧草地", "AGR_GRASS", "草地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "0404", "其他草地", "AGR_GRASS", "草地", "AGRICULTURAL_TOTAL", "农用地");
            AddSecond(list, "AGR_TRANSPORT", "交通运输用地", "AGRICULTURAL_TOTAL", "农用地", "10");
            AddLeaf(list, "1006", "农村道路", "AGR_TRANSPORT", "交通运输用地", "AGRICULTURAL_TOTAL", "农用地");
            AddSecond(list, "AGR_WATER", "水域及水利设施用地", "AGRICULTURAL_TOTAL", "农用地", "11");
            AddLeaf(list, "1103", "水库水面", "AGR_WATER", "水域及水利设施用地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "1104", "坑塘水面", "AGR_WATER", "水域及水利设施用地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "1104K", "可调整养殖坑塘", "AGR_WATER", "水域及水利设施用地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "1107", "沟渠", "AGR_WATER", "水域及水利设施用地", "AGRICULTURAL_TOTAL", "农用地");
            AddSecond(list, "AGR_OTHER", "其他土地", "AGRICULTURAL_TOTAL", "农用地", "12");
            AddLeaf(list, "1202", "设施农用地", "AGR_OTHER", "其他土地", "AGRICULTURAL_TOTAL", "农用地");
            AddLeaf(list, "1203", "田坎", "AGR_OTHER", "其他土地", "AGRICULTURAL_TOTAL", "农用地");

            AddTop(list, "CONSTRUCTION_TOTAL", "建设用地");
            AddSecond(list, "CON_URBAN", "城镇村及工矿用地", "CONSTRUCTION_TOTAL", "建设用地", "20");
            AddLeaf(list, "201", "城市", "CON_URBAN", "城镇村及工矿用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "202", "建制镇", "CON_URBAN", "城镇村及工矿用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "203", "村庄", "CON_URBAN", "城镇村及工矿用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "204", "盐田及采矿用地", "CON_URBAN", "城镇村及工矿用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "205", "特殊用地", "CON_URBAN", "城镇村及工矿用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddSecond(list, "CON_BUSINESS", "商业服务业用地", "CONSTRUCTION_TOTAL", "建设用地", "05");
            AddLeaf(list, "05H1", "商业服务业设施用地", "CON_BUSINESS", "商业服务业用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "0508", "物流仓储用地", "CON_BUSINESS", "商业服务业用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddSecond(list, "CON_INDUSTRIAL", "工矿用地", "CONSTRUCTION_TOTAL", "建设用地", "06");
            AddLeaf(list, "0601", "工业用地", "CON_INDUSTRIAL", "工矿用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "0602", "采矿用地", "CON_INDUSTRIAL", "工矿用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "0603", "盐田", "CON_INDUSTRIAL", "工矿用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddSecond(list, "CON_RESIDENTIAL", "住宅用地", "CONSTRUCTION_TOTAL", "建设用地", "07");
            AddLeaf(list, "0701", "城镇住宅用地", "CON_RESIDENTIAL", "住宅用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "0702", "农村宅基地", "CON_RESIDENTIAL", "住宅用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddSecond(list, "CON_PUBLIC", "公共管理与公共服务用地", "CONSTRUCTION_TOTAL", "建设用地", "08");
            AddLeaf(list, "08H1", "机关团体新闻出版用地", "CON_PUBLIC", "公共管理与公共服务用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "08H2", "科教文卫用地", "CON_PUBLIC", "公共管理与公共服务用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "0809", "公共设施用地", "CON_PUBLIC", "公共管理与公共服务用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "0810", "公园与绿地", "CON_PUBLIC", "公共管理与公共服务用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddSecond(list, "CON_SPECIAL", "特殊用地", "CONSTRUCTION_TOTAL", "建设用地", "09");
            AddSecond(list, "CON_TRANSPORT", "交通运输用地", "CONSTRUCTION_TOTAL", "建设用地", "10");
            AddLeaf(list, "1001", "铁路用地", "CON_TRANSPORT", "交通运输用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "1002", "轨道交通用地", "CON_TRANSPORT", "交通运输用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "1003", "公路用地", "CON_TRANSPORT", "交通运输用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "1004", "城镇村道路用地", "CON_TRANSPORT", "交通运输用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "1005", "交通服务场站用地", "CON_TRANSPORT", "交通运输用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "1007", "机场用地", "CON_TRANSPORT", "交通运输用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "1008", "港口码头用地", "CON_TRANSPORT", "交通运输用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "1009", "管道运输用地", "CON_TRANSPORT", "交通运输用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddSecond(list, "CON_WATER", "水域及水利设施用地", "CONSTRUCTION_TOTAL", "建设用地", "11");
            AddLeaf(list, "1107A", "干渠", "CON_WATER", "水域及水利设施用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddLeaf(list, "1109", "水工建筑用地", "CON_WATER", "水域及水利设施用地", "CONSTRUCTION_TOTAL", "建设用地");
            AddSecond(list, "CON_OTHER", "其他用地", "CONSTRUCTION_TOTAL", "建设用地", "12");
            AddLeaf(list, "1201", "空闲地", "CON_OTHER", "其他用地", "CONSTRUCTION_TOTAL", "建设用地");

            AddTop(list, "UNUSED_TOTAL", "未利用地");
            AddSecond(list, "UNUSED_WETLAND", "湿地", "UNUSED_TOTAL", "未利用地", "00");
            AddLeaf(list, "1105", "沿海滩涂", "UNUSED_WETLAND", "湿地", "UNUSED_TOTAL", "未利用地");
            AddLeaf(list, "1106", "内陆滩涂", "UNUSED_WETLAND", "湿地", "UNUSED_TOTAL", "未利用地");
            AddLeaf(list, "1108", "沼泽地", "UNUSED_WETLAND", "湿地", "UNUSED_TOTAL", "未利用地");
            AddSecond(list, "UNUSED_WATER", "水域及水利设施用地", "UNUSED_TOTAL", "未利用地", "11");
            AddLeaf(list, "1101", "河流水面", "UNUSED_WATER", "水域及水利设施用地", "UNUSED_TOTAL", "未利用地");
            AddLeaf(list, "1102", "湖泊水面", "UNUSED_WATER", "水域及水利设施用地", "UNUSED_TOTAL", "未利用地");
            AddLeaf(list, "1110", "冰川及永久积雪", "UNUSED_WATER", "水域及水利设施用地", "UNUSED_TOTAL", "未利用地");
            AddSecond(list, "UNUSED_OTHER", "其他土地", "UNUSED_TOTAL", "未利用地", "12");
            AddLeaf(list, "1204", "盐碱地", "UNUSED_OTHER", "其他土地", "UNUSED_TOTAL", "未利用地");
            AddLeaf(list, "1205", "沙地", "UNUSED_OTHER", "其他土地", "UNUSED_TOTAL", "未利用地");
            AddLeaf(list, "1206", "裸土地", "UNUSED_OTHER", "其他土地", "UNUSED_TOTAL", "未利用地");
            AddLeaf(list, "1207", "裸岩石砾地", "UNUSED_OTHER", "其他土地", "UNUSED_TOTAL", "未利用地");
            AddLeaf(list, "1208", "后备耕地", "UNUSED_OTHER", "其他土地", "UNUSED_TOTAL", "未利用地");

            return list;
        }

        private static void AddTop(List<LandClassDefinition> list, string key, string name)
        {
            list.Add(new LandClassDefinition(key, name, string.Empty, key, name, string.Empty, string.Empty, LandClassTableColumnLevel.TopGroup));
        }

        private static void AddSecond(
            List<LandClassDefinition> list,
            string key,
            string name,
            string topGroupKey,
            string topGroupName,
            string code)
        {
            list.Add(new LandClassDefinition(key, name, code, topGroupKey, topGroupName, key, name, LandClassTableColumnLevel.SecondGroup));
        }

        private static void AddLeaf(
            List<LandClassDefinition> list,
            string code,
            string name,
            string secondGroupKey,
            string secondGroupName,
            string topGroupKey,
            string topGroupName)
        {
            list.Add(new LandClassDefinition(code, name, code, topGroupKey, topGroupName, secondGroupKey, secondGroupName, LandClassTableColumnLevel.Leaf));
        }
    }

    public sealed record LandClassDefinition(
        string Key,
        string Name,
        string Code,
        string TopGroupKey,
        string TopGroupName,
        string SecondGroupKey,
        string SecondGroupName,
        LandClassTableColumnLevel Level);

    internal sealed record ProjectKey(string ProjectName, string PlotName);

    internal sealed class ProjectKeyComparer : IEqualityComparer<ProjectKey>
    {
        internal static readonly ProjectKeyComparer Instance = new();

        public bool Equals(ProjectKey? x, ProjectKey? y)
        {
            if (x == null || y == null)
            {
                return x == y;
            }

            return string.Equals(x.ProjectName, y.ProjectName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(x.PlotName, y.PlotName, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(ProjectKey obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProjectName ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.PlotName ?? string.Empty));
        }
    }

    internal sealed record RowKey(string ProjectName, string PlotName, string OwnerUnitName, string OwnerNatureName);
}
