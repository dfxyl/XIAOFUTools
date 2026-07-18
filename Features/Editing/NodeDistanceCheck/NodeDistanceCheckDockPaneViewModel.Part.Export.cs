using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Editing;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.NodeDistanceCheck
{
    internal partial class NodeDistanceCheckDockPaneViewModel
    {

        /// <summary>
        /// 创建输出要素类
        /// </summary>
        private async Task<string> CreateOutputFeatureClass()
        {
            try
            {
                string outputPath = OutputPath;
                if (string.IsNullOrEmpty(outputPath))
                {
                    LogError("输出路径为空");
                    return null;
                }

                // 获取输入图层的空间参考
                SpatialReference spatialReference = null;
                if (SelectedPolygonLayer != null)
                {
                    try
                    {
                        using (var table = SelectedPolygonLayer.GetTable())
                        {
                            if (table != null)
                            {
                                var definition = table.GetDefinition() as FeatureClassDefinition;
                                spatialReference = definition?.GetSpatialReference();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"获取空间参考失败: {ex.Message}");
                        throw;
                    }
                }

                // 使用通用工具解析输出路径
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_节点距离检查" : "节点距离检查结果";
                var info = OutputDatasetUtils.ParseOutputPath(outputPath, defName);

                // 覆盖处理
                if (OutputDatasetUtils.Exists(info))
                {
                    bool overwrite = false;
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        var msg = info.IsGdb
                                ? $"目标要素类已存在：{info.CatalogPath}。是否覆盖？"
                                : $"目标Shapefile已存在：{info.CatalogPath}。是否覆盖？";
                            var result = PresentationServices.Dialogs.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                            overwrite = result == System.Windows.MessageBoxResult.Yes;
                    });
                    if (!overwrite)
                    {
                        LogWarning("用户取消覆盖，操作已中止。");
                        return null;
                    }

                    try
                    {
                        await OutputDatasetUtils.DeleteIfExistsAsync(info);
                        LogInfo($"删除已存在的数据集: {info.CatalogPath}");
                    }
                    catch (Exception delEx)
                    {
                        LogWarning($"删除已有数据集失败: {delEx.Message}");
                    }
                }

                // 创建要素类
                var featureClassPath = await OutputDatasetUtils.CreateFeatureClassAsync(info, "POLYLINE", spatialReference);
                LogInfo($"成功创建输出要素类: {featureClassPath}");

                // 添加字段
                var addFieldParams1 = Geoprocessing.MakeValueArray(
                    featureClassPath,
                    "源要素ID",
                    "LONG"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams1);

                var addFieldParams2 = Geoprocessing.MakeValueArray(
                    featureClassPath,
                    "节点距离",
                    "DOUBLE"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams2);

                var addFieldParams3 = Geoprocessing.MakeValueArray(
                    featureClassPath,
                    "检查条件",
                    "TEXT",
                    null, null, 20
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams3);

                // 添加选中的保留字段
                if (SelectedFields != null && SelectedFields.Count > 0)
                {
                    try
                    {
                        using (var table = SelectedPolygonLayer.GetTable())
                        {
                            if (table != null)
                            {
                                var definition = table.GetDefinition();
                                var sourceFields = definition.GetFields();

                                foreach (var fieldName in SelectedFields)
                                {
                                    var sourceField = sourceFields.FirstOrDefault(f => f.Name == fieldName);
                                    if (sourceField != null)
                                    {
                                        var fieldType = GetGeoprocessingFieldType(sourceField.FieldType);
                                        var addFieldParams = Geoprocessing.MakeValueArray(
                                            featureClassPath,
                                            sourceField.Name,
                                            fieldType,
                                            null, null, sourceField.Length > 0 ? sourceField.Length : (object)null
                                        );
                                        await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams);
                                        LogInfo($"添加保留字段: {sourceField.Name} ({fieldType})");
                                    }
                                }
                            }
                            else
                            {
                                LogError("无法获取图层表格");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"添加保留字段失败: {ex.Message}");
                        throw;
                    }
                }

                return featureClassPath;
            }
            catch (Exception ex)
            {
                LogError($"创建输出要素类时发生错误: {ex.Message}");
                return null;
            }
        }
    }
}
