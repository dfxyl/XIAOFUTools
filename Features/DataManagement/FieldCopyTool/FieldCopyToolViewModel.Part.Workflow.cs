using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using System.Threading;
using ArcGIS.Desktop.Core.Geoprocessing;

namespace XIAOFUTools.Features.DataManagement.FieldCopyTool
{
    public partial class FieldCopyToolViewModel
    {

        /// <summary>
        /// 判断是否可以开始复制
        /// </summary>
        private bool CanStartCopy()
        {
            return !IsProcessing &&
                   SelectedSourceLayer != null &&
                   FieldList.Any(f => f.IsSelected) &&
                   TargetLayerList.Any(l => l.IsSelected);
        }

        /// <summary>
        /// 开始复制字段
        /// </summary>
        private async void StartCopyFields()
        {
            IsProcessing = true;
            LogText = "";
            LogInfo("开始复制字段...");

            try
            {
                var selectedFields = FieldList.Where(f => f.IsSelected).ToList();
                var selectedTargetLayers = TargetLayerList.Where(l => l.IsSelected).ToList();

                LogInfo($"源图层: {SelectedSourceLayer.Name}");
                LogInfo($"选中字段数: {selectedFields.Count}");
                LogInfo($"目标图层数: {selectedTargetLayers.Count}");

                await QueuedTask.Run(async () =>
                {
                    foreach (var targetLayerInfo in selectedTargetLayers)
                    {
                        var targetLayer = targetLayerInfo.Layer;
                        LogInfo($"正在处理目标图层: {targetLayer.Name}");

                        try
                        {
                            using (var targetTable = targetLayer.GetTable())
                            {
                                var targetDefinition = targetTable.GetDefinition();
                                var existingFields = targetDefinition.GetFields().ToDictionary(f => f.Name.ToUpper(), f => f);

                                // 收集要添加的字段
                                var fieldsToAdd = new List<ArcGIS.Core.Data.DDL.FieldDescription>();

                                foreach (var fieldInfo in selectedFields)
                                {
                                    if (existingFields.ContainsKey(fieldInfo.Name.ToUpper()))
                                    {
                                        LogWarning($"字段 {fieldInfo.Name} 在图层 {targetLayer.Name} 中已存在，跳过");
                                        continue;
                                    }

                                    // 创建新字段描述
                                    var fieldDescription = new ArcGIS.Core.Data.DDL.FieldDescription(fieldInfo.Name, fieldInfo.FieldType);
                                    fieldDescription.AliasName = fieldInfo.Alias;

                                    // 保留源字段的长度（如果有）
                                    if (fieldInfo.FieldType == FieldType.String && fieldInfo.Length > 0)
                                    {
                                        fieldDescription.Length = fieldInfo.Length;
                                    }
                                    else if (fieldInfo.FieldType == FieldType.String)
                                    {
                                        fieldDescription.Length = 255; // 默认字符串长度
                                    }

                                    fieldsToAdd.Add(fieldDescription);
                                }

                                if (fieldsToAdd.Count > 0)
                                {
                                    // 使用Geoprocessing工具添加字段，支持所有数据源类型（Shapefile、Geodatabase等）
                                    int successCount = 0;
                                    int failCount = 0;

                                    // 对于已加载到地图的图层，直接使用图层名称
                                    string targetLayerName = targetLayer.Name;

                                    foreach (var fieldDesc in fieldsToAdd)
                                    {
                                        try
                                        {
                                            // 将FieldType转换为Geoprocessing工具所需的字符串类型
                                            string fieldType = ConvertFieldTypeToString(fieldDesc.FieldType);
                                            
                                            // 构建AddField_management工具的参数
                                            var parameters = Geoprocessing.MakeValueArray(
                                                targetLayerName,
                                                fieldDesc.Name,
                                                fieldType,
                                                null, // precision
                                                null, // scale
                                                fieldDesc.Length > 0 ? (object)fieldDesc.Length : null, // length
                                                fieldDesc.AliasName // alias
                                            );

                                            var result = await Geoprocessing.ExecuteToolAsync("AddField_management", parameters);
                                            
                                            if (!result.IsFailed)
                                            {
                                                successCount++;
                                            }
                                            else
                                            {
                                                failCount++;
                                                var errorMsg = string.Join("; ", result.Messages.Where(m => m.Type == GPMessageType.Error).Select(m => m.Text));
                                                LogWarning($"字段 {fieldDesc.Name} 添加失败: {errorMsg}");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            failCount++;
                                            LogError($"添加字段 {fieldDesc.Name} 时出错: {ex.Message}");
                                        }
                                    }

                                    if (successCount > 0)
                                    {
                                        LogInfo($"成功在图层 {targetLayer.Name} 中添加 {successCount} 个字段");
                                    }
                                    if (failCount > 0)
                                    {
                                        LogWarning($"图层 {targetLayer.Name} 中有 {failCount} 个字段添加失败");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogError($"处理图层 {targetLayer.Name} 时出错: {ex.Message}");
                        }
                    }
                });

                LogInfo("字段复制完成！");
            }
            catch (Exception ex)
            {
                LogError($"复制字段时出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }
    }
}
