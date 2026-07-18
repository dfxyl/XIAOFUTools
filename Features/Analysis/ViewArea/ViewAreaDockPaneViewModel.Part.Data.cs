using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Features.User.Settings;

namespace XIAOFUTools.Features.Analysis.ViewArea
{
    internal partial class ViewAreaDockPaneViewModel
    {

        /// <summary>
        /// 计算选中要素
        /// </summary>
        private void CalculateSelectedFeatures()
        {
            var mapView = MapView.Active;
            if (mapView?.Map == null)
            {
                UpdateSelectionInfo("无活动地图");
                ClearResults();
                return;
            }

            // 获取所有有选择的要素图层（包括组图层中的图层）
            var selectedLayers = GetAllFeatureLayers(mapView.Map.Layers).Where(layer => layer.SelectionCount > 0).ToList();
            if (!selectedLayers.Any())
            {
                UpdateSelectionInfo("未选择任何要素");
                ClearResults();
                return;
            }

            // 统计选择信息和检测坐标系类型
            int totalFeatures = 0;
            int layerCount = 0;
            var layerData = new Dictionary<string, List<Feature>>();
            var coordinateSystemTypes = new List<CoordinateSystemType>();
            var layerSpatialReferences = new Dictionary<string, SpatialReference>();

            foreach (var layer in selectedLayers)
            {
                layerCount++;
                var features = new List<Feature>();

                // 检测图层坐标系类型
                var layerSpatialRef = layer.GetSpatialReference();
                System.Diagnostics.Debug.WriteLine($"图层 '{layer.Name}' 的坐标系检测:");
                var layerCoordType = GetCoordinateSystemType(layerSpatialRef);
                System.Diagnostics.Debug.WriteLine($"图层 '{layer.Name}' 坐标系类型: {layerCoordType}");
                coordinateSystemTypes.Add(layerCoordType);
                layerSpatialReferences[layer.Name] = layerSpatialRef;

                // 获取图层的选择
                var selection = layer.GetSelection();
                using (var cursor = selection.Search(null, false))
                {
                    while (cursor.MoveNext())
                    {
                        var feature = cursor.Current as Feature;
                        if (feature != null)
                        {
                            features.Add(feature);
                            totalFeatures++;
                        }
                    }
                }

                if (features.Any())
                {
                    layerData[layer.Name] = features;
                }
            }

            // 确定使用的坐标系类型策略
            var coordinateSystemType = DetermineCoordinateSystemStrategy(coordinateSystemTypes);

            // 构建详细的选择信息
            var selectionDetails = new List<string>();
            foreach (var kvp in layerData)
            {
                selectionDetails.Add($"{kvp.Key}: {kvp.Value.Count}个要素");
            }

            var detailInfo = string.Join(", ", selectionDetails);
            var coordTypeInfo = GetCoordinateSystemTypeDescription(coordinateSystemType);
            UpdateSelectionInfo($"选择了{totalFeatures}个图形（{layerCount}个图层）- {detailInfo} - {coordTypeInfo}");

            // 计算结果
            CalculateResults(layerData, coordinateSystemType, layerSpatialReferences);
        }

        /// <summary>
        /// 计算结果（所有ArcGIS API调用在QueuedTask/MCT线程内执行）
        /// </summary>
        private void CalculateResults(Dictionary<string, List<Feature>> layerData, CoordinateSystemType coordinateSystemType, Dictionary<string, SpatialReference> layerSpatialReferences)
        {
            if (!layerData.Any())
            {
                ClearResults();
                return;
            }

            try
            {
                var allPolygons = new List<Polygon>();
                var allPolylines = new List<Polyline>();
                var layerSummaries = new List<LayerCalculationSummary>();

                // 在MCT线程上顺序处理每个图层
                foreach (var kvp in layerData)
                {
                    var layerName = kvp.Key;
                    var features = kvp.Value;

                    var layerSummary = new LayerCalculationSummary
                    {
                        LayerName = layerName,
                        FeatureCount = features.Count
                    };

                    var layerPolygons = new List<Polygon>();
                    var layerPolylines = new List<Polyline>();

                    foreach (var feature in features)
                    {
                        var geometry = feature.GetShape();
                        if (geometry != null)
                        {
                            // 获取图层的坐标系
                            var layerSpatialRef = layerSpatialReferences.ContainsKey(layerName) ? layerSpatialReferences[layerName] : null;

                            // 如果图层有坐标系，确保几何图形使用正确的坐标系
                            if (layerSpatialRef != null && geometry.SpatialReference == null)
                            {
                                geometry = GeometryEngine.Instance.Project(geometry, layerSpatialRef);
                            }

                            if (geometry is Polygon polygon)
                            {
                                allPolygons.Add(polygon);
                                layerPolygons.Add(polygon);
                            }
                            else if (geometry is Polyline polyline)
                            {
                                allPolylines.Add(polyline);
                                layerPolylines.Add(polyline);
                            }
                        }
                    }

                    CalculateLayerResults(layerSummary, layerPolygons, layerPolylines, coordinateSystemType);
                    layerSummaries.Add(layerSummary);
                }

                var combinedAreaResults = CalculateAreaResults(allPolygons, coordinateSystemType);
                var combinedLengthResults = CalculateLengthResults(allPolylines, coordinateSystemType);

                // 更新UI（在UI线程中）
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    CombinedAreaResults.Clear();
                    CombinedLengthResults.Clear();
                    LayerResults.Clear();

                    foreach (var result in combinedAreaResults)
                        CombinedAreaResults.Add(result);

                    foreach (var result in combinedLengthResults)
                        CombinedLengthResults.Add(result);

                    foreach (var summary in layerSummaries)
                        LayerResults.Add(summary);

                    HasAreaResults = combinedAreaResults.Any();
                    HasLengthResults = combinedLengthResults.Any();
                    ShowLayerResults = layerData.Count > 1;
                    NotifyPropertyChanged(() => HasResults);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"计算时出错: {ex.Message}");
            }
        }





        /// <summary>
        /// 计算图层结果
        /// </summary>
        private void CalculateLayerResults(LayerCalculationSummary layerSummary, List<Polygon> polygons, List<Polyline> polylines, CoordinateSystemType coordinateSystemType)
        {
            var areaResults = CalculateAreaResults(polygons, coordinateSystemType);
            var lengthResults = CalculateLengthResults(polylines, coordinateSystemType);

            layerSummary.AreaResults.Clear();
            layerSummary.LengthResults.Clear();

            foreach (var result in areaResults)
                layerSummary.AreaResults.Add(result);

            foreach (var result in lengthResults)
                layerSummary.LengthResults.Add(result);

            // 通知属性变化
            layerSummary.RefreshHasResultsProperties();
        }

        /// <summary>
        /// 计算面积结果（并行优化，根据坐标系类型决定计算策略）
        /// </summary>
        private List<CalculationResult> CalculateAreaResults(List<Polygon> polygons, CoordinateSystemType coordinateSystemType)
        {
            var results = new List<CalculationResult>();

            if (!polygons.Any())
                return results;

            try
            {
                double planarArea = 0;
                double geodesicArea = 0;

                // 根据坐标系类型决定计算策略
                System.Diagnostics.Debug.WriteLine($"面要素面积计算 - 坐标系类型: {coordinateSystemType}, 面要素数量: {polygons.Count}");
                switch (coordinateSystemType)
                {
                    case CoordinateSystemType.None:
                        // 无坐标系：只计算平面面积
                        planarArea = polygons.Sum(p => GeometryEngine.Instance.Area(p));
                        System.Diagnostics.Debug.WriteLine($"无坐标系 - 只计算平面面积: {planarArea}");
                        break;

                    case CoordinateSystemType.Geographic:
                        // 地理坐标系：只计算椭球面积（无法计算平面）
                        geodesicArea = polygons.Sum(p => CalculateGeodesicArea(p));
                        System.Diagnostics.Debug.WriteLine($"地理坐标系 - 只计算椭球面积: {geodesicArea}");
                        break;

                    case CoordinateSystemType.Projected:
                        // 投影坐标系：计算两种面积
                        planarArea = polygons.Sum(p => GeometryEngine.Instance.Area(p));
                        geodesicArea = polygons.Sum(p => CalculateGeodesicArea(p));
                        System.Diagnostics.Debug.WriteLine($"投影坐标系 - 平面面积: {planarArea}, 椭球面积: {geodesicArea}");
                        break;
                }

                // 平面面积结果（根据坐标系类型决定是否显示）
                results.Add(new CalculationResult
                {
                    CalculationType = "平面面积",
                    Unit1Value = planarArea > 0 ? FormatValue(planarArea, "平方米") : "",
                    Unit2Value = planarArea > 0 ? FormatValue(planarArea / 10000, "公顷") : "",
                    Unit3Value = planarArea > 0 ? FormatValue(planarArea / 666.67, "亩") : "",
                    Unit4Value = planarArea > 0 ? FormatValue(planarArea / 1000000, "平方公里") : ""
                });

                // 椭球面积结果（根据坐标系类型决定是否显示）
                results.Add(new CalculationResult
                {
                    CalculationType = "椭球面积",
                    Unit1Value = geodesicArea > 0 ? FormatValue(geodesicArea, "平方米") : "",
                    Unit2Value = geodesicArea > 0 ? FormatValue(geodesicArea / 10000, "公顷") : "",
                    Unit3Value = geodesicArea > 0 ? FormatValue(geodesicArea / 666.67, "亩") : "",
                    Unit4Value = geodesicArea > 0 ? FormatValue(geodesicArea / 1000000, "平方公里") : ""
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"并行面积计算失败，使用单线程: {ex.Message}");

                // 回退到单线程计算
                double planarArea = 0;
                double geodesicArea = 0;

                // 根据坐标系类型决定计算策略
                switch (coordinateSystemType)
                {
                    case CoordinateSystemType.None:
                        // 无坐标系：只计算平面面积
                        planarArea = polygons.Sum(p => GeometryEngine.Instance.Area(p));
                        break;

                    case CoordinateSystemType.Geographic:
                        // 地理坐标系：只计算椭球面积（无法计算平面）
                        geodesicArea = polygons.Sum(p => CalculateGeodesicArea(p));
                        break;

                    case CoordinateSystemType.Projected:
                        // 投影坐标系：计算两种面积
                        planarArea = polygons.Sum(p => GeometryEngine.Instance.Area(p));
                        geodesicArea = polygons.Sum(p => CalculateGeodesicArea(p));
                        break;
                }

                results.Add(new CalculationResult
                {
                    CalculationType = "平面面积",
                    Unit1Value = planarArea > 0 ? FormatValue(planarArea, "平方米") : "",
                    Unit2Value = planarArea > 0 ? FormatValue(planarArea / 10000, "公顷") : "",
                    Unit3Value = planarArea > 0 ? FormatValue(planarArea / 666.67, "亩") : "",
                    Unit4Value = planarArea > 0 ? FormatValue(planarArea / 1000000, "平方公里") : ""
                });

                results.Add(new CalculationResult
                {
                    CalculationType = "椭球面积",
                    Unit1Value = geodesicArea > 0 ? FormatValue(geodesicArea, "平方米") : "",
                    Unit2Value = geodesicArea > 0 ? FormatValue(geodesicArea / 10000, "公顷") : "",
                    Unit3Value = geodesicArea > 0 ? FormatValue(geodesicArea / 666.67, "亩") : "",
                    Unit4Value = geodesicArea > 0 ? FormatValue(geodesicArea / 1000000, "平方公里") : ""
                });
            }

            return results;
        }

        /// <summary>
        /// 计算长度结果（并行优化，根据坐标系类型决定计算策略）
        /// </summary>
        private List<CalculationResult> CalculateLengthResults(List<Polyline> polylines, CoordinateSystemType coordinateSystemType)
        {
            var results = new List<CalculationResult>();

            if (!polylines.Any())
                return results;

            try
            {
                double planarLength = 0;
                double geodesicLength = 0;

                // 根据坐标系类型决定计算策略
                System.Diagnostics.Debug.WriteLine($"线要素长度计算 - 坐标系类型: {coordinateSystemType}, 线要素数量: {polylines.Count}");
                switch (coordinateSystemType)
                {
                    case CoordinateSystemType.None:
                        // 无坐标系：只计算平面长度
                        planarLength = polylines.Sum(p => GeometryEngine.Instance.Length(p));
                        System.Diagnostics.Debug.WriteLine($"无坐标系 - 只计算平面长度: {planarLength}");
                        break;

                    case CoordinateSystemType.Geographic:
                        // 地理坐标系：只计算测地线长度（无法计算平面）
                        geodesicLength = polylines.Sum(p => CalculateGeodesicLength(p));
                        System.Diagnostics.Debug.WriteLine($"地理坐标系 - 只计算测地线长度: {geodesicLength}");
                        break;

                    case CoordinateSystemType.Projected:
                        // 投影坐标系：计算两种长度
                        planarLength = polylines.Sum(p => GeometryEngine.Instance.Length(p));
                        geodesicLength = polylines.Sum(p => CalculateGeodesicLength(p));
                        System.Diagnostics.Debug.WriteLine($"投影坐标系 - 平面长度: {planarLength}, 测地线长度: {geodesicLength}");
                        break;
                }

                // 平面长度结果（根据坐标系类型决定是否显示）
                results.Add(new CalculationResult
                {
                    CalculationType = "平面长度",
                    Unit1Value = planarLength > 0 ? FormatValue(planarLength, "米") : "",
                    Unit2Value = planarLength > 0 ? FormatValue(planarLength / 1000, "千米") : "",
                    Unit3Value = "",
                    Unit4Value = ""
                });

                // 测地线长度结果（根据坐标系类型决定是否显示）
                results.Add(new CalculationResult
                {
                    CalculationType = "测地线",
                    Unit1Value = geodesicLength > 0 ? FormatValue(geodesicLength, "米") : "",
                    Unit2Value = geodesicLength > 0 ? FormatValue(geodesicLength / 1000, "千米") : "",
                    Unit3Value = "",
                    Unit4Value = ""
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"并行长度计算失败，使用单线程: {ex.Message}");

                // 回退到单线程计算
                double planarLength = 0;
                double geodesicLength = 0;

                // 根据坐标系类型决定计算策略
                switch (coordinateSystemType)
                {
                    case CoordinateSystemType.None:
                        // 无坐标系：只计算平面长度
                        planarLength = polylines.Sum(p => GeometryEngine.Instance.Length(p));
                        break;

                    case CoordinateSystemType.Geographic:
                        // 地理坐标系：只计算测地线长度（无法计算平面）
                        geodesicLength = polylines.Sum(p => CalculateGeodesicLength(p));
                        break;

                    case CoordinateSystemType.Projected:
                        // 投影坐标系：计算两种长度
                        planarLength = polylines.Sum(p => GeometryEngine.Instance.Length(p));
                        geodesicLength = polylines.Sum(p => CalculateGeodesicLength(p));
                        break;
                }

                results.Add(new CalculationResult
                {
                    CalculationType = "平面长度",
                    Unit1Value = planarLength > 0 ? FormatValue(planarLength, "米") : "",
                    Unit2Value = planarLength > 0 ? FormatValue(planarLength / 1000, "千米") : "",
                    Unit3Value = "",
                    Unit4Value = ""
                });

                results.Add(new CalculationResult
                {
                    CalculationType = "测地线",
                    Unit1Value = geodesicLength > 0 ? FormatValue(geodesicLength, "米") : "",
                    Unit2Value = geodesicLength > 0 ? FormatValue(geodesicLength / 1000, "千米") : "",
                    Unit3Value = "",
                    Unit4Value = ""
                });
            }

            return results;
        }
    }
}
