using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO.Compression;
using System.Xml;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Conversion.ExportToKml
{
    internal partial class ExportToKmlViewModel
    {

        private async Task AppendFieldLabelLayerToKmzAsync(
            object sourceLayerInput,
            string targetKmzPath,
            CancellationToken cancellationToken,
            string whereClause = null)
        {
            var labelItems = await BuildLabelItemsAsync(sourceLayerInput, SelectedLabelField, cancellationToken, whereClause);
            if (labelItems.Count == 0)
            {
                AddLog($"字段 [{SelectedLabelField}] 没有可用标注，已跳过标注层");
                return;
            }

            var targetKml = await _fileStore.ReadDocKmlAsync(targetKmzPath, cancellationToken);
            var mergedKml = MergeKmlWithLabelItems(targetKml, labelItems, SelectedLabelField);
            await _fileStore.WriteDocKmlAsync(targetKmzPath, mergedKml, cancellationToken);

            AddLog($"已按字段 [{SelectedLabelField}] 追加 {labelItems.Count} 个标注点");
        }

        private static string ExtractFieldValueFromPlacemark(XmlElement placemark, string fieldName, string kmlNs)
        {
            var dataNodes = placemark.GetElementsByTagName("Data", kmlNs);
            for (int i = 0; i < dataNodes.Count; i++)
            {
                if (dataNodes[i] is not XmlElement dataElement)
                {
                    continue;
                }

                var nameAttr = dataElement.GetAttribute("name");
                if (!string.Equals(nameAttr, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var valueElement = dataElement["value", kmlNs];
                if (valueElement != null)
                {
                    return valueElement.InnerText;
                }
            }

            var simpleDataNodes = placemark.GetElementsByTagName("SimpleData", kmlNs);
            for (int i = 0; i < simpleDataNodes.Count; i++)
            {
                if (simpleDataNodes[i] is not XmlElement simpleDataElement)
                {
                    continue;
                }

                var nameAttr = simpleDataElement.GetAttribute("name");
                if (string.Equals(nameAttr, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return simpleDataElement.InnerText;
                }
            }

            return string.Empty;
        }

        private static void EnsureLabelStyle(XmlDocument targetDoc, XmlElement targetDocument, XmlNamespaceManager nsManager, string kmlNs)
        {
            var existing = targetDocument.SelectSingleNode($"kml:Style[@id='{LabelStyleId}']", nsManager);
            if (existing != null)
            {
                return;
            }

            var styleElement = targetDoc.CreateElement("Style", kmlNs);
            var idAttribute = targetDoc.CreateAttribute("id");
            idAttribute.Value = LabelStyleId;
            styleElement.Attributes.Append(idAttribute);

            var iconStyle = targetDoc.CreateElement("IconStyle", kmlNs);
            var iconScale = targetDoc.CreateElement("scale", kmlNs);
            iconScale.InnerText = "0";
            iconStyle.AppendChild(iconScale);

            var labelStyle = targetDoc.CreateElement("LabelStyle", kmlNs);
            var labelColor = targetDoc.CreateElement("color", kmlNs);
            labelColor.InnerText = "FFFFFFFF";
            var labelScale = targetDoc.CreateElement("scale", kmlNs);
            labelScale.InnerText = "1";
            labelStyle.AppendChild(labelColor);
            labelStyle.AppendChild(labelScale);

            styleElement.AppendChild(iconStyle);
            styleElement.AppendChild(labelStyle);
            targetDocument.AppendChild(styleElement);
        }

        private async Task<(FieldType FieldType, List<GroupExportItem> Groups)> CollectGroupInfoAsync(
            FeatureLayer inputLayer,
            string groupField,
            CancellationToken cancellationToken)
        {
            return await QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var table = inputLayer.GetTable();
                var definition = table.GetDefinition();
                var field = definition.GetFields().FirstOrDefault(f => f.Name == groupField);
                if (field == null)
                {
                    throw new InvalidOperationException($"分组字段不存在: {groupField}");
                }

                var uniqueGroups = new Dictionary<string, GroupExportItem>(StringComparer.Ordinal);

                using var cursor = table.Search();
                while (cursor.MoveNext())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var row = cursor.Current;
                    var rawValue = row[groupField];
                    var isNull = rawValue == null || rawValue == DBNull.Value;
                    var displayValue = isNull
                        ? "空值"
                        : (rawValue.ToString() ?? string.Empty);

                    if (string.IsNullOrWhiteSpace(displayValue))
                    {
                        displayValue = "空字符串";
                    }

                    var key = isNull ? "__NULL__" : $"VALUE::{rawValue}";
                    if (!uniqueGroups.ContainsKey(key))
                    {
                        uniqueGroups[key] = new GroupExportItem
                        {
                            RawValue = isNull ? null : rawValue,
                            DisplayValue = displayValue
                        };
                    }
                }

                var orderedGroups = uniqueGroups.Values
                    .OrderBy(g => g.DisplayValue)
                    .ToList();

                return (field.FieldType, orderedGroups);
            });
        }

        private static string EscapeSqlValue(string input)
        {
            return string.IsNullOrEmpty(input) ? string.Empty : input.Replace("'", "''");
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            return string.IsNullOrEmpty(sanitized) ? "export" : sanitized;
        }
    }
}
