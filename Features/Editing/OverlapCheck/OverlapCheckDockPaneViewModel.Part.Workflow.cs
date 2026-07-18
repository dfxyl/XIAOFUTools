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

namespace XIAOFUTools.Features.Editing.OverlapCheck
{
    internal partial class OverlapCheckDockPaneViewModel
    {

        /// <summary>
        /// 执行重叠检查
        /// </summary>
        private async Task RunOverlapCheck()
        {
            if (SelectedPolygonLayer == null)
            {
                AddLog("请选择面要素图层");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                AddLog("请选择输出路径");
                return;
            }

            if (!double.TryParse(Tolerance, out double tolerance) || tolerance < 0)
            {
                AddLog("请输入有效的容差值");
                return;
            }

            try
            {
                IsProcessing = true;
                IsProgressIndeterminate = true;
                StatusMessage = "正在检查重叠...";
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = new CancellationTokenSource();

                AddLog("开始执行图形重叠检查...");
                AddLog($"输入图层: {SelectedPolygonLayer.Name}");
                AddLog($"容差值: {tolerance} 米");
                AddLog($"输出路径: {OutputPath}");

                await QueuedTask.Run(async () =>
                {
                    await ProcessOverlapCheck(tolerance, _cancellationTokenSource.Token);
                });

                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    StatusMessage = "重叠检查完成";
                    AddLog("图形重叠检查完成！");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
                AddLog("操作已被用户取消");
            }
            catch (Exception ex)
            {
                StatusMessage = "处理失败";
                AddLog($"处理过程中出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                Progress = 0;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 处理重叠检查核心逻辑
        /// </summary>
        private async Task ProcessOverlapCheck(double tolerance, CancellationToken cancellationToken)
        {
            using (var table = SelectedPolygonLayer.GetTable())
            {
                // 获取所有要素及其字段值
                var features = new List<(long ObjectID, Geometry Geometry, Dictionary<string, object> Fields)>();

                using (var cursor = table.Search())
                {
                    while (cursor.MoveNext())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        using (var feature = cursor.Current as Feature)
                        {
                            if (feature?.GetShape() != null)
                            {
                                var fieldValues = new Dictionary<string, object>();

                                // 如果有选中的保留字段，读取字段值
                                if (SelectedFields != null && SelectedFields.Count > 0)
                                {
                                    foreach (var fieldName in SelectedFields)
                                    {
                                        try
                                        {
                                            var value = feature[fieldName];
                                            fieldValues[fieldName] = value;
                                        }
                                        catch (Exception ex)
                                        {
                                            AddLog($"读取字段 {fieldName} 失败: {ex.Message}");
                                            fieldValues[fieldName] = null;
                                        }
                                    }
                                }

                                features.Add((feature.GetObjectID(), feature.GetShape(), fieldValues));
                            }
                        }
                    }
                }

                AddLog($"共读取 {features.Count} 个要素");

                // 检查重叠
                var overlaps = new List<OverlapInfo>();
                int totalComparisons = features.Count * (features.Count - 1) / 2;
                int currentComparison = 0;

                for (int i = 0; i < features.Count; i++)
                {
                    for (int j = i + 1; j < features.Count; j++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        currentComparison++;
                        if (currentComparison % 100 == 0)
                        {
                            Progress = (int)((double)currentComparison / totalComparisons * 100);
                            await Task.Delay(1, cancellationToken); // 让UI有机会更新
                        }

                        var feature1 = features[i];
                        var feature2 = features[j];

                        if (GeometryEngine.Instance.Intersects(feature1.Geometry, feature2.Geometry))
                        {
                            var intersection = GeometryEngine.Instance.Intersection(feature1.Geometry, feature2.Geometry);
                            if (intersection != null && intersection.IsEmpty == false)
                            {
                                // 检查重叠面积是否大于容差
                                if (intersection is Polygon polygon && polygon.Area > tolerance)
                                {
                                    var overlapInfo = new OverlapInfo
                                    {
                                        Geometry = intersection,
                                        Feature1ObjectID = feature1.ObjectID,
                                        Feature2ObjectID = feature2.ObjectID
                                    };

                                    // 合并字段值
                                    if (SelectedFields != null && SelectedFields.Count > 0)
                                    {
                                        foreach (var fieldName in SelectedFields)
                                        {
                                            var value1 = feature1.Fields.ContainsKey(fieldName) ? feature1.Fields[fieldName]?.ToString() ?? "" : "";
                                            var value2 = feature2.Fields.ContainsKey(fieldName) ? feature2.Fields[fieldName]?.ToString() ?? "" : "";

                                            // 合并字段值，使用 / 分隔
                                            var combinedValue = $"{value1}/{value2}";
                                            overlapInfo.Feature1Fields[fieldName] = combinedValue;
                                        }
                                    }

                                    overlaps.Add(overlapInfo);
                                    AddLog($"发现重叠: 要素 {feature1.ObjectID} 与要素 {feature2.ObjectID}");
                                }
                            }
                        }
                    }
                }

                AddLog($"共发现 {overlaps.Count} 个重叠区域");

                // 创建输出要素类
                if (overlaps.Count > 0)
                {
                    await CreateOutputFeatureClass(overlaps, cancellationToken);
                }
                else
                {
                    AddLog("未发现重叠区域");
                }
            }
        }
    }
}
