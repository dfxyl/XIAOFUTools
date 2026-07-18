using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;

namespace XIAOFUTools.Features.Custom.DevZoneCheck
{
    internal partial class DevZoneCheckDockPaneViewModel
    {

        private void LoadParkFields()
        {
            if (SelectedParkLayer == null) return;

            QueuedTask.Run(() =>
            {
                var fc = SelectedParkLayer.GetFeatureClass();
                if (fc == null) return;

                var fields = fc.GetDefinition().GetFields()
                    .Where(f => f.FieldType == FieldType.String || 
                               f.FieldType == FieldType.Integer ||
                               f.FieldType == FieldType.SmallInteger)
                    .Select(f => f.Name)
                    .ToList();

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    ParkFields.Clear();
                    ParkFields.Add(""); // 允许不选择
                    foreach (var field in fields)
                    {
                        ParkFields.Add(field);
                    }
                });
            });
        }

        private void LoadLandSurveyFields()
        {
            if (LandSurveyLayer == null) return;

            QueuedTask.Run(() =>
            {
                var fc = LandSurveyLayer.GetFeatureClass();
                if (fc == null) return;

                var fields = fc.GetDefinition().GetFields()
                    .Where(f => f.FieldType == FieldType.String)
                    .Select(f => f.Name)
                    .ToList();

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    LandSurveyFields.Clear();
                    foreach (var field in fields)
                    {
                        LandSurveyFields.Add(field);
                    }
                    // 自动选择DLBM字段
                    if (fields.Contains("DLBM"))
                        SelectedLandSurveyField = "DLBM";
                    else if (fields.Count > 0)
                        SelectedLandSurveyField = fields[0];
                });
            });
        }

        private void LoadSpatialPlanningFields()
        {
            if (SpatialPlanningLayer == null) return;

            QueuedTask.Run(() =>
            {
                var fc = SpatialPlanningLayer.GetFeatureClass();
                if (fc == null) return;

                var fields = fc.GetDefinition().GetFields()
                    .Where(f => f.FieldType == FieldType.String)
                    .Select(f => f.Name)
                    .ToList();

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    SpatialPlanningFields.Clear();
                    foreach (var field in fields)
                    {
                        SpatialPlanningFields.Add(field);
                    }

                    if (SpatialPlanningFields.Contains("GHDLBM"))
                    {
                        SelectedSpatialPlanningField = "GHDLBM";
                    }
                    else if (SpatialPlanningFields.Count > 0)
                    {
                        SelectedSpatialPlanningField = SpatialPlanningFields[0];
                    }
                });
            });
        }

        /// <summary>
        /// 计算面积（平方米）- 支持平面和椭球面
        /// </summary>
        private double CalculateArea(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty) return 0;
            
            try
            {
                if (SelectedAreaCalculationMethod == "椭球面")
                {
                    // 椭球面计算需要有效的空间参考
                    if (geometry.SpatialReference == null)
                    {
                        // 没有空间参考，回退到平面计算
                        return Math.Abs(((Polygon)geometry).Area);
                    }
                    
                    return Math.Abs(GeometryEngine.Instance.GeodesicArea(geometry));
                }
                else
                {
                    return Math.Abs(((Polygon)geometry).Area);
                }
            }
            catch (Exception)
            {
                // 回退到平面计算
                try
                {
                    return Math.Abs(((Polygon)geometry).Area);
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// 转换面积单位
        /// </summary>
        private double ConvertArea(double squareMeters)
        {
            return SelectedAreaUnit switch
            {
                "平方米" => squareMeters,
                "公顷" => squareMeters / 10000.0,
                "亩" => squareMeters / 666.67,
                "平方公里" => squareMeters / 1000000.0,
                _ => squareMeters
            };
        }

        /// <summary>
        /// 计算两个图层的交集面积（优化版：使用空间过滤）
        /// </summary>
        private async Task<double> CalculateIntersectAreaAsync(FeatureLayer sourceLayer, Geometry clipGeometry, 
            string filterField = null, string[] filterCodes = null)
        {
            double totalArea = 0;

            await QueuedTask.Run(() =>
            {
                var fc = sourceLayer.GetFeatureClass();
                if (fc == null) return;

                // 获取数据层的空间参考
                var layerSpatialRef = fc.GetDefinition().GetSpatialReference();
                var clipSpatialRef = clipGeometry.SpatialReference;
                
                // 确定统一的目标坐标系：优先使用clipGeometry的空间参考（园区图层）
                SpatialReference targetSpatialRef = clipSpatialRef ?? layerSpatialRef;
                
                // 准备clipGeometry - 确保有空间参考
                Geometry clipGeomInTarget = clipGeometry;
                
                // 如果clipGeometry没有空间参考但图层有，为其设置空间参考
                if (clipSpatialRef == null && layerSpatialRef != null)
                {
                    try
                    {
                        var builder = new PolygonBuilderEx(clipGeometry as Polygon);
                        builder.SpatialReference = layerSpatialRef;
                        clipGeomInTarget = builder.ToGeometry();
                        targetSpatialRef = layerSpatialRef;
                    }
                    catch { }
                }

                // 为空间过滤器准备几何（需要与数据层坐标系一致）
                Geometry filterGeometry = clipGeomInTarget;
                if (layerSpatialRef != null && clipGeomInTarget.SpatialReference != null &&
                    !SpatialReference.AreEqual(layerSpatialRef, clipGeomInTarget.SpatialReference, false))
                {
                    try
                    {
                        filterGeometry = GeometryEngine.Instance.Project(clipGeomInTarget, layerSpatialRef);
                    }
                    catch
                    {
                        filterGeometry = clipGeomInTarget;
                    }
                }

                // 使用空间过滤器提高性能
                var spatialFilter = new SpatialQueryFilter
                {
                    FilterGeometry = filterGeometry,
                    SpatialRelationship = SpatialRelationship.Intersects
                };

                // 如果有字段过滤条件，添加到查询
                if (!string.IsNullOrEmpty(filterField) && filterCodes != null && filterCodes.Length > 0)
                {
                    var whereClauses = filterCodes.Select(code => 
                        $"{filterField} = '{code}' OR {filterField} LIKE '{code}%'");
                    spatialFilter.WhereClause = string.Join(" OR ", whereClauses);
                }

                using (var cursor = fc.Search(spatialFilter))
                {
                    while (cursor.MoveNext())
                    {
                        if (CancelRequested) break;

                        using (var feature = cursor.Current as Feature)
                        {
                            var geom = feature.GetShape();
                            if (geom == null || geom.IsEmpty) continue;

                            try
                            {
                                Geometry processGeom = geom;
                                Geometry clipGeomForIntersect = clipGeomInTarget;
                                
                                // 获取要素的空间参考（优先使用要素自身的，否则使用图层的）
                                var geomSpatialRef = geom.SpatialReference ?? layerSpatialRef;
                                var clipGeomSpatialRef = clipGeomForIntersect.SpatialReference;
                                
                                // 情况1：两者都没有空间参考 - 直接计算
                                if (geomSpatialRef == null && clipGeomSpatialRef == null)
                                {
                                    // 无需投影，直接计算
                                }
                                // 情况2：要素没有空间参考，clipGeom有 - 为要素设置clipGeom的空间参考
                                else if (geomSpatialRef == null && clipGeomSpatialRef != null)
                                {
                                    var builder = new PolygonBuilderEx(geom as Polygon);
                                    builder.SpatialReference = clipGeomSpatialRef;
                                    processGeom = builder.ToGeometry();
                                }
                                // 情况3：要素有空间参考，clipGeom没有 - 为clipGeom设置要素的空间参考
                                else if (geomSpatialRef != null && clipGeomSpatialRef == null)
                                {
                                    var builder = new PolygonBuilderEx(clipGeomForIntersect as Polygon);
                                    builder.SpatialReference = geomSpatialRef;
                                    clipGeomForIntersect = builder.ToGeometry();
                                }
                                // 情况4：两者都有空间参考但不同 - 投影要素到clipGeom的坐标系
                                else if (!SpatialReference.AreEqual(geomSpatialRef, clipGeomSpatialRef, false))
                                {
                                    processGeom = GeometryEngine.Instance.Project(geom, clipGeomSpatialRef);
                                }
                                
                                // 计算交集
                                var intersection = GeometryEngine.Instance.Intersection(processGeom, clipGeomForIntersect);
                                if (intersection != null && !intersection.IsEmpty)
                                {
                                    totalArea += CalculateArea(intersection);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Intersection error: {ex.Message}");
                                continue;
                            }
                        }
                    }
                }
            });

            return totalArea;
        }
    }
}
