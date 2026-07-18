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
        /// 根据面积单位获取默认小数位数
        /// </summary>
        private int GetDefaultDecimalPlacesByUnit(string areaUnit)
        {
            return string.Equals(areaUnit, SquareMetersUnit, StringComparison.Ordinal)
                ? SquareMetersDecimalPlaces
                : OtherUnitsDecimalPlaces;
        }

        /// <summary>
        /// 根据 URI（优先）和名称（兜底）匹配目标图层
        /// </summary>
        private FeatureLayer FindPreferredLayer(IEnumerable<FeatureLayer> layers)
        {
            if (layers == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(PreferredLayerUri))
            {
                var uriMatchedLayer = layers.FirstOrDefault(layer =>
                    string.Equals(layer.URI, PreferredLayerUri, StringComparison.OrdinalIgnoreCase));
                if (uriMatchedLayer != null)
                {
                    return uriMatchedLayer;
                }
            }

            if (!string.IsNullOrWhiteSpace(PreferredLayerName))
            {
                return layers.FirstOrDefault(layer =>
                    string.Equals(layer.Name, PreferredLayerName, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

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
                    if (PresentationServices.UiThread != null)
                    {
                        PresentationServices.UiThread.Invoke(() =>
                        {
                            // 清空图层列表
                            PolygonLayers?.Clear();

                            // 添加图层
                            if (PolygonLayers != null)
                            {
                                foreach (var layer in tempLayers)
                                {
                                    PolygonLayers.Add(layer);
                                }

                                // 如果有图层，默认选择第一个
                                if (PolygonLayers.Count > 0)
                                {
                                    var preferredLayer = FindPreferredLayer(PolygonLayers);

                                    SelectedPolygonLayer = preferredLayer ?? PolygonLayers[0];
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    // 确保在UI线程显示错误信息
                    if (PresentationServices.UiThread != null)
                    {
                        PresentationServices.UiThread.Invoke(() =>
                        {
                            StatusMessage = $"加载图层出错: {ex.Message}";
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 加载字段信息
        /// </summary>
        private void LoadFieldNames()
        {
            if (SelectedPolygonLayer == null)
            {
                FieldInfos?.Clear();
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldDisplayInfo>();

                    await QueuedTask.Run(() =>
                    {
                        var featureClass = SelectedPolygonLayer.GetFeatureClass();
                        if (featureClass != null)
                        {
                            var definition = featureClass.GetDefinition();
                            var fields = definition.GetFields();

                            foreach (var field in fields)
                            {
                                // 显示数值类型字段和文本字段
                                if (field.FieldType == FieldType.Double ||
                                    field.FieldType == FieldType.Single ||
                                    field.FieldType == FieldType.Integer ||
                                    field.FieldType == FieldType.SmallInteger ||
                                    field.FieldType == FieldType.String)
                                {
                                    var fieldInfo = new FieldDisplayInfo
                                    {
                                        FieldName = field.Name,
                                        Alias = field.AliasName,
                                        FieldType = GetFieldTypeDisplayName(field.FieldType)
                                    };
                                    tempFieldInfos.Add(fieldInfo);
                                }
                            }
                        }
                    });

                    // 在UI线程更新字段列表
                    if (PresentationServices.UiThread != null)
                    {
                        PresentationServices.UiThread.Invoke(() =>
                        {
                            FieldInfos?.Clear();
                            if (FieldInfos != null)
                            {
                                foreach (var fieldInfo in tempFieldInfos)
                                {
                                    FieldInfos.Add(fieldInfo);
                                }
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    if (PresentationServices.UiThread != null)
                    {
                        PresentationServices.UiThread.Invoke(() =>
                        {
                            StatusMessage = $"加载字段出错: {ex.Message}";
                        });
                    }
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
                FieldType.DateOnly => "仅日期",
                FieldType.TimeOnly => "仅时间",
                FieldType.TimestampOffset => "时间戳偏移",
                FieldType.GUID => "全局唯一标识符",
                FieldType.GlobalID => "全局ID",
                FieldType.OID => "对象ID",
                FieldType.Geometry => "几何",
                FieldType.Blob => "二进制大对象",
                FieldType.Raster => "栅格",
                FieldType.XML => "XML",
                _ => "未知类型"
            };
        }

        /// <summary>
        /// 计算面积
        /// </summary>
        private double CalculateArea(Polygon polygon)
        {
            if (polygon == null) return 0;

            if (SelectedAreaType == "椭球")
            {
                // 椭球面积计算
                return GeometryEngine.Instance.GeodesicArea(polygon);
            }
            else
            {
                // 平面面积计算
                return polygon.Area;
            }
        }

        /// <summary>
        /// 根据类型计算面积（线程安全版本）
        /// </summary>
        private double CalculateAreaByType(Polygon polygon, string areaType)
        {
            if (polygon == null) return 0;

            if (areaType == "椭球")
            {
                // 椭球面积计算 - 使用ArcGIS内置椭球面积计算
                return GeometryEngine.Instance.GeodesicArea(polygon);
            }
            else
            {
                // 平面面积计算
                return polygon.Area;
            }
        }

        /// <summary>
        /// 转换面积单位
        /// </summary>
        private double ConvertAreaUnit(double areaInSquareMeters, string targetUnit)
        {
            switch (targetUnit)
            {
                case "平方米":
                    return areaInSquareMeters;
                case "公顷":
                    return areaInSquareMeters / 10000.0;
                case "亩":
                    return areaInSquareMeters / 666.67;
                case "平方公里":
                    return areaInSquareMeters / 1000000.0;
                default:
                    return areaInSquareMeters;
            }
        }

        /// <summary>
        /// 从显示名称获取字段类型
        /// </summary>
        private FieldType GetFieldTypeFromDisplayName(string displayName)
        {
            return displayName switch
            {
                "双精度" => FieldType.Double,
                "单精度" => FieldType.Single,
                "整型" => FieldType.Integer,
                "短整型" => FieldType.SmallInteger,
                "文本" => FieldType.String,
                _ => FieldType.Double
            };
        }
    }
}
