using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using Microsoft.Win32;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.Conversion.FeatureToTxt.Core;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    internal partial class FeatureToTxtDockPaneViewModel
    {

        /// <summary>
        /// 获取字段描述
        /// </summary>
        private string GetFieldDescription(string fieldName)
        {
            switch (fieldName)
            {
                case "点数": return "自动根据下面点数生成";
                case "图形类型": return "自动根据类型输出：点/线/面";
                case "公顷4位": return "动态计算面积(公顷)，保留4位小数";
                case "公顷6位": return "动态计算面积(公顷)，保留6位小数";
                case "@": return "结束标记";
                default: return "自动读取字段值";
            }
        }



        /// <summary>
        /// 加载面图层
        /// </summary>
        private void LoadPolygonLayers()
        {
            LogInfo("开始加载面图层...");
            StatusMessage = "正在加载图层...";

            _ = Task.Run(async () =>
            {
                try
                {
                    var tempLayers = await LayerUtils.GetPolygonLayersAsync();

                    await PresentationServices.UiThread.InvokeAsync(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var layer in tempLayers)
                        {
                            PolygonLayers.Add(layer);
                        }

                        if (PolygonLayers.Count > 0)
                        {
                            SelectedPolygonLayer = PolygonLayers[0];
                            StatusMessage = $"成功加载 {PolygonLayers.Count} 个面图层";
                        }
                        else
                        {
                            StatusMessage = "未找到面图层，请确保地图中包含面要素图层";
                        }

                        // 强制刷新UI绑定
                        NotifyPropertyChanged(() => PolygonLayers);
                        NotifyPropertyChanged(() => SelectedPolygonLayer);
                        NotifyPropertyChanged(() => HasSelectedLayer);
                    });
                }
                catch (Exception ex)
                {
                    await PresentationServices.UiThread.InvokeAsync(() =>
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                        LogError($"加载图层出错: {ex.Message}");
                        LogError($"异常详情: {ex}");
                    });
                }
            });
        }

        /// <summary>
        /// 加载字段信息并进行自动匹配
        /// </summary>
        private void LoadFieldNames()
        {
            if (SelectedPolygonLayer == null)
            {
                FieldInfos?.Clear();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldDisplayInfo>();
                    var matchingResults = new List<string>();

                    await QueuedTask.Run(() =>
                    {
                        var featureClass = SelectedPolygonLayer.GetFeatureClass();
                        if (featureClass != null)
                        {
                            var definition = featureClass.GetDefinition();
                            var fields = definition.GetFields();
                            var fieldNames = fields.Select(f => f.Name).ToList();

                            // 收集所有字段信息
                            foreach (var field in fields)
                            {
                                if (field.FieldType == FieldType.Double ||
                                    field.FieldType == FieldType.Single ||
                                    field.FieldType == FieldType.Integer ||
                                    field.FieldType == FieldType.SmallInteger ||
                                    field.FieldType == FieldType.String)
                                {
                                    var fieldInfo = new FieldDisplayInfo
                                    {
                                        FieldName = field.Name,
                                        Alias = field.AliasName,
                                        FieldType = GetFieldTypeDisplayName(field.FieldType)
                                    };
                                    tempFieldInfos.Add(fieldInfo);
                                }
                            }

                            // 进行字段自动匹配
                            LogInfo("开始进行字段自动匹配...");
                            foreach (var mapping in FeatureToTxtFieldCatalog.Mappings)
                            {
                                var matchedField = FindMatchingField(fieldNames, mapping.Value);
                                if (!string.IsNullOrEmpty(matchedField))
                                {
                                    matchingResults.Add($"✓ {mapping.Key}: {matchedField}");
                                    LogInfo($"字段匹配成功 - {mapping.Key}: {matchedField}");
                                }
                                else
                                {
                                    matchingResults.Add($"✗ {mapping.Key}: 未找到匹配字段");
                                    LogWarning($"字段匹配失败 - {mapping.Key}: 未找到匹配字段");
                                }
                            }
                        }
                    });

                    // 在UI线程更新字段列表和匹配结果
                    await PresentationServices.UiThread.InvokeAsync(() =>
                    {
                        FieldInfos?.Clear();
                        AvailableFields?.Clear();
                        GroupableFields?.Clear();

                        if (FieldInfos != null && AvailableFields != null && GroupableFields != null)
                        {
                            // 添加实际字段
                            foreach (var fieldInfo in tempFieldInfos)
                            {
                                FieldInfos.Add(fieldInfo);
                                AvailableFields.Add(fieldInfo.FieldName);
                                GroupableFields.Add(fieldInfo.FieldName);  // 只添加实际字段到分组列表
                            }

                            // 添加分隔线
                            if (tempFieldInfos.Count > 0)
                            {
                                AvailableFields.Add("--- 预设字段 ---");
                            }

                            // 添加特殊字段（仅用于配置输出字段，不用于分组）
                            AvailableFields.Add("点数");
                            AvailableFields.Add("图形类型");
                            AvailableFields.Add("公顷4位");
                            AvailableFields.Add("公顷6位");
                            AvailableFields.Add(",");
                            AvailableFields.Add("@");
                        }

                        // 记录匹配结果
                        LogInfo($"字段匹配完成，共匹配 {matchingResults.Count(r => r.StartsWith("✓"))} 个字段");
                        LogInfo($"可用字段数量: {AvailableFields?.Count ?? 0}");
                        LogInfo($"可分组字段数量: {GroupableFields?.Count ?? 0}");
                        
                        // 显式通知GroupableFields属性已更新
                        NotifyPropertyChanged(nameof(GroupableFields));
                    });
                }
                catch (Exception ex)
                {
                    await PresentationServices.UiThread.InvokeAsync(() =>
                    {
                        StatusMessage = $"加载字段出错: {ex.Message}";
                        LogError($"加载字段出错: {ex.Message}");
                    });
                }
            });
        }

        /// <summary>
        /// 查找匹配的字段名
        /// </summary>
        private string FindMatchingField(List<string> availableFields, IReadOnlyList<string> possibleNames)
        {
            foreach (var possibleName in possibleNames)
            {
                // 精确匹配
                var exactMatch = availableFields.FirstOrDefault(f =>
                    string.Equals(f, possibleName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(exactMatch))
                {
                    return exactMatch;
                }

                // 包含匹配
                var containsMatch = availableFields.FirstOrDefault(f =>
                    f.IndexOf(possibleName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrEmpty(containsMatch))
                {
                    return containsMatch;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取字段类型的显示名称
        /// </summary>
        private string GetFieldTypeDisplayName(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.Double => "双精度",
                FieldType.Single => "单精度",
                FieldType.Integer => "整型",
                FieldType.SmallInteger => "短整型",
                FieldType.String => "文本",
                _ => "未知类型"
            };
        }

        /// <summary>
        /// 加载头部信息配置
        /// </summary>
        private void LoadHeaderConfigs()
        {
            try
            {
                var configs = HeaderConfigManager.LoadConfigs();
                HeaderConfigs = new ObservableCollection<HeaderConfig>(configs);

                // 选择第一个配置
                if (HeaderConfigs.Count > 0)
                {
                    SelectedHeaderConfig = HeaderConfigs[0];
                    LogInfo($"已加载 {HeaderConfigs.Count} 个头部配置，当前选择: {SelectedHeaderConfig.Name}");
                }
            }
            catch (Exception ex)
            {
                LogError($"加载头部配置失败: {ex.Message}");
                // 加载失败时使用默认配置
                HeaderConfigs = new ObservableCollection<HeaderConfig> { HeaderConfigManager.GetDefaultConfig() };
                SelectedHeaderConfig = HeaderConfigs[0];
            }
        }

        /// <summary>
        /// 获取安全的文件名（移除非法字符）
        /// </summary>
        private string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return "未命名";
            }

            // 替换文件名中的非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // 替换其他可能导致问题的字符
            fileName = fileName.Replace("\\", "_")
                              .Replace("/", "_")
                              .Replace(":", "_")
                              .Replace("*", "_")
                              .Replace("?", "_")
                              .Replace("\"", "_")
                              .Replace("<", "_")
                              .Replace(">", "_")
                              .Replace("|", "_");

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
                fileName = "未命名";
            }

            // 限制文件名长度
            if (fileName.Length > 100)
            {
                fileName = fileName.Substring(0, 100);
            }

            return fileName;
        }

    }
}
