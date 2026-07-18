using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using System.Globalization;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace XIAOFUTools.Features.DataManagement.BatchLayerClip
{
    internal partial class BatchLayerClipViewModel
    {

        private async Task<bool> ExportGroupAsync(FeatureLayer layer, string outputFolder, string shapeName, string whereClause, IEnumerable<KeyValuePair<string, string>> environmentSettings)
        {
            var exportParams = Geoprocessing.MakeValueArray(
                layer,
                outputFolder,
                shapeName,
                whereClause,
                "");

            using (var cts = new CancellationTokenSource())
            {
                _geoprocessingCancellationSource = cts;

                try
                {
                    var gpResult = await Geoprocessing.ExecuteToolAsync(
                        "conversion.FeatureClassToFeatureClass",
                        exportParams,
                        environmentSettings,
                        cts.Token,
                        null,
                        GPExecuteToolFlags.GPThread);

                    if (gpResult == null)
                    {
                        LogError($"导出失败: GP 结果为空 ({shapeName})");
                        return false;
                    }

                    if (gpResult.IsFailed)
                    {
                        LogError($"导出失败: {shapeName}");
                        foreach (var msg in gpResult.Messages)
                        {
                            switch (msg.Type)
                            {
                                case GPMessageType.Error:
                                    LogError($"GP错误: {msg.Text}");
                                    break;
                                case GPMessageType.Warning:
                                    LogWarning($"GP警告: {msg.Text}");
                                    break;
                                default:
                                    LogInfo($"GP信息: {msg.Text}");
                                    break;
                            }
                        }
                        return false;
                    }

                    foreach (var msg in gpResult.Messages)
                    {
                        if (msg.Type == GPMessageType.Warning)
                            LogWarning($"GP警告: {msg.Text}");
                    }

                    LogInfo($"导出成功: {shapeName}");
                    return true;
                }
                catch (OperationCanceledException)
                {
                    LogWarning($"地理处理任务已取消: {shapeName}");
                    return false;
                }
                catch (Exception ex)
                {
                    if (CancelRequested)
                    {
                        LogWarning($"由于用户取消，跳过 {shapeName}");
                        return false;
                    }

                    LogError($"导出要素出错: {ex.Message}");
                    return false;
                }
                finally
                {
                    _geoprocessingCancellationSource = null;
                }
            }
        }

        private string BuildWhereClause(string fieldName, FieldType fieldType, FieldGroupInfo group)
        {
            if (group.Key.IsNull)
                return $"{fieldName} IS NULL";

            switch (fieldType)
            {
                case FieldType.String:
                    var textValue = group.Key.Value?.ToString() ?? string.Empty;
                    var escaped = textValue.Replace("'", "''");
                    return $"{fieldName} = '{escaped}'";
                case FieldType.Integer:
                case FieldType.SmallInteger:
                    var longValue = Convert.ToInt64(group.Key.Value, CultureInfo.InvariantCulture);
                    return $"{fieldName} = {longValue}";
                case FieldType.Double:
                case FieldType.Single:
                    var doubleValue = Convert.ToDouble(group.Key.Value, CultureInfo.InvariantCulture);
                    return $"{fieldName} = {doubleValue.ToString("R", CultureInfo.InvariantCulture)}";
                default:
                    var fallback = group.Key.Value?.ToString()?.Replace("'", "''") ?? string.Empty;
                    return $"{fieldName} = '{fallback}'";
            }
        }

        private string BuildUniqueGroupName(string displayValue, Dictionary<string, int> usageTracker)
        {
            var baseName = SanitizeFileName(displayValue);

            if (!usageTracker.TryGetValue(baseName, out var count))
            {
                usageTracker[baseName] = 1;
                return baseName;
            }

            count++;
            usageTracker[baseName] = count;
            return $"{baseName}_{count}";
        }

        private string FormatGroupDisplayValue(object value)
        {
            var displayValue = value?.ToString();
            return string.IsNullOrWhiteSpace(displayValue) ? "未知" : displayValue;
        }
    }
}
