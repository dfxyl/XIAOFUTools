using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using OSGeo.OGR;
using OSGeo.OSR;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal sealed class SourceLayerInfo
    {
        public int Index { get; set; }

        public string SourceName { get; set; } = string.Empty;

        public string FeatureDatasetName { get; set; } = string.Empty;

        public string OutputName { get; set; } = string.Empty;

        public string AliasName { get; set; } = string.Empty;

        public IReadOnlyDictionary<string, string> FieldAliases { get; set; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool IsTable { get; set; }
    }

    internal sealed class SourceLayerMetadataInfo
    {
        public string FeatureDatasetName { get; set; } = string.Empty;

        public string AliasName { get; set; } = string.Empty;

        public Dictionary<string, string> FieldAliases { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal static class GdalMdbLayerInspector
    {
        public static List<SourceLayerInfo> GetSourceLayerInfos(DataSource sourceDs)
        {
            var result = new List<SourceLayerInfo>();
            int layerCount = sourceDs.GetLayerCount();
            for (int i = 0; i < layerCount; i++)
            {
                Layer sourceLayer = sourceDs.GetLayerByIndex(i);
                if (sourceLayer == null)
                {
                    continue;
                }

                string sourceLayerName = OgrUtf8Interop.GetLayerName(sourceLayer);
                SourceLayerMetadataInfo metadataInfo = ReadLayerMetadataInfo(sourceDs, sourceLayerName);
                string featureDatasetName = GetFeatureDatasetName(sourceLayerName, metadataInfo);
                string outputLayerName = GetFeatureClassOrTableName(sourceLayerName);
                FeatureDefn defn = sourceLayer.GetLayerDefn();
                bool isTable = defn == null || defn.GetGeomType() == wkbGeometryType.wkbNone;

                result.Add(new SourceLayerInfo
                {
                    Index = i,
                    SourceName = sourceLayerName,
                    FeatureDatasetName = featureDatasetName,
                    OutputName = outputLayerName,
                    AliasName = string.IsNullOrWhiteSpace(metadataInfo.AliasName) ? outputLayerName : metadataInfo.AliasName.Trim(),
                    FieldAliases = metadataInfo.FieldAliases,
                    IsTable = isTable
                });
            }

            return result;
        }

        public static Dictionary<string, SpatialReference> BuildFeatureDatasetSpatialReferences(
            DataSource sourceDs,
            IEnumerable<SourceLayerInfo> layerInfos)
        {
            var result = new Dictionary<string, SpatialReference>(StringComparer.OrdinalIgnoreCase);
            foreach (SourceLayerInfo layerInfo in layerInfos)
            {
                if (layerInfo.IsTable || string.IsNullOrWhiteSpace(layerInfo.FeatureDatasetName))
                {
                    continue;
                }

                if (result.ContainsKey(layerInfo.FeatureDatasetName))
                {
                    continue;
                }

                Layer sourceLayer = sourceDs.GetLayerByIndex(layerInfo.Index);
                if (sourceLayer == null)
                {
                    continue;
                }

                SpatialReference sourceReference = sourceLayer.GetSpatialRef();
                SpatialReference clone = NormalizeSpatialReferenceForFileGeodatabase(sourceReference);
                if (clone != null)
                {
                    result[layerInfo.FeatureDatasetName] = clone;
                }
            }

            return result;
        }

        public static SpatialReference ResolveTargetLayerSpatialReference(
            Layer sourceLayer,
            SourceLayerInfo layerInfo,
            IDictionary<string, SpatialReference> featureDatasetSrs,
            IDictionary<string, SpatialReference> createdFeatureDatasetSrs,
            Action<string> log)
        {
            SpatialReference layerReference = sourceLayer.GetSpatialRef();
            if (string.IsNullOrWhiteSpace(layerInfo.FeatureDatasetName) || layerInfo.IsTable)
            {
                return layerReference;
            }

            if (createdFeatureDatasetSrs.TryGetValue(layerInfo.FeatureDatasetName, out SpatialReference createdDatasetReference)
                && createdDatasetReference != null)
            {
                return createdDatasetReference;
            }

            if (!featureDatasetSrs.TryGetValue(layerInfo.FeatureDatasetName, out SpatialReference datasetReference)
                || datasetReference == null)
            {
                SpatialReference clone = NormalizeSpatialReferenceForFileGeodatabase(layerReference);
                if (clone == null)
                {
                    throw new InvalidOperationException(
                        $"要素数据集 {layerInfo.FeatureDatasetName} 中图层 {layerInfo.OutputName} 未读取到有效坐标系。");
                }

                featureDatasetSrs[layerInfo.FeatureDatasetName] = clone;
                return clone;
            }

            if (layerReference != null && !IsSpatialReferenceEquivalent(layerReference, datasetReference))
            {
                log?.Invoke($"警告: 图层 {layerInfo.FeatureDatasetName}\\{layerInfo.OutputName} 坐标系与要素数据集不一致，已按数据集坐标系创建。");
            }

            return datasetReference;
        }

        public static SpatialReference CloneSpatialReference(SpatialReference spatialReference)
        {
            if (spatialReference == null)
            {
                return null;
            }

            try
            {
                return spatialReference.Clone();
            }
            catch
            {
                try
                {
                    spatialReference.ExportToWkt(out string wkt, null);
                    if (!string.IsNullOrWhiteSpace(wkt))
                    {
                        return new SpatialReference(wkt);
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        public static SpatialReference NormalizeSpatialReferenceForFileGeodatabase(SpatialReference spatialReference)
        {
            if (spatialReference == null)
            {
                return null;
            }

            try
            {
                spatialReference.ExportToWkt(out string esriWkt, new[] { "FORMAT=WKT1_ESRI" });
                if (!string.IsNullOrWhiteSpace(esriWkt))
                {
                    return new SpatialReference(esriWkt);
                }
            }
            catch
            {
            }

            return CloneSpatialReference(spatialReference);
        }

        private static bool IsSpatialReferenceEquivalent(SpatialReference a, SpatialReference b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }

            try
            {
                return a.IsSame(b, null) == 1;
            }
            catch
            {
                try
                {
                    a.ExportToWkt(out string wa, null);
                    b.ExportToWkt(out string wb, null);
                    return string.Equals(wa, wb, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return false;
                }
            }
        }

        private static string GetFeatureDatasetName(string sourceLayerName, SourceLayerMetadataInfo metadataInfo)
        {
            string featureDatasetName = GetDatasetNameFromLayerName(sourceLayerName);
            if (!string.IsNullOrWhiteSpace(featureDatasetName))
            {
                return NormalizeFeatureDatasetName(featureDatasetName);
            }

            featureDatasetName = metadataInfo?.FeatureDatasetName;
            if (!string.IsNullOrWhiteSpace(featureDatasetName))
            {
                return NormalizeFeatureDatasetName(featureDatasetName);
            }

            return string.Empty;
        }

        private static string GetFeatureClassOrTableName(string sourceLayerName)
        {
            int slashIndex = sourceLayerName.LastIndexOf('/');
            int backslashIndex = sourceLayerName.LastIndexOf('\\');
            int splitIndex = Math.Max(slashIndex, backslashIndex);
            if (splitIndex >= 0 && splitIndex < sourceLayerName.Length - 1)
            {
                return sourceLayerName.Substring(splitIndex + 1).Trim();
            }

            return sourceLayerName.Trim();
        }

        private static string GetDatasetNameFromLayerName(string sourceLayerName)
        {
            int slashIndex = sourceLayerName.LastIndexOf('/');
            int backslashIndex = sourceLayerName.LastIndexOf('\\');
            int splitIndex = Math.Max(slashIndex, backslashIndex);
            if (splitIndex > 0)
            {
                return sourceLayerName.Substring(0, splitIndex).Trim();
            }

            return string.Empty;
        }

        private static string NormalizeFeatureDatasetName(string datasetName)
        {
            if (string.IsNullOrWhiteSpace(datasetName))
            {
                return string.Empty;
            }

            string normalized = datasetName.Trim().Replace('/', '\\');
            normalized = normalized.Trim('\\');
            while (normalized.Contains("\\\\"))
            {
                normalized = normalized.Replace("\\\\", "\\");
            }

            return normalized;
        }

        private static SourceLayerMetadataInfo ReadLayerMetadataInfo(DataSource sourceDs, string layerName)
        {
            var info = new SourceLayerMetadataInfo();
            string layerNameEscaped = layerName.Replace("'", "''");
            string[] sqlCandidates =
            {
                $"GetLayerMetadata {layerName}",
                $"GetLayerDefinition {layerName}",
                $"GetLayerMetadata '{layerNameEscaped}'",
                $"GetLayerDefinition '{layerNameEscaped}'"
            };

            foreach (string sql in sqlCandidates)
            {
                Layer metadataLayer = null;
                try
                {
                    metadataLayer = sourceDs.ExecuteSQL(sql, null, "PGeo");
                    if (metadataLayer == null)
                    {
                        continue;
                    }

                    var builder = new StringBuilder();
                    metadataLayer.ResetReading();

                    Feature feature;
                    while ((feature = metadataLayer.GetNextFeature()) != null)
                    {
                        using (feature)
                        {
                            for (int i = 0; i < feature.GetFieldCount(); i++)
                            {
                                builder.Append(OgrUtf8Interop.GetFieldAsString(feature, i));
                            }
                        }
                    }

                    string xml = builder.ToString();
                    if (string.IsNullOrWhiteSpace(xml))
                    {
                        continue;
                    }

                    MergeMetadataXml(xml, info);
                }
                catch
                {
                }
                finally
                {
                    if (metadataLayer != null)
                    {
                        sourceDs.ReleaseResultSet(metadataLayer);
                    }
                }
            }

            return info;
        }

        private static void MergeMetadataXml(string xml, SourceLayerMetadataInfo info)
        {
            if (string.IsNullOrWhiteSpace(xml) || info == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(info.FeatureDatasetName))
            {
                string featureDatasetName = ExtractXmlTagValue(xml, "FeatureDatasetName");
                if (string.IsNullOrWhiteSpace(featureDatasetName))
                {
                    string catalogPath = ExtractXmlTagValue(xml, "CatalogPath");
                    if (!string.IsNullOrWhiteSpace(catalogPath))
                    {
                        featureDatasetName = GetDatasetNameFromLayerName(catalogPath)?.TrimStart('\\', '/').Trim();
                    }
                }

                if (!string.IsNullOrWhiteSpace(featureDatasetName))
                {
                    info.FeatureDatasetName = featureDatasetName.Trim();
                }
            }

            try
            {
                var document = new XmlDocument();
                document.LoadXml(xml);

                if (string.IsNullOrWhiteSpace(info.AliasName))
                {
                    info.AliasName =
                        SelectSingleNodeText(document, "//*[local-name()='itemName']") ??
                        SelectSingleNodeText(document, "//*[local-name()='resTitle']") ??
                        SelectSingleNodeAttribute(document, "//*[local-name()='detailed']", "Name") ??
                        SelectSingleNodeAttribute(document, "//*[local-name()='esriterm']", "Name") ??
                        SelectSingleNodeText(document, "//*[local-name()='AliasName']");
                }

                XmlNodeList attributeNodes = document.SelectNodes("//*[local-name()='attr']");
                if (attributeNodes == null)
                {
                    return;
                }

                foreach (XmlNode attributeNode in attributeNodes)
                {
                    string fieldName =
                        SelectSingleNodeText(attributeNode, "*[local-name()='attrlabl']") ??
                        SelectSingleNodeText(attributeNode, "*[local-name()='Name']");
                    string aliasName =
                        SelectSingleNodeText(attributeNode, "*[local-name()='attalias']") ??
                        SelectSingleNodeText(attributeNode, "*[local-name()='AliasName']");

                    if (string.IsNullOrWhiteSpace(fieldName) || string.IsNullOrWhiteSpace(aliasName))
                    {
                        continue;
                    }

                    if (!info.FieldAliases.ContainsKey(fieldName))
                    {
                        info.FieldAliases[fieldName] = aliasName.Trim();
                    }
                }
            }
            catch
            {
            }
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

        private static string SelectSingleNodeText(XmlNode parentNode, string xpath)
        {
            XmlNode node = parentNode?.SelectSingleNode(xpath);
            return string.IsNullOrWhiteSpace(node?.InnerText) ? null : node.InnerText.Trim();
        }

        private static string SelectSingleNodeAttribute(XmlNode parentNode, string xpath, string attributeName)
        {
            XmlNode node = parentNode?.SelectSingleNode(xpath);
            string value = node?.Attributes?[attributeName]?.Value;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
