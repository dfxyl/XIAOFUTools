using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

namespace XIAOFUTools.Features.Analysis.AreaCalculator
{
    internal partial class AreaCalculatorDockPaneViewModel
    {

        /// <summary>
        /// 执行面积计算
        /// </summary>
        private async Task ExecuteAsync()
        {
            if (SelectedPolygonLayer == null || string.IsNullOrEmpty(SelectedFieldName))
            {
                StatusMessage = "请选择面图层和字段。";
                return;
            }

            try
            {
                IsProcessing = true;
                CancelRequested = false;
                Progress = 0;
                IsProgressIndeterminate = true;
                StatusMessage = "正在计算面积...";
                LogContent = "";

                LogInfo($"开始计算面积 - 图层: {SelectedPolygonLayer.Name}, 字段: {SelectedFieldName}");
                LogInfo($"单位: {SelectedAreaUnit}, 小数位数: {DecimalPlaces}, 类型: {SelectedAreaType}");

                await QueuedTask.Run(() =>
                {
                    try
                    {
                        // 检查取消请求
                        if (CancelRequested)
                        {
                            LogWarning("操作已取消");
                            return;
                        }

                        // 获取要素类
                        var featureClass = SelectedPolygonLayer.GetFeatureClass();
                        if (featureClass == null)
                        {
                            LogError("无法获取要素类");
                            return;
                        }

                        // 获取要素总数
                        var totalCount = (long)featureClass.GetCount();
                        LogInfo($"共有 {totalCount} 个要素需要处理");

                        if (totalCount == 0)
                        {
                            if (PresentationServices.UiThread != null)
                            {
                                PresentationServices.UiThread.Invoke(() =>
                                {
                                    StatusMessage = "图层中没有可处理的要素。";
                                    Progress = 100;
                                });
                            }

                            return;
                        }

                        // 更新进度条为确定模式
                        if (PresentationServices.UiThread != null)
                        {
                            PresentationServices.UiThread.Invoke(() =>
                            {
                                IsProgressIndeterminate = false;
                                Progress = 0;
                            });
                        }

                        // 获取字段类型信息
                        FieldType targetFieldType = FieldType.Double;
                        try
                        {
                            var fieldDef = featureClass.GetDefinition().GetFields().FirstOrDefault(f => f.Name == SelectedFieldName);
                            if (fieldDef != null)
                            {
                                targetFieldType = fieldDef.FieldType;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogWarning($"获取字段类型失败: {ex.Message}");
                        }

                        // 处理要素并直接写入数据（无需手动开启编辑会话）
                        var updatedCount = ProcessFeatures(featureClass, totalCount, SelectedFieldName, SelectedAreaUnit, DecimalPlaces, SelectedAreaType, targetFieldType);

                        if (!CancelRequested)
                        {
                            LogInfo($"面积计算完成，共写入 {updatedCount} 个要素！");

                            if (PresentationServices.UiThread != null)
                            {
                                PresentationServices.UiThread.Invoke(() =>
                                {
                                    StatusMessage = $"处理完成，已更新 {updatedCount} 个要素。";
                                    Progress = 100;
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理过程中出错: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"执行出错: {ex.Message}");
                StatusMessage = $"执行出错: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
            }
        }

        /// <summary>
        /// 处理要素
        /// </summary>
        private int ProcessFeatures(FeatureClass featureClass, long totalCount,
            string fieldName, string areaUnit, int decimalPlaces, string areaType, FieldType targetFieldType)
        {
            var processedCount = 0L;
            var updatedCount = 0;
            var batchSize = 100; // 批处理大小

            using (var cursor = featureClass.Search(new QueryFilter(), false))
            {
                while (cursor.MoveNext())
                {
                    if (CancelRequested)
                    {
                        LogWarning("操作已取消");
                        break;
                    }

                    using (var feature = cursor.Current as Feature)
                    {
                        if (feature != null)
                        {
                            try
                            {
                                // 计算面积
                                var area = CalculateAreaByType(feature.GetShape() as Polygon, areaType);

                                // 转换单位
                                var convertedArea = ConvertAreaUnit(area, areaUnit);

                                // 格式化面积值
                                var formattedValue = FormatAreaValueByFieldType(convertedArea, decimalPlaces, targetFieldType);

                                // 直接写入目标字段（无需编辑操作）
                                feature[fieldName] = formattedValue;
                                feature.Store();
                                updatedCount++;
                            }
                            catch (Exception ex)
                            {
                                LogWarning($"处理要素 {feature.GetObjectID()} 时出错: {ex.Message}");
                            }

                            processedCount++;

                            // 更新进度
                            if (processedCount % batchSize == 0 || processedCount == totalCount)
                            {
                                var progressPercent = (int)((double)processedCount / totalCount * 100);

                                if (PresentationServices.UiThread != null)
                                {
                                    PresentationServices.UiThread.Invoke(() =>
                                    {
                                        Progress = progressPercent;
                                        StatusMessage = $"正在处理... ({processedCount}/{totalCount})";
                                    });
                                }

                                LogInfo($"已处理 {processedCount}/{totalCount} 个要素");
                            }
                        }
                    }
                }
            }

            return updatedCount;
        }
    }
}
