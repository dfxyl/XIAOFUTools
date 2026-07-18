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

        private GroupPreparationResult PrepareFieldGroups(FeatureLayer layer, string fieldName)
        {
            var result = new GroupPreparationResult { FieldName = fieldName };

            using (var table = layer.GetTable())
            {
                var definition = table.GetDefinition();
                var targetField = definition.GetFields().FirstOrDefault(f => f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                if (targetField == null)
                    throw new InvalidOperationException($"字段 {fieldName} 不存在。");

                result.FieldType = targetField.FieldType;

                var groups = new Dictionary<FieldGroupKey, FieldGroupInfo>();
                var nameUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                using (var cursor = table.Search(null, false))
                {
                    while (cursor.MoveNext())
                    {
                        if (CancelRequested)
                            throw new OperationCanceledException();

                        using (var row = cursor.Current)
                        {
                            var rawValue = row[fieldName];
                            var key = new FieldGroupKey(rawValue, targetField.FieldType);

                            if (!groups.TryGetValue(key, out var info))
                            {
                                var displayValue = FormatGroupDisplayValue(rawValue);
                                var safeName = BuildUniqueGroupName(displayValue, nameUsage);

                                info = new FieldGroupInfo
                                {
                                    Key = key,
                                    DisplayValue = displayValue,
                                    OutputName = safeName,
                                    FeatureCount = 0
                                };
                                groups[key] = info;
                            }

                            info.FeatureCount++;
                        }
                    }
                }

                result.Groups = groups.Values
                    .OrderBy(g => g.DisplayValue, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            return result;
        }

        /// <summary>
        /// 清理文件名中的非法字符，确保文件名有效
        /// </summary>
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "未知";
            }

            // 替换文件名中的非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // 替换其他可能导致问题的字符（保持最小替换）
            fileName = fileName.Replace("\\", "_");
            fileName = fileName.Replace("/", "_");
            fileName = fileName.Replace(":", "_");
            fileName = fileName.Replace("*", "_");
            fileName = fileName.Replace("?", "_");
            fileName = fileName.Replace("\"", "_");
            fileName = fileName.Replace("<", "_");
            fileName = fileName.Replace(">", "_");
            fileName = fileName.Replace("|", "_");

            // 移除多余的下划线
            while (fileName.Contains("__"))
            {
                fileName = fileName.Replace("__", "_");
            }

            // 移除文件名开头和末尾的下划线
            fileName = fileName.Trim('_');

            // 如果文件名为空，使用默认值
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = "未知";
            }

            return fileName;
        }
    }
}
