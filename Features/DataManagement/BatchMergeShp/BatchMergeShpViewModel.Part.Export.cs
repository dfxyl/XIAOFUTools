using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BatchMergeShp
{
    internal partial class BatchMergeShpViewModel
    {

        private async Task CreateSourceFileNameFieldAsync(string outputCatalogPath, CancellationToken token)
        {
            var fieldNames = await GetFieldNameSetAsync(outputCatalogPath);
            if (!fieldNames.Contains("MERGE_SRC"))
            {
                AppendWarning("未找到 MERGE_SRC 字段，跳过“源文件名”字段创建。");
                return;
            }

            bool isShapefile = outputCatalogPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase);
            var newFieldName = BuildAvailableFieldName(fieldNames, isShapefile ? "SRC_FILE" : "SourceFile", isShapefile ? 10 : 64);

            AppendInfo($"创建源文件名字段: {newFieldName}");
            var addFieldParams = Geoprocessing.MakeValueArray(outputCatalogPath, newFieldName, "TEXT", null, null, 254, "源文件名");
            var addFieldResult = await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams, null, token);
            if (addFieldResult.IsFailed)
            {
                AppendWarning($"创建源文件名字段失败: {GetGpMessageText(addFieldResult)}");
                return;
            }

            var expression = "GetName(!MERGE_SRC!)";
            var codeBlock = @"import os
def GetName(path):
    if path is None:
        return None
    text = str(path).strip()
    if not text:
        return None
    text = text.split(';')[0]
    base = os.path.basename(text)
    name, _ = os.path.splitext(base)
    return name";
            var calcFieldParams = Geoprocessing.MakeValueArray(outputCatalogPath, newFieldName, expression, "PYTHON3", codeBlock);
            var calcFieldResult = await Geoprocessing.ExecuteToolAsync("CalculateField_management", calcFieldParams, null, token);
            if (calcFieldResult.IsFailed)
            {
                AppendWarning($"写入源文件名字段失败: {GetGpMessageText(calcFieldResult)}");
                return;
            }

            AppendInfo("源文件名字段创建并写入完成。");
        }

        private static string BuildAvailableFieldName(ISet<string> existingFieldNames, string baseName, int maxLength)
        {
            var normalizedBase = new string((baseName ?? string.Empty)
                .Where(ch => char.IsLetterOrDigit(ch) || ch == '_')
                .ToArray());

            if (string.IsNullOrWhiteSpace(normalizedBase))
            {
                normalizedBase = "SRC_FILE";
            }

            if (normalizedBase.Length > maxLength)
            {
                normalizedBase = normalizedBase.Substring(0, maxLength);
            }

            var candidate = normalizedBase;
            int suffix = 1;
            while (existingFieldNames.Contains(candidate))
            {
                var suffixText = "_" + suffix;
                int prefixLength = Math.Max(1, maxLength - suffixText.Length);
                var prefix = normalizedBase.Length > prefixLength
                    ? normalizedBase.Substring(0, prefixLength)
                    : normalizedBase;
                candidate = prefix + suffixText;
                suffix++;
            }

            return candidate;
        }
    }
}
