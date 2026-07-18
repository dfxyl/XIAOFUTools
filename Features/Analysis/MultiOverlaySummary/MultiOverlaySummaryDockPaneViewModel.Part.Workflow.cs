using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary
{
    internal partial class MultiOverlaySummaryDockPaneViewModel
    {

        private async Task ExecuteAsync()
        {
            if (SelectedMainLayer == null)
            {
                StatusMessage = "请选择主图层。";
                return;
            }

            var selectedOverlayLayers = OverlayLayerItems?.Where(l => l.IsSelected).Select(l => l.Layer).ToList();
            if (selectedOverlayLayers == null || !selectedOverlayLayers.Any())
            {
                StatusMessage = "请至少选择一个压盖图层。";
                return;
            }

            try
            {
                IsProcessing = true;
                CancelRequested = false;
                Progress = 0;
                IsProgressIndeterminate = true;
                StatusMessage = "正在计算多图层压盖汇总...";
                LogContent = "";

                string uniqueFieldName = string.IsNullOrEmpty(SelectedUniqueField?.FieldName) ? null : SelectedUniqueField.FieldName;
                bool useGrouping = !string.IsNullOrEmpty(uniqueFieldName);
                _mainLayerName = SelectedMainLayer.Name;

                LogInfo($"开始计算多图层压盖汇总");
                LogInfo($"主图层: {_mainLayerName}");
                LogInfo($"唯一字段: {(useGrouping ? uniqueFieldName : "（不分组-汇总全部）")}");
                LogInfo($"压盖图层: {string.Join(", ", selectedOverlayLayers.Select(l => l.Name))}");

                var results = new Dictionary<string, Dictionary<string, double>>();
                var mainFeatureAreas = new Dictionary<string, double>();
                _intersectGeometries = new List<IntersectGeometryItem>();

                await QueuedTask.Run(() =>
                {
                    if (CancelRequested) return;

                    var mainFC = SelectedMainLayer.GetFeatureClass();
                    if (mainFC == null)
                    {
                        LogError("无法获取主图层要素类");
                        return;
                    }

                    _spatialReference = mainFC.GetDefinition().GetSpatialReference();

                    var mainFeatures = new List<(string Key, Polygon Geometry)>();
                    using (var cursor = mainFC.Search())
                    {
                        while (cursor.MoveNext())
                        {
                            if (CancelRequested) return;
                            using (var feature = cursor.Current as Feature)
                            {
                                if (feature?.GetShape() is Polygon polygon)
                                {
                                    string key = useGrouping
                                        ? (feature[uniqueFieldName]?.ToString() ?? "(空值)")
                                        : "全部";
                                    mainFeatures.Add((key, polygon));
                                }
                            }
                        }
                    }

                    LogInfo($"共有 {mainFeatures.Count} 个主图层要素");
                    UpdateProgress(false, 0);

                    int totalSteps = mainFeatures.Count * selectedOverlayLayers.Count;
                    int currentStep = 0;

                    var uniqueKeys = mainFeatures.Select(f => f.Key).Distinct().ToList();
                    foreach (var key in uniqueKeys)
                    {
                        results[key] = new Dictionary<string, double>();
                        mainFeatureAreas[key] = 0;
                        foreach (var overlayLayer in selectedOverlayLayers)
                            results[key][overlayLayer.Name] = 0;
                    }

                    foreach (var mainFeature in mainFeatures)
                    {
                        if (CancelRequested) { LogWarning("操作已取消"); return; }

                        double mainArea = CalculateArea(mainFeature.Geometry);
                        mainFeatureAreas[mainFeature.Key] += mainArea;

                        foreach (var overlayLayer in selectedOverlayLayers)
                        {
                            if (CancelRequested) return;

                            var overlayFC = overlayLayer.GetFeatureClass();
                            if (overlayFC == null) continue;

                            // 获取压盖图层的空间参考
                            var overlaySR = overlayFC.GetDefinition().GetSpatialReference();

                            // 将主图层几何投影到压盖图层坐标系进行空间查询
                            Geometry queryGeometry = mainFeature.Geometry;
                            if (_spatialReference != null && overlaySR != null && !SpatialReference.AreEqual(_spatialReference, overlaySR, false))
                            {
                                queryGeometry = GeometryEngine.Instance.Project(mainFeature.Geometry, overlaySR);
                            }

                            var spatialFilter = new SpatialQueryFilter
                            {
                                FilterGeometry = queryGeometry,
                                SpatialRelationship = SpatialRelationship.Intersects
                            };

                            using (var overlayCursor = overlayFC.Search(spatialFilter))
                            {
                                while (overlayCursor.MoveNext())
                                {
                                    if (CancelRequested) return;
                                    using (var overlayFeature = overlayCursor.Current as Feature)
                                    {
                                        if (overlayFeature?.GetShape() is Polygon overlayPolygon)
                                        {
                                            // 将压盖图层几何投影到主图层坐标系进行交集运算
                                            Polygon projectedOverlay = overlayPolygon;
                                            if (_spatialReference != null && overlaySR != null && !SpatialReference.AreEqual(_spatialReference, overlaySR, false))
                                            {
                                                projectedOverlay = GeometryEngine.Instance.Project(overlayPolygon, _spatialReference) as Polygon;
                                            }

                                            if (projectedOverlay == null) continue;

                                            var intersection = GeometryEngine.Instance.Intersection(mainFeature.Geometry, projectedOverlay);
                                            if (intersection != null && !intersection.IsEmpty && intersection is Polygon intersectPolygon)
                                            {
                                                double area = CalculateArea(intersectPolygon);
                                                results[mainFeature.Key][overlayLayer.Name] += area;

                                                // 保存交集几何
                                                _intersectGeometries.Add(new IntersectGeometryItem
                                                {
                                                    GroupKey = mainFeature.Key,
                                                    OverlayLayerName = overlayLayer.Name,
                                                    Geometry = intersectPolygon,
                                                    Area = area
                                                });
                                            }
                                        }
                                    }
                                }
                            }

                            currentStep++;
                            UpdateProgress(false, (int)((double)currentStep / totalSteps * 100));
                            UpdateStatus($"正在处理... ({currentStep}/{totalSteps})");
                        }
                    }

                    LogInfo($"压盖计算完成，共 {_intersectGeometries.Count} 个交集图形");
                });

                if (!CancelRequested && results.Count > 0)
                {
                    CreateResultTable(results, mainFeatureAreas, selectedOverlayLayers.Select(l => l.Name).ToList(), uniqueFieldName);
                    LogInfo($"汇总完成，共 {results.Count} 条记录");
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
                    StatusMessage = "未找到数据";
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
