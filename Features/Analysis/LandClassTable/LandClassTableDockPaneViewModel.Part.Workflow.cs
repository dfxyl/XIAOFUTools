using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.Analysis.LandClassTable.Core;
using XIAOFUTools.Features.Analysis.LandClassTable.Infrastructure;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    internal sealed partial class LandClassTableDockPaneViewModel
    {

        private async Task ExecuteAsync()
        {
            if (!CanProcess)
            {
                return;
            }

            try
            {
                IsProcessing = true;
                _cancelRequested = false;
                Progress = 0;
                IsProgressIndeterminate = false;
                LogContent = string.Empty;

                LogInfo("开始分析项目红线与地类图斑。");
                var projectAreas = new List<LandClassProjectArea>();
                var intersectionAreas = new List<LandClassIntersectionArea>();
                var rightHolderNames = new List<string>();
                var unmatchedClasses = new Dictionary<string, UnmatchedLandClassInfo>(StringComparer.OrdinalIgnoreCase);

                await QueuedTask.Run(() =>
                {
                    var redlineFeatureClass = SelectedRedlineLayer.GetFeatureClass();
                    var classFeatureClass = SelectedClassLayer.GetFeatureClass();
                    if (redlineFeatureClass == null || classFeatureClass == null)
                    {
                        throw new InvalidOperationException("无法读取图层要素类。");
                    }

                    var redlineSpatialReference = redlineFeatureClass.GetDefinition().GetSpatialReference();
                    var classSpatialReference = classFeatureClass.GetDefinition().GetSpatialReference();

                    var redlineItems = new List<RedlineItem>();
                    using (var cursor = redlineFeatureClass.Search(null, false))
                    {
                        while (cursor.MoveNext())
                        {
                            if (_cancelRequested)
                            {
                                return;
                            }

                            using (var feature = cursor.Current as Feature)
                            {
                                if (feature?.GetShape() is not Polygon polygon || polygon.IsEmpty)
                                {
                                    continue;
                                }

                                string name = ReadProjectName(feature);
                                string groupName = ReadOptionalField(feature, SelectedGroupField);
                                string plotName = ReadOptionalField(feature, SelectedPlotNameField);
                                double totalArea = ReadTotalArea(feature, polygon);
                                if (totalArea <= 0)
                                {
                                    continue;
                                }

                                redlineItems.Add(new RedlineItem(name, totalArea, polygon, groupName, plotName));
                                projectAreas.Add(new LandClassProjectArea(name, totalArea, plotName, groupName));
                                rightHolderNames.Add(name);
                            }
                        }
                    }

                    LogInfo($"读取项目红线 {redlineItems.Count} 个。");
                    int totalSteps = Math.Max(1, redlineItems.Count);

                    for (int i = 0; i < redlineItems.Count; i++)
                    {
                        if (_cancelRequested)
                        {
                            return;
                        }

                        var redline = redlineItems[i];
                        Geometry queryGeometry = redline.Geometry;
                        if (redlineSpatialReference != null &&
                            classSpatialReference != null &&
                            !SpatialReference.AreEqual(redlineSpatialReference, classSpatialReference, false))
                        {
                            queryGeometry = GeometryEngine.Instance.Project(redline.Geometry, classSpatialReference);
                        }

                        var spatialFilter = new SpatialQueryFilter
                        {
                            FilterGeometry = queryGeometry,
                            SpatialRelationship = SpatialRelationship.Intersects
                        };

                        using (var classCursor = classFeatureClass.Search(spatialFilter, false))
                        {
                            while (classCursor.MoveNext())
                            {
                                if (_cancelRequested)
                                {
                                    return;
                                }

                                using (var classFeature = classCursor.Current as Feature)
                                {
                                    if (classFeature?.GetShape() is not Polygon classPolygon || classPolygon.IsEmpty)
                                    {
                                        continue;
                                    }

                                    Polygon projectedClass = classPolygon;
                                    if (redlineSpatialReference != null &&
                                        classSpatialReference != null &&
                                        !SpatialReference.AreEqual(redlineSpatialReference, classSpatialReference, false))
                                    {
                                        projectedClass = GeometryEngine.Instance.Project(classPolygon, redlineSpatialReference) as Polygon;
                                    }

                                    if (projectedClass == null)
                                    {
                                        continue;
                                    }

                                    var intersection = GeometryEngine.Instance.Intersection(redline.Geometry, projectedClass) as Polygon;
                                    if (intersection == null || intersection.IsEmpty)
                                    {
                                        continue;
                                    }

                                    string landClassName = Convert.ToString(classFeature[SelectedClassNameField.FieldName], CultureInfo.CurrentCulture) ?? string.Empty;
                                    if (LandClassTableBuilder.ResolveDefinition(landClassName) == null)
                                    {
                                        AddUnmatchedLandClass(unmatchedClasses, landClassName, redline.ProjectName, redline.PlotName);
                                        continue;
                                    }

                                    double area = CalculateSquareMeterArea(intersection);
                                    if (area > 0)
                                    {
                                        string ownerUnitName = ReadOwnerUnit(classFeature);
                                        string ownerNatureCode = ReadOwnerNature(classFeature);
                                        intersectionAreas.Add(new LandClassIntersectionArea(
                                            redline.ProjectName,
                                            landClassName,
                                            area,
                                            ownerUnitName,
                                            ownerNatureCode,
                                            redline.PlotName,
                                            redline.GroupValue));
                                    }
                                }
                            }
                        }

                        UpdateProgress((int)Math.Round((i + 1) * 70.0 / totalSteps));
                    }
                });

                if (_cancelRequested)
                {
                    LogWarning("操作已取消。");
                    return;
                }

                var result = LandClassTableBuilder.BuildTable(
                    projectAreas,
                    intersectionAreas,
                    DecimalPlaces);

                if (result.Rows.Count == 0)
                {
                    LogWarning("未生成有效统计结果。");
                    return;
                }

                LogInfo("开始导出 Excel 文件。");
                UpdateProgress(70);
                var exportOptions = CreateExportOptions(
                    LandClassReportHeaderBuilder.BuildRightHolderName(rightHolderNames));
                await LandClassExcelExporter.ExportResultsAsync(
                    projectAreas,
                    intersectionAreas,
                    exportOptions,
                    () => _cancelRequested,
                    UpdateProgressFromWorker);
                if (_cancelRequested)
                {
                    LogWarning("操作已取消。");
                    return;
                }

                Progress = 100;
                IsProgressIndeterminate = false;
                LogInfo($"地类表已输出到: {exportOptions.OutputFolder}");

                if (unmatchedClasses.Count > 0)
                {
                    foreach (var item in unmatchedClasses.Values
                                 .OrderByDescending(x => x.Count)
                                 .ThenBy(x => x.DisplayValue, StringComparer.CurrentCulture)
                                 .Take(20))
                    {
                        LogWarning($"地类字段{item.DisplayValue}未参与统计，数量 {item.Count}，位置: {string.Join("；", item.Locations.Take(5))}");
                    }

                    if (unmatchedClasses.Count > 20)
                    {
                        LogWarning($"未匹配地类共 {unmatchedClasses.Count} 类，日志仅显示前 20 类。");
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    LogWarning("操作已取消。");
                    return;
                }

                LogError($"执行失败: {ex.Message}");
                PresentationServices.Dialogs.Show($"执行失败: {ex.Message}", "错误");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
            }
        }


    }
}
