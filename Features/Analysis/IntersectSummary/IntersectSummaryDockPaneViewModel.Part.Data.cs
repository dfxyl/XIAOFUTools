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
        /// 加载面图层
        /// </summary>
        private void LoadPolygonLayers()
        {
            Task.Run(async () =>
            {
                try
                {
                    var tempLayers = new List<FeatureLayer>();

                    await QueuedTask.Run(() =>
                    {
                        var map = MapView.Active?.Map;
                        if (map != null)
                        {
                            var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>();
                            foreach (var layer in layers)
                            {
                                if (layer.GetFeatureClass()?.GetDefinition()?.GetShapeType() == GeometryType.Polygon)
                                {
                                    tempLayers.Add(layer);
                                }
                            }
                        }
                    });

                    // 在UI线程更新图层列表
                    PresentationServices.UiThread.Invoke(() =>
                    {
                        PolygonLayers?.Clear();

                        if (PolygonLayers != null)
                        {
                            foreach (var layer in tempLayers)
                            {
                                PolygonLayers.Add(layer);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.Invoke(() =>
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                    });
                }
            });
        }

        /// <summary>
        /// 加载区域字段
        /// </summary>
        private void LoadRegionFields()
        {
            if (SelectedRedlineLayer == null)
            {
                RegionFields?.Clear();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldSelectItem>();

                    await QueuedTask.Run(() =>
                    {
                        var featureClass = SelectedRedlineLayer.GetFeatureClass();
                        if (featureClass != null)
                        {
                            var definition = featureClass.GetDefinition();
                            var fields = definition.GetFields();

                            foreach (var field in fields)
                            {
                                // 跳过系统字段
                                if (field.FieldType == FieldType.Geometry ||
                                    field.FieldType == FieldType.OID ||
                                    field.FieldType == FieldType.GlobalID ||
                                    field.FieldType == FieldType.Blob ||
                                    field.FieldType == FieldType.Raster)
                                    continue;

                                var fieldInfo = new FieldSelectItem
                                {
                                    FieldName = field.Name,
                                    Alias = field.AliasName,
                                    FieldType = GetFieldTypeDisplayName(field.FieldType),
                                    IsSelected = false
                                };
                                fieldInfo.PropertyChanged += (s, e) =>
                                {
                                    if (e.PropertyName == nameof(FieldSelectItem.IsSelected))
                                    {
                                        NotifyPropertyChanged(() => CanProcess);
                                    }
                                };
                                tempFieldInfos.Add(fieldInfo);
                            }
                        }
                    });

                    // 在UI线程更新字段列表
                    PresentationServices.UiThread.Invoke(() =>
                    {
                        RegionFields?.Clear();
                        if (RegionFields != null)
                        {
                            foreach (var fieldInfo in tempFieldInfos)
                            {
                                RegionFields.Add(fieldInfo);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.Invoke(() =>
                    {
                        StatusMessage = $"加载字段出错: {ex.Message}";
                    });
                }
            });
        }

        /// <summary>
        /// 加载类字段
        /// </summary>
        private void LoadClassFields()
        {
            if (SelectedClassLayer == null)
            {
                ClassFields?.Clear();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldSelectItem>();

                    await QueuedTask.Run(() =>
                    {
                        var featureClass = SelectedClassLayer.GetFeatureClass();
                        if (featureClass != null)
                        {
                            var definition = featureClass.GetDefinition();
                            var fields = definition.GetFields();

                            foreach (var field in fields)
                            {
                                // 跳过系统字段
                                if (field.FieldType == FieldType.Geometry ||
                                    field.FieldType == FieldType.OID ||
                                    field.FieldType == FieldType.GlobalID ||
                                    field.FieldType == FieldType.Blob ||
                                    field.FieldType == FieldType.Raster)
                                    continue;

                                var fieldInfo = new FieldSelectItem
                                {
                                    FieldName = field.Name,
                                    Alias = field.AliasName,
                                    FieldType = GetFieldTypeDisplayName(field.FieldType),
                                    IsSelected = false
                                };
                                fieldInfo.PropertyChanged += (s, e) =>
                                {
                                    if (e.PropertyName == nameof(FieldSelectItem.IsSelected))
                                    {
                                        NotifyPropertyChanged(() => CanProcess);
                                    }
                                };
                                tempFieldInfos.Add(fieldInfo);
                            }
                        }
                    });

                    // 在UI线程更新字段列表
                    PresentationServices.UiThread.Invoke(() =>
                    {
                        ClassFields?.Clear();
                        if (ClassFields != null)
                        {
                            foreach (var fieldInfo in tempFieldInfos)
                            {
                                ClassFields.Add(fieldInfo);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.Invoke(() =>
                    {
                        StatusMessage = $"加载字段出错: {ex.Message}";
                    });
                }
            });
        }

        /// <summary>
        /// 获取字段类型的显示名称
        /// </summary>
        private string GetFieldTypeDisplayName(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.Double => "双精度",
                FieldType.Single => "单精度",
                FieldType.Integer => "整型",
                FieldType.SmallInteger => "短整型",
                FieldType.String => "文本",
                FieldType.BigInteger => "长整型",
                FieldType.Date => "日期",
                _ => "其他"
            };
        }

        /// <summary>
        /// 转换面积单位
        /// </summary>
        private double ConvertAreaUnit(double areaInSquareMeters, string targetUnit)
        {
            return targetUnit switch
            {
                "平方米" => areaInSquareMeters,
                "公顷" => areaInSquareMeters / 10000.0,
                "亩" => areaInSquareMeters / 666.6666666667,
                _ => areaInSquareMeters
            };
        }
    }
}
