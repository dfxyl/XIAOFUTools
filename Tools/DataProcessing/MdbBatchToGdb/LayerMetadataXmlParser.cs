using System;
using System.Collections.Generic;
using System.Xml;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal sealed class SourceLayerMetadataInfo
    {
        public string FeatureDatasetName { get; set; } = string.Empty;

        public string AliasName { get; set; } = string.Empty;

        public Dictionary<string, string> FieldAliases { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal static class LayerMetadataXmlParser
    {
        private static readonly string[] LayerIdentityElementNames =
        {
            "Name",
            "DatasetName",
            "CatalogPath",
            "enttypl",
            "itemName",
            "ftname",
            "resTitle"
        };

        private static readonly string[] FieldNodeNames =
        {
            "attr",
            "Field",
            "GPFieldInfoEx",
            "FieldInfo"
        };

        public static void MergeMetadataXml(
            string xml,
            SourceLayerMetadataInfo info,
            string sourceLayerName,
            string outputLayerName)
        {
            if (string.IsNullOrWhiteSpace(xml) || info == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(info.FeatureDatasetName))
            {
                string featureDatasetName = ExtractFeatureDatasetName(xml);
                if (!string.IsNullOrWhiteSpace(featureDatasetName))
                {
                    info.FeatureDatasetName = featureDatasetName.Trim();
                }
            }

            try
            {
                var document = new XmlDocument();
                document.LoadXml(xml);

                HashSet<string> relevantLayerNames = BuildRelevantLayerNames(sourceLayerName, outputLayerName);

                if (string.IsNullOrWhiteSpace(info.AliasName))
                {
                    string aliasName = ReadLayerAlias(document, relevantLayerNames);
                    if (!string.IsNullOrWhiteSpace(aliasName))
                    {
                        info.AliasName = aliasName.Trim();
                    }
                }

                MergeFieldAliases(document, info.FieldAliases, relevantLayerNames);
            }
            catch
            {
            }
        }

        private static string ExtractFeatureDatasetName(string xml)
        {
            string featureDatasetName = ExtractXmlTagValue(xml, "FeatureDatasetName");
            if (!string.IsNullOrWhiteSpace(featureDatasetName))
            {
                return featureDatasetName;
            }

            string catalogPath = ExtractXmlTagValue(xml, "CatalogPath");
            if (string.IsNullOrWhiteSpace(catalogPath))
            {
                return string.Empty;
            }

            string datasetName = GetDatasetNameFromLayerName(catalogPath);
            return datasetName?.TrimStart('\\', '/').Trim() ?? string.Empty;
        }

        private static HashSet<string> BuildRelevantLayerNames(string sourceLayerName, string outputLayerName)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddLayerNameVariants(names, sourceLayerName);
            AddLayerNameVariants(names, outputLayerName);
            return names;
        }

        private static void AddLayerNameVariants(ISet<string> names, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string normalized = NormalizeLayerPath(value);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                names.Add(normalized);
            }

            string lastSegment = GetLastSegment(normalized);
            if (!string.IsNullOrWhiteSpace(lastSegment))
            {
                names.Add(lastSegment);
            }
        }

        private static string ReadLayerAlias(XmlDocument document, ISet<string> relevantLayerNames)
        {
            XmlNodeList? nodeList = document.SelectNodes("//*");
            if (nodeList == null)
            {
                return string.Empty;
            }

            string bestAlias = string.Empty;
            int bestScore = int.MinValue;

            foreach (XmlNode node in nodeList)
            {
                if (node == null || IsFieldNode(node))
                {
                    continue;
                }

                string aliasName = ReadChildText(node, "AliasName");
                if (string.IsNullOrWhiteSpace(aliasName))
                {
                    continue;
                }

                int score = ScoreLayerAliasNode(node, aliasName, relevantLayerNames);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAlias = aliasName.Trim();
                }
            }

            return bestScore >= 0 ? bestAlias : string.Empty;
        }

        private static int ScoreLayerAliasNode(XmlNode node, string aliasName, ISet<string> relevantLayerNames)
        {
            if (!ContextMatchesLayer(node, relevantLayerNames))
            {
                return int.MinValue;
            }

            int score = 100;

            if (!MatchesRelevantLayer(aliasName, relevantLayerNames))
            {
                score += 20;
            }

            if (node.LocalName.StartsWith("DE", StringComparison.OrdinalIgnoreCase))
            {
                score += 10;
            }

            return score;
        }

        private static void MergeFieldAliases(
            XmlDocument document,
            IDictionary<string, string> fieldAliases,
            ISet<string> relevantLayerNames)
        {
            bool mergedLayerScoped = MergeFieldAliases(document, fieldAliases, relevantLayerNames, requireLayerContext: true);
            if (!mergedLayerScoped)
            {
                MergeFieldAliases(document, fieldAliases, relevantLayerNames, requireLayerContext: false);
            }
        }

        private static bool MergeFieldAliases(
            XmlDocument document,
            IDictionary<string, string> fieldAliases,
            ISet<string> relevantLayerNames,
            bool requireLayerContext)
        {
            XmlNodeList? nodeList = document.SelectNodes("//*");
            if (nodeList == null)
            {
                return false;
            }

            bool mergedAny = false;

            foreach (XmlNode node in nodeList)
            {
                if (node == null || !IsFieldNode(node))
                {
                    continue;
                }

                if (requireLayerContext && !ContextMatchesLayer(node, relevantLayerNames))
                {
                    continue;
                }

                if (!TryReadFieldAlias(node, out string fieldName, out string aliasName))
                {
                    continue;
                }

                if (fieldAliases.ContainsKey(fieldName))
                {
                    continue;
                }

                fieldAliases[fieldName] = aliasName;
                mergedAny = true;
            }

            return mergedAny;
        }

        private static bool TryReadFieldAlias(XmlNode node, out string fieldName, out string aliasName)
        {
            fieldName = ReadChildText(node, "attrlabl", "Name");
            aliasName = ReadChildText(node, "attalias", "AliasName");

            if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(aliasName))
            {
                fieldName = string.Empty;
                aliasName = string.Empty;
                return false;
            }

            fieldName = fieldName.Trim();
            aliasName = aliasName.Trim();
            return true;
        }

        private static bool IsFieldNode(XmlNode node)
        {
            foreach (string fieldNodeName in FieldNodeNames)
            {
                if (node.LocalName.Equals(fieldNodeName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContextMatchesLayer(XmlNode? node, ISet<string> relevantLayerNames)
        {
            for (XmlNode? current = node; current != null; current = current.ParentNode)
            {
                if (NodeMatchesLayer(current, relevantLayerNames))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool NodeMatchesLayer(XmlNode? node, ISet<string> relevantLayerNames)
        {
            if (node == null)
            {
                return false;
            }

            if (node.Attributes != null)
            {
                foreach (XmlAttribute attribute in node.Attributes)
                {
                    if (attribute != null && MatchesRelevantLayer(attribute.Value, relevantLayerNames))
                    {
                        return true;
                    }
                }
            }

            foreach (string elementName in LayerIdentityElementNames)
            {
                string value = ReadChildText(node, elementName);
                if (MatchesRelevantLayer(value, relevantLayerNames))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesRelevantLayer(string value, ISet<string> relevantLayerNames)
        {
            if (string.IsNullOrWhiteSpace(value) || relevantLayerNames == null || relevantLayerNames.Count == 0)
            {
                return false;
            }

            string normalized = NormalizeLayerPath(value);
            if (relevantLayerNames.Contains(normalized))
            {
                return true;
            }

            string lastSegment = GetLastSegment(normalized);
            return !string.IsNullOrWhiteSpace(lastSegment) && relevantLayerNames.Contains(lastSegment);
        }

        private static string ReadChildText(XmlNode node, params string[] localNames)
        {
            if (node == null || localNames == null || localNames.Length == 0)
            {
                return string.Empty;
            }

            foreach (string localName in localNames)
            {
                XmlNode? child = node.SelectSingleNode($"*[local-name()='{localName}']");
                if (!string.IsNullOrWhiteSpace(child?.InnerText))
                {
                    return child.InnerText.Trim();
                }
            }

            return string.Empty;
        }

        private static string ExtractXmlTagValue(string xml, string tagName)
        {
            string startTag = "<" + tagName + ">";
            string endTag = "</" + tagName + ">";

            int start = xml.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return string.Empty;
            }

            start += startTag.Length;
            int end = xml.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
            if (end < 0 || end <= start)
            {
                return string.Empty;
            }

            return xml.Substring(start, end - start);
        }

        private static string NormalizeLayerPath(string layerName)
        {
            return string.IsNullOrWhiteSpace(layerName)
                ? string.Empty
                : layerName.Trim().Replace('/', '\\').Trim('\\');
        }

        private static string GetLastSegment(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
            {
                return string.Empty;
            }

            int separatorIndex = layerName.LastIndexOf('\\');
            return separatorIndex >= 0 && separatorIndex < layerName.Length - 1
                ? layerName.Substring(separatorIndex + 1).Trim()
                : layerName.Trim();
        }

        private static string GetDatasetNameFromLayerName(string sourceLayerName)
        {
            if (string.IsNullOrWhiteSpace(sourceLayerName))
            {
                return string.Empty;
            }

            int slashIndex = sourceLayerName.LastIndexOf('/');
            int backslashIndex = sourceLayerName.LastIndexOf('\\');
            int splitIndex = Math.Max(slashIndex, backslashIndex);
            if (splitIndex > 0)
            {
                return sourceLayerName.Substring(0, splitIndex).Trim();
            }

            return string.Empty;
        }
    }
}
