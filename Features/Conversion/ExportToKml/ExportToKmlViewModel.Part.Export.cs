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

        private async Task RunExportAsync()
        {
            if (!CanProcess) return;

            IsProcessing = true;
            Progress = 0;
            IsProgressIndeterminate = true;
            StatusMessage = "正在处理...";
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await PerformExport(_cancellationTokenSource.Token);

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusMessage = "导出完成";
                    AddLog("KML/KMZ 导出完成！");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
                AddLog("操作被用户取消");
            }
            catch (Exception ex)
            {
                StatusMessage = $"导出失败: {ex.Message}";
                AddLog($"错误: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 0;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void CancelExport()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task PerformExport(CancellationToken cancellationToken)
        {
            var inputLayer = SelectedInputLayer as FeatureLayer;
            if (inputLayer == null)
            {
                throw new InvalidOperationException("输入图层无效");
            }

            await _fileStore.EnsureOutputDirectoryAsync(OutputFolder, cancellationToken);

            AddLog($"开始导出图层: {inputLayer.Name}");
            AddLog($"分组字段: {SelectedGroupField}");
            AddLog($"导出格式: {SelectedExportFormat}");
            AddLog($"输出文件夹: {OutputFolder}");

            if (SelectedExportFormat == "KML")
            {
                AddLog("KML格式将先由内置工具输出为KMZ，再自动提取为KML");
            }

            if (EnableLabel)
            {
                AddLog($"提示: 将按字段 [{SelectedLabelField}] 直接生成标注点");
            }

            var sourceSpatialReferenceName = await QueuedTask.Run(() =>
            {
                using var table = inputLayer.GetTable();
                var featureClass = table as FeatureClass;
                if (featureClass == null)
                {
                    return "未知";
                }

                var sourceSpatialReference = featureClass.GetDefinition().GetSpatialReference();
                return sourceSpatialReference?.Name ?? "未知";
            });

            AddLog($"源坐标系: {sourceSpatialReferenceName}");

            IsProgressIndeterminate = false;

            if (SelectedGroupField == NoGroupFieldOption)
            {
                await ExportAllFeatures(inputLayer, inputLayer.Name, cancellationToken);
            }
            else
            {
                await ExportByGroup(inputLayer, inputLayer.Name, cancellationToken);
            }
        }

        private async Task ExportAllFeatures(FeatureLayer inputLayer, string layerName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileName = SanitizeFileName(layerName);
            Progress = 20;

            await ExportLayerWithBuiltInToolAsync(inputLayer, fileName, cancellationToken);

            Progress = 100;
            AddLog($"导出完成: {fileName}.{GetOutputExtension()}");
        }

        private async Task ExportByGroup(FeatureLayer inputLayer, string layerName, CancellationToken cancellationToken)
        {
            var groupInfo = await CollectGroupInfoAsync(inputLayer, SelectedGroupField, cancellationToken);
            AddLog($"发现 {groupInfo.Groups.Count} 个分组");

            if (groupInfo.Groups.Count == 0)
            {
                Progress = 100;
                AddLog("没有可导出的分组记录");
                return;
            }

            for (int i = 0; i < groupInfo.Groups.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var group = groupInfo.Groups[i];
                var whereClause = BuildGroupWhereClause(SelectedGroupField, groupInfo.FieldType, group.RawValue);
                var fileName = SanitizeFileName($"{layerName}_{group.DisplayValue}");

                await ExportLayerWithBuiltInToolAsync(inputLayer, fileName, cancellationToken, whereClause);
                AddLog($"导出分组 [{group.DisplayValue}]: {fileName}.{GetOutputExtension()}");

                Progress = (double)(i + 1) / groupInfo.Groups.Count * 100;
            }
        }

        private async Task ExportLayerWithBuiltInToolAsync(
            object layerInput,
            string fileName,
            CancellationToken cancellationToken,
            string whereClause = null)
        {
            if (SelectedExportFormat == "KMZ")
            {
                var kmzPath = Path.Combine(OutputFolder, $"{fileName}.kmz");
                await ExecuteLayerToKmlAsync(layerInput, kmzPath, cancellationToken, whereClause);

                if (ShouldExportLabelsFromField())
                {
                    await AppendFieldLabelLayerToKmzAsync(layerInput, kmzPath, cancellationToken, whereClause);
                }

                return;
            }

            var kmlPath = Path.Combine(OutputFolder, $"{fileName}.kml");
            var tempKmzPath = _fileStore.CreateTemporaryKmzPath();

            try
            {
                await ExecuteLayerToKmlAsync(layerInput, tempKmzPath, cancellationToken, whereClause);

                if (ShouldExportLabelsFromField())
                {
                    await AppendFieldLabelLayerToKmzAsync(layerInput, tempKmzPath, cancellationToken, whereClause);
                }

                await _fileStore.ExtractDocKmlAsync(tempKmzPath, kmlPath, cancellationToken);
            }
            finally
            {
                await _fileStore.TryDeleteFileAsync(tempKmzPath);
            }
        }

        private bool ShouldExportLabelsFromField()
        {
            return EnableLabel && !string.IsNullOrWhiteSpace(SelectedLabelField);
        }

        private async Task<List<KmlLabelItem>> BuildLabelItemsAsync(
            object sourceLayerInput,
            string labelFieldName,
            CancellationToken cancellationToken,
            string whereClause = null)
        {
            return await QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var featureLayer = ResolveFeatureLayer(sourceLayerInput);
                if (featureLayer == null)
                {
                    return new List<KmlLabelItem>();
                }

                using var table = featureLayer.GetTable();
                var definition = table.GetDefinition();
                var labelField = definition.GetFields().FirstOrDefault(f => f.Name == labelFieldName);
                if (labelField == null)
                {
                    throw new InvalidOperationException($"标注字段不存在: {labelFieldName}");
                }

                if (table is not FeatureClass featureClass)
                {
                    return new List<KmlLabelItem>();
                }

                var wgs84 = SpatialReferenceBuilder.CreateSpatialReference(4326);
                var items = new List<KmlLabelItem>();

                var queryFilter = string.IsNullOrWhiteSpace(whereClause)
                    ? null
                    : new QueryFilter { WhereClause = whereClause };
                using var cursor = queryFilter == null ? featureClass.Search() : featureClass.Search(queryFilter);
                while (cursor.MoveNext())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var row = cursor.Current;
                    if (row is not Feature feature)
                    {
                        continue;
                    }

                    var rawLabel = row[labelFieldName];
                    var labelText = rawLabel?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(labelText))
                    {
                        continue;
                    }

                    var labelPoint = GetGeometryLabelPoint(feature.GetShape());
                    if (labelPoint == null || labelPoint.IsEmpty)
                    {
                        continue;
                    }

                    var wgs84Point = labelPoint;
                    if (wgs84Point.SpatialReference == null || wgs84Point.SpatialReference.Wkid != 4326)
                    {
                        wgs84Point = GeometryEngine.Instance.Project(wgs84Point, wgs84) as MapPoint;
                    }

                    if (wgs84Point == null || wgs84Point.IsEmpty)
                    {
                        continue;
                    }

                    items.Add(new KmlLabelItem
                    {
                        Text = labelText,
                        X = wgs84Point.X,
                        Y = wgs84Point.Y
                    });
                }

                return items;
            });
        }

        private static string MergeKmlWithLabelItems(string targetKml, IReadOnlyList<KmlLabelItem> labelItems, string labelFieldName)
        {
            const string kmlNs = "http://www.opengis.net/kml/2.2";

            var targetDoc = new XmlDocument();
            targetDoc.LoadXml(targetKml);

            var targetNsManager = new XmlNamespaceManager(targetDoc.NameTable);
            targetNsManager.AddNamespace("kml", kmlNs);

            var targetDocument = targetDoc.SelectSingleNode("/kml:kml/kml:Document", targetNsManager) as XmlElement;
            if (targetDocument == null)
            {
                return targetKml;
            }

            if (labelItems == null || labelItems.Count == 0)
            {
                return targetKml;
            }

            UpdatePlacemarkNamesFromField(targetDoc, targetNsManager, kmlNs, labelFieldName);

            EnsureLabelStyle(targetDoc, targetDocument, targetNsManager, kmlNs);

            var labelFolder = targetDoc.CreateElement("Folder", kmlNs);
            var nameElement = targetDoc.CreateElement("name", kmlNs);
            nameElement.InnerText = "标注";
            labelFolder.AppendChild(nameElement);

            foreach (var item in labelItems)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Text))
                {
                    continue;
                }

                var placemarkElement = targetDoc.CreateElement("Placemark", kmlNs);

                var placemarkName = targetDoc.CreateElement("name", kmlNs);
                placemarkName.InnerText = item.Text;
                placemarkElement.AppendChild(placemarkName);

                var styleUrl = targetDoc.CreateElement("styleUrl", kmlNs);
                styleUrl.InnerText = $"#{LabelStyleId}";
                placemarkElement.AppendChild(styleUrl);

                var pointElement = targetDoc.CreateElement("Point", kmlNs);
                var coordinateElement = targetDoc.CreateElement("coordinates", kmlNs);
                coordinateElement.InnerText =
                    $"{item.X.ToString(CultureInfo.InvariantCulture)},{item.Y.ToString(CultureInfo.InvariantCulture)},0";
                pointElement.AppendChild(coordinateElement);
                placemarkElement.AppendChild(pointElement);

                labelFolder.AppendChild(placemarkElement);
            }

            if (labelFolder.ChildNodes.Count > 1)
            {
                targetDocument.AppendChild(labelFolder);
            }

            return targetDoc.OuterXml;
        }

        private string BuildGroupWhereClause(string fieldName, FieldType fieldType, object rawValue)
        {
            var escapedFieldName = $"\"{fieldName.Replace("\"", "\"\"")}\"";
            if (rawValue == null)
            {
                return $"{escapedFieldName} IS NULL";
            }

            switch (fieldType)
            {
                case FieldType.String:
                    return $"{escapedFieldName} = '{EscapeSqlValue(rawValue.ToString())}'";

                case FieldType.Integer:
                case FieldType.SmallInteger:
                case FieldType.BigInteger:
                    return $"{escapedFieldName} = {Convert.ToString(rawValue, CultureInfo.InvariantCulture)}";

                default:
                    return $"{escapedFieldName} = '{EscapeSqlValue(rawValue.ToString())}'";
            }
        }

        private static string BuildGpErrorMessage(IGPResult result)
        {
            if (result?.Messages == null)
            {
                return "未返回详细错误信息";
            }

            var messages = result.Messages
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Text))
                .Select(m => m.Text)
                .ToList();

            return messages.Count == 0 ? "未返回详细错误信息" : string.Join(" | ", messages);
        }
    }
}
