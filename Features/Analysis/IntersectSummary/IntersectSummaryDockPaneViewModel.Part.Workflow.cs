using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.IntersectSummary
{
    internal partial class IntersectSummaryDockPaneViewModel
    {

        /// <summary>
        /// 执行交集汇总计算
        /// </summary>
        private async Task ExecuteAsync()
        {
            if (SelectedRedlineLayer == null || SelectedClassLayer == null)
            {
                StatusMessage = "请选择区域图层和类要素图层。";
                return;
            }

            // 区域字段可选，类字段必选
            var selectedRegionFields = RegionFields?.Where(f => f.IsSelected).Select(f => f.FieldName).ToList() ?? new List<string>();
            var selectedClassFields = ClassFields?.Where(f => f.IsSelected).Select(f => f.FieldName).ToList();

            if (selectedClassFields == null || !selectedClassFields.Any())
            {
                StatusMessage = "请至少选择一个类字段。";
                return;
            }

            try
            {
                IsProcessing = true;
                CancelRequested = false;
                Progress = 0;
                IsProgressIndeterminate = true;
                StatusMessage = "正在计算交集汇总...";
                LogContent = "";

                LogInfo($"开始计算交集汇总");
                LogInfo($"红线图层: {SelectedRedlineLayer.Name}");
                LogInfo($"区域字段: {string.Join(", ", selectedRegionFields)}");
                LogInfo($"类要素图层: {SelectedClassLayer.Name}");
                LogInfo($"类字段: {string.Join(", ", selectedClassFields)}");
                LogInfo($"单位: {SelectedAreaUnit}, 小数位数: {DecimalPlaces}");

                var results = new List<IntersectSummaryResultItem>();

                await QueuedTask.Run(() =>
                {
                    try
                    {
                        if (CancelRequested) return;

                        var redlineFC = SelectedRedlineLayer.GetFeatureClass();
                        var classFC = SelectedClassLayer.GetFeatureClass();

                        if (redlineFC == null || classFC == null)
                        {
                            LogError("无法获取要素类");
                            return;
                        }

                        // 获取两个图层的空间参考
                        var redlineSR = redlineFC.GetDefinition().GetSpatialReference();
                        var classSR = classFC.GetDefinition().GetSpatialReference();

                        // 获取红线要素
                        var redlineFeatures = new List<(long OID, Polygon Geometry, Dictionary<string, object> Attributes)>();
                        using (var cursor = redlineFC.Search())
                        {
                            while (cursor.MoveNext())
                            {
                                if (CancelRequested) return;
                                using (var feature = cursor.Current as Feature)
                                {
                                    if (feature?.GetShape() is Polygon polygon)
                                    {
                                        var attrs = new Dictionary<string, object>();
                                        foreach (var fieldName in selectedRegionFields)
                                        {
                                            attrs[fieldName] = feature[fieldName];
                                        }
                                        redlineFeatures.Add((feature.GetObjectID(), polygon, attrs));
                                    }
                                }
                            }
                        }

                        LogInfo($"共有 {redlineFeatures.Count} 个红线要素");

                        // 更新进度条为确定模式
                        UpdateProgress(false, 0);

                        int processedCount = 0;
                        int totalCount = redlineFeatures.Count;

                        // 按红线要素分组存储交集结果，用于面积调平
                        var redlineIntersects = new Dictionary<long, List<IntersectSummaryResultItem>>();

                        foreach (var redline in redlineFeatures)
                        {
                            if (CancelRequested)
                            {
                                LogWarning("操作已取消");
                                return;
                            }

                            // 获取红线要素的实际面积
                            double redlineArea = redline.Geometry.Area;

                            // 如果空间参考不同，需要将红线几何投影到类图层坐标系进行空间查询
                            Polygon queryGeometry = redline.Geometry;
                            if (!SpatialReference.AreEqual(redlineSR, classSR, false))
                            {
                                queryGeometry = GeometryEngine.Instance.Project(redline.Geometry, classSR) as Polygon;
                            }

                            // 使用空间过滤器获取与红线相交的类要素
                            var spatialFilter = new SpatialQueryFilter
                            {
                                FilterGeometry = queryGeometry,
                                SpatialRelationship = SpatialRelationship.Intersects
                            };

                            var intersectItems = new List<IntersectSummaryResultItem>();

                            using (var classCursor = classFC.Search(spatialFilter))
                            {
                                while (classCursor.MoveNext())
                                {
                                    if (CancelRequested) return;

                                    using (var classFeature = classCursor.Current as Feature)
                                    {
                                        if (classFeature?.GetShape() is Polygon classPolygon)
                                        {
                                            // 将类图层几何投影到红线图层坐标系进行交集运算
                                            Polygon projectedClass = classPolygon;
                                            if (!SpatialReference.AreEqual(redlineSR, classSR, false))
                                            {
                                                projectedClass = GeometryEngine.Instance.Project(classPolygon, redlineSR) as Polygon;
                                            }

                                            // 计算交集
                                            var intersection = GeometryEngine.Instance.Intersection(redline.Geometry, projectedClass);
                                            if (intersection != null && !intersection.IsEmpty && intersection is Polygon intersectPolygon)
                                            {
                                                double intersectArea = intersectPolygon.Area;
                                                if (intersectArea > 0.0001) // 忽略极小面积
                                                {
                                                    var resultItem = new IntersectSummaryResultItem
                                                    {
                                                        RegionValues = new Dictionary<string, object>(redline.Attributes),
                                                        Area = intersectArea
                                                    };

                                                    foreach (var fieldName in selectedClassFields)
                                                    {
                                                        resultItem.ClassValues[fieldName] = classFeature[fieldName];
                                                    }

                                                    intersectItems.Add(resultItem);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            // 计算交集面积总和
                            double totalIntersectArea = intersectItems.Sum(i => i.Area);

                            // 计算未覆盖面积（不在类范围内的面积）
                            double uncoveredArea = redlineArea - totalIntersectArea;

                            // 如果存在未覆盖面积，添加"其他"类别
                            if (uncoveredArea > 0.0001) // 容差0.0001平方米
                            {
                                var uncoveredItem = new IntersectSummaryResultItem
                                {
                                    RegionValues = new Dictionary<string, object>(),
                                    ClassValues = new Dictionary<string, object>(),
                                    Area = uncoveredArea,
                                    AdjustedArea = uncoveredArea
                                };

                                // 复制区域字段值
                                foreach (var fieldName in selectedRegionFields)
                                {
                                    uncoveredItem.RegionValues[fieldName] = redline.Attributes.ContainsKey(fieldName) ? redline.Attributes[fieldName] : null;
                                }

                                // 类字段设置为"其他"
                                foreach (var fieldName in selectedClassFields)
                                {
                                    uncoveredItem.ClassValues[fieldName] = "其他";
                                }

                                intersectItems.Add(uncoveredItem);
                                totalIntersectArea += uncoveredArea; // 更新总面积
                            }

                            // 面积调平：确保交集面积之和等于红线要素的实际面积
                            if (intersectItems.Count > 0)
                            {
                                if (totalIntersectArea > 0)
                                {
                                    // 按比例调整各交集面积
                                    double adjustmentRatio = redlineArea / totalIntersectArea;

                                    foreach (var item in intersectItems)
                                    {
                                        item.AdjustedArea = item.Area * adjustmentRatio;
                                    }

                                    // 处理调整后的舍入误差
                                    double adjustedTotal = intersectItems.Sum(i => i.AdjustedArea);
                                    double remainder = redlineArea - adjustedTotal;

                                    // 将剩余误差加到最大面积的项上
                                    if (Math.Abs(remainder) > 0.0000001 && intersectItems.Count > 0)
                                    {
                                        var maxItem = intersectItems.OrderByDescending(i => i.AdjustedArea).First();
                                        maxItem.AdjustedArea += remainder;
                                    }
                                }
                                else
                                {
                                    foreach (var item in intersectItems)
                                    {
                                        item.AdjustedArea = item.Area;
                                    }
                                }

                                results.AddRange(intersectItems);
                            }
                            else
                            {
                                // 如果没有任何交集，整个区域都是"其他"
                                var uncoveredItem = new IntersectSummaryResultItem
                                {
                                    RegionValues = new Dictionary<string, object>(),
                                    ClassValues = new Dictionary<string, object>(),
                                    Area = redlineArea,
                                    AdjustedArea = redlineArea
                                };

                                foreach (var fieldName in selectedRegionFields)
                                {
                                    uncoveredItem.RegionValues[fieldName] = redline.Attributes.ContainsKey(fieldName) ? redline.Attributes[fieldName] : null;
                                }

                                foreach (var fieldName in selectedClassFields)
                                {
                                    uncoveredItem.ClassValues[fieldName] = "其他";
                                }

                                results.Add(uncoveredItem);
                            }

                            processedCount++;
                            int progressPercent = (int)((double)processedCount / totalCount * 100);
                            UpdateProgress(false, progressPercent);
                            UpdateStatus($"正在处理... ({processedCount}/{totalCount})");
                        }

                        LogInfo($"交集计算完成，共 {results.Count} 条记录");
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理过程中出错: {ex.Message}");
                    }
                });

                if (!CancelRequested && results.Count > 0)
                {
                    // 按区域字段和类字段汇总
                    var summaryResults = AggregateResults(results, selectedRegionFields, selectedClassFields);

                    // 创建结果表
                    BuildResultTable(summaryResults, selectedRegionFields, selectedClassFields);

                    LogInfo($"汇总完成，共 {summaryResults.Count} 条汇总记录");
                    StatusMessage = "处理完成！";
                    Progress = 100;
                    NotifyPropertyChanged(() => HasResult);
                }
                else if (CancelRequested)
                {
                    StatusMessage = "操作已取消";
                }
                else
                {
                    StatusMessage = "未找到交集数据";
                }
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
    }
}
