using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
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
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.DataManagement.BoundaryPointGenerator.Infrastructure;
using XIAOFUTools.Shared.Presentation;
using SharedFieldSelectionDialog = XIAOFUTools.Shared.Presentation.Dialogs.FieldSelectionDialog;

namespace XIAOFUTools.Features.DataManagement.BoundaryPointGenerator
{
    internal partial class BoundaryPointGeneratorDockPaneViewModel
    {

        /// <summary>
        /// 刷新图层列表（供DockPane调用）
        /// </summary>
        public void RefreshLayers()
        {
            LoadPolygonLayers();
        }

        /// <summary>
        /// 更新所选要素数量提示
        /// </summary>
        private void UpdateSelectionInfo()
        {
            Task.Run(async () =>
            {
                var info = await SelectionUtils.GetSelectionInfoAsync(SelectedPolygonLayer);
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    UseSelection = SelectionUtils.RecommendUseSelection(UseSelection, info.HasSelection);
                    HasSelection = info.HasSelection;
                    SelectedCount = info.Count;
                    SelectionInfoText = info.InfoText;
                });
            });
        }

        /// <summary>
        /// 地图选择变化事件
        /// </summary>
        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            UpdateSelectionInfo();
        }

        /// <summary>
        /// 更新输出路径，默认命名：图层名称+SZD
        /// </summary>
        private void UpdateOutputPath()
        {
            string projectGDB = GetProjectGDBPath();
            string outputName;

            if (SelectedPolygonLayer != null)
            {
                // 格式：图层名称+SZD
                outputName = $"{SelectedPolygonLayer.Name}_SZD";
            }
            else
            {
                // 默认名称
                outputName = "四至点SZD";
            }

            if (!string.IsNullOrEmpty(projectGDB))
            {
                OutputPath = Path.Combine(projectGDB, outputName);
            }
            else
            {
                OutputPath = outputName;
            }
        }

        /// <summary>
        /// 处理面要素，生成四至坐标点
        /// </summary>
        private async Task ProcessPolygonFeatures(string outputFeatureClassPath)
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
                    // 收集所有要创建的点
                    var pointsToCreate = new List<(MapPoint point, Dictionary<string, object> attributes)>();

                    int totalFeatures = 0;
                    int processedFeatures = 0;

                    // 根据是否存在选择集且用户选择启用决定处理范围
                    bool useSelection = UseSelection && SelectedPolygonLayer.SelectionCount > 0;
                    if (useSelection)
                    {
                        totalFeatures = SelectedPolygonLayer.SelectionCount;
                        LogInfo($"检测到选择集: {totalFeatures} 个要素，将仅处理选择的要素。");
                    }
                    else
                    {
                        // 先计算总数（全部要素）
                        using (var countCursor = inputTable.Search())
                        {
                            while (countCursor.MoveNext())
                            {
                                totalFeatures++;
                            }
                        }
                        LogInfo($"未检测到选择集，将处理全部 {totalFeatures} 个要素。");
                    }

                    // 处理每个要素（优先处理选择集）
                    using (var cursor = useSelection
                        ? SelectedPolygonLayer.GetSelection().Search(new QueryFilter(), false)
                        : inputTable.Search())
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
                                // 获取面几何和要素ID
                                var polygon = feature.GetShape() as Polygon;
                                var featureOID = feature.GetObjectID();

                                LogInfo($"正在处理面要素 {featureOID}");

                                if (polygon != null)
                                {
                                    LogInfo($"面要素 {featureOID} 的几何信息: PartCount={polygon.PartCount}, Area={polygon.Area:F2}");

                                    // 按生成模式生成四至坐标点
                                    IReadOnlyList<ArcGisBoundaryPoint> boundaryPoints =
                                        _boundaryPointPlanner.Plan(
                                            polygon,
                                            SelectedGenerationMode == "四角点");

                                    LogInfo($"面要素 {featureOID} 生成了 {boundaryPoints.Count} 个四至点");

                                    // 收集点要素数据
                                    foreach (var point in boundaryPoints)
                                    {
                                        var attributes = new Dictionary<string, object>
                                        {
                                            ["源要素ID"] = featureOID,
                                            ["方向"] = point.Direction,
                                            // 按测绘习惯交换XY：X字段写入Y值，Y字段写入X值
                                            ["X坐标_米"] = point.Point.Y,
                                            ["Y坐标_米"] = point.Point.X
                                        };

                                        // 添加保留字段的值
                                        if (SelectedFields != null && SelectedFields.Count > 0)
                                        {
                                            foreach (var fieldName in SelectedFields)
                                            {
                                                try
                                                {
                                                    var fieldValue = feature[fieldName];
                                                    attributes[fieldName] = fieldValue;
                                                }
                                                catch (Exception ex)
                                                {
                                                    LogWarning($"获取字段 {fieldName} 的值失败: {ex.Message}");
                                                    attributes[fieldName] = null;
                                                }
                                            }
                                        }

                                        pointsToCreate.Add((point.Point, attributes));
                                        // 日志中也按测绘习惯显示为 (X=Y, Y=X)
                                        LogInfo($"  - {point.Direction}点: ({point.Point.Y:F2}, {point.Point.X:F2})");
                                    }
                                }
                                else
                                {
                                    LogWarning($"面要素 {featureOID} 的几何为空");
                                }
                            }
                            else
                            {
                                LogWarning($"第 {processedFeatures + 1} 个要素为空");
                            }

                            processedFeatures++;

                            // 更新进度
                            int progressValue = (int)((double)processedFeatures / totalFeatures * 100);
                            PresentationServices.UiThread.InvokeOrRun(() =>
                            {
                                Progress = progressValue;
                                IsProgressIndeterminate = false;
                            });

                            LogInfo($"已处理 {processedFeatures}/{totalFeatures} 个要素");
                        }
                    }

                    // 使用地理处理工具插入点
                    if (!CancelRequested && pointsToCreate.Count > 0)
                    {
                        LogInfo($"正在插入 {pointsToCreate.Count} 个点要素...");
                        await InsertPointFeatures(outputFeatureClassPath, pointsToCreate);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"处理面要素时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除日志
        /// </summary>
        private void ClearLog()
        {
            _logBuilder.Clear();
            LogContent = "";
        }

        /// <summary>
        /// 记录信息
        /// </summary>
        private void LogInfo(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logBuilder.AppendLine(logMessage);

            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent = _logBuilder.ToString();
            });
        }

        /// <summary>
        /// 记录警告
        /// </summary>
        private void LogWarning(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] 警告: {message}";
            _logBuilder.AppendLine(logMessage);

            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent = _logBuilder.ToString();
            });
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        private void LogError(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] 错误: {message}";
            _logBuilder.AppendLine(logMessage);

            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent = _logBuilder.ToString();
            });
        }

        /// <summary>
        /// 选择保留字段
        /// </summary>
        private void SelectFields()
        {
            if (SelectedPolygonLayer == null) return;

            try
            {
                QueuedTask.Run(() =>
                {
                    try
                    {
                        using (var table = SelectedPolygonLayer.GetTable())
                        {
                            if (table != null)
                            {
                                var definition = table.GetDefinition();
                                var fields = definition.GetFields().ToList();

                                PresentationServices.UiThread.InvokeOrRun(() =>
                                {
                                    var selectedFields = SharedFieldSelectionDialog.Select(fields, SelectedFields);
                                    if (selectedFields is not null)
                                    {
                                        SelectedFields = selectedFields.ToList();
                                        LogInfo($"已选择 {SelectedFields.Count} 个保留字段");
                                    }
                                });
                            }
                            else
                            {
                                LogError("无法获取图层表格");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"获取字段列表失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"获取字段列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private void ShowHelp()
        {
            var helpContent = "生成四至坐标点工具使用说明\n\n" +
                "功能描述：\n" +
                "根据输入的面要素生成四至坐标点（最东、最西、最南、最北点）。\n\n" +
                "参数说明：\n" +
                "• 面图层：选择要处理的面要素图层\n" +
                "• 输出四至点图层：指定输出点要素的位置和名称\n" +
                "• 保留原始字段：选择要从源图层复制到输出图层的字段\n\n" +
                "操作步骤：\n" +
                "1. 选择要处理的面图层\n" +
                "2. 设置输出四至点图层路径（默认输出到工程数据库）\n" +
                "3. 可选：点击\"选择字段...\"选择要保留的原始字段\n" +
                "4. 点击\"开始\"按钮执行处理\n" +
                "5. 处理过程中可点击\"停止\"按钮取消操作\n\n" +
                "输出结果：\n" +
                "生成的点要素包含以下字段：\n" +
                "• 源要素ID：源面要素的ObjectID\n" +
                "• 方向：标识点的方位（东、西、南、北）\n" +
                "• X坐标_米：点的X坐标值\n" +
                "• Y坐标_米：点的Y坐标值\n" +
                "• 选中的原始字段：从源图层复制的字段值\n\n" +
                "注意事项：\n" +
                "• 输出到文件夹时将创建Shapefile格式\n" +
                "• 输出到地理数据库时将创建要素类\n" +
                "• 四至点基于面要素本身折点的最东西南北坐标计算\n" +
                "• 每个面要素将生成4个点（东、西、南、北各一个）\n" +
                "• 保留字段的值会复制到每个生成的四至点";

            PresentationServices.Dialogs.Show(helpContent, "帮助", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
    }
}
