using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.IO;
using System.Linq;

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.OverlapCheck
{
    internal partial class OverlapCheckDockPaneViewModel
    {

        /// <summary>
        /// 创建输出要素类
        /// </summary>
        private async Task CreateOutputFeatureClass(List<OverlapInfo> overlaps, CancellationToken cancellationToken)
        {
            try
            {
                if (overlaps.Count == 0)
                {
                    AddLog("未发现重叠区域");
                    return;
                }

                AddLog("正在创建输出要素类...");

                string outputPath = OutputPath;
                if (string.IsNullOrEmpty(outputPath))
                {
                    AddLog("输出路径为空");
                    return;
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
                        AddLog($"获取空间参考失败: {ex.Message}");
                        throw;
                    }
                }

                // 使用通用工具解析输出路径
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_重叠区域" : "重叠区域";
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
                        AddLog("用户取消覆盖，操作已中止。");
                        return;
                    }

                    try
                    {
                        await OutputDatasetUtils.DeleteIfExistsAsync(info);
                        AddLog($"删除已存在的数据集: {info.CatalogPath}");
                    }
                    catch (Exception delEx)
                    {
                        AddLog($"删除已有数据集失败: {delEx.Message}");
                    }
                }

                // 创建要素类
                var fullFeatureClassPath = await OutputDatasetUtils.CreateFeatureClassAsync(info, "POLYGON", spatialReference);
                AddLog($"成功创建输出要素类: {fullFeatureClassPath}");
                var featureClassName = info.OutNameNoExt;

                // 添加字段
                var addFieldParams1 = Geoprocessing.MakeValueArray(
                    fullFeatureClassPath,
                    "OVERLAP_ID",
                    "LONG"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams1);

                var addFieldParams2 = Geoprocessing.MakeValueArray(
                    fullFeatureClassPath,
                    "AREA",
                    "DOUBLE"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams2);

                var addFieldParams3 = Geoprocessing.MakeValueArray(
                    fullFeatureClassPath,
                    "TOLERANCE",
                    "DOUBLE"
                );
                await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams3);

                // 添加选中的保留字段（全部作为文本字段）
                if (SelectedFields != null && SelectedFields.Count > 0)
                {
                    try
                    {
                        foreach (var fieldName in SelectedFields)
                        {
                            var addFieldParams = Geoprocessing.MakeValueArray(
                                fullFeatureClassPath,
                                fieldName,
                                "TEXT",
                                null, null, 255  // 设置为255字符长度的文本字段
                            );
                            await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams);
                            AddLog($"添加保留字段: {fieldName} (文本)");
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"添加保留字段失败: {ex.Message}");
                        throw;
                    }
                }

                AddLog($"成功创建输出要素类: {fullFeatureClassPath}");

                // 插入重叠区域要素
                await InsertOverlapFeatures(fullFeatureClassPath, overlaps, cancellationToken);

                AddLog($"重叠检查完成，发现 {overlaps.Count} 个重叠区域");
            }
            catch (Exception ex)
            {
                AddLog($"创建输出要素类时发生错误: {ex.Message}");
                throw;
            }
        }
    }
}
