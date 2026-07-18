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
        /// 执行节点距离检查
        /// </summary>
        private async void Execute()
        {
            if (IsProcessing)
            {
                LogWarning("工具正在运行中，请等待完成后再次执行");
                return;
            }

            CancelRequested = false;
            IsProcessing = true;
            StatusMessage = "正在处理...";
            ClearLog();
            Progress = 0;
            IsProgressIndeterminate = true;

            try
            {
                LogInfo("开始节点距离检查...");

                await QueuedTask.Run(async () =>
                {
                    try
                    {
                        if (CancelRequested)
                        {
                            LogWarning("操作已取消");
                            return;
                        }

                        // 创建输出要素类
                        var outputFeatureClassPath = await CreateOutputFeatureClass();
                        if (outputFeatureClassPath == null)
                        {
                            LogError("创建输出要素类失败");
                            return;
                        }

                        LogInfo($"成功创建输出要素类: {outputFeatureClassPath}");

                        // 处理面要素，检查节点距离
                        await ProcessNodeDistanceCheck(outputFeatureClassPath);

                        if (!CancelRequested)
                        {
                            LogInfo("节点距离检查完成！");

                            PresentationServices.UiThread.InvokeOrRun(() =>
                            {
                                StatusMessage = "处理完成！";
                                    Progress = 100;
                                    IsProgressIndeterminate = false;
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理过程中发生错误: {ex.Message}");
                        PresentationServices.UiThread.InvokeOrRun(() =>
                        {
                            StatusMessage = $"处理失败: {ex.Message}";
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"执行失败: {ex.Message}");
                StatusMessage = $"执行失败: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
            }
        }

        /// <summary>
        /// 处理节点距离检查
        /// </summary>
        private async Task ProcessNodeDistanceCheck(string outputFeatureClassPath)
        {
            try
            {
                using (var inputTable = SelectedPolygonLayer.GetTable())
                {
                    if (inputTable == null)
                    {
                        LogError("无法获取输入图层的表格");
                        return;
                    }

                    var linesToCreate = new List<(Polyline line, Dictionary<string, object> attributes)>();
                    int totalFeatures = 0;
                    int processedFeatures = 0;

                    // 计算总数
                    using (var countCursor = inputTable.Search())
                    {
                        while (countCursor.MoveNext())
                        {
                            totalFeatures++;
                        }
                    }

                    LogInfo($"共找到 {totalFeatures} 个面要素");

                    // 处理每个要素
                    using (var cursor = inputTable.Search())
                    {
                        while (cursor.MoveNext())
                        {
                            if (CancelRequested)
                            {
                                LogWarning("操作已取消");
                                break;
                            }

                            var feature = cursor.Current as Feature;
                            if (feature != null)
                            {
                                var polygon = feature.GetShape() as Polygon;
                                var featureOID = feature.GetObjectID();

                                if (polygon != null)
                                {
                                    // 检查节点距离
                                    var resultLines = CheckNodeDistances(polygon, featureOID, feature);
                                    linesToCreate.AddRange(resultLines);

                                    LogInfo($"面要素 {featureOID} 检查完成，找到 {resultLines.Count} 条符合条件的线段");
                                }
                            }

                            processedFeatures++;

                            // 更新进度
                            int progressValue = (int)((double)processedFeatures / totalFeatures * 100);
                            PresentationServices.UiThread.InvokeOrRun(() =>
                            {
                                Progress = progressValue;
                                    IsProgressIndeterminate = false;
                            });
                        }
                    }

                    // 插入线要素
                    if (!CancelRequested && linesToCreate.Count > 0)
                    {
                        LogInfo($"正在插入 {linesToCreate.Count} 条线要素...");
                        await InsertLineFeatures(outputFeatureClassPath, linesToCreate);
                    }
                    else if (linesToCreate.Count == 0)
                    {
                        LogInfo("未找到符合条件的节点距离");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"处理节点距离检查时发生错误: {ex.Message}");
            }
        }
    }
}
