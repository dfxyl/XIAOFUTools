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

                    PresentationServices.UiThread.Invoke(() =>
                    {
                        PolygonLayers?.Clear();
                        foreach (var layer in tempLayers)
                            PolygonLayers?.Add(layer);
                        UpdateOverlayLayerItems();
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

        private void LoadMainLayerFields()
        {
            if (SelectedMainLayer == null)
            {
                MainLayerFields?.Clear();
                SelectedUniqueField = null;
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    var tempFieldInfos = new List<FieldSelectItem>();
                    await QueuedTask.Run(() =>
                    {
                        var featureClass = SelectedMainLayer.GetFeatureClass();
                        if (featureClass != null)
                        {
                            var definition = featureClass.GetDefinition();
                            foreach (var field in definition.GetFields())
                            {
                                if (field.FieldType == FieldType.Geometry ||
                                    field.FieldType == FieldType.OID ||
                                    field.FieldType == FieldType.GlobalID ||
                                    field.FieldType == FieldType.Blob ||
                                    field.FieldType == FieldType.Raster)
                                    continue;

                                tempFieldInfos.Add(new FieldSelectItem
                                {
                                    FieldName = field.Name,
                                    Alias = field.AliasName,
                                    FieldType = GetFieldTypeDisplayName(field.FieldType)
                                });
                            }
                        }
                    });

                    PresentationServices.UiThread.Invoke(() =>
                    {
                        MainLayerFields?.Clear();
                        MainLayerFields?.Add(new FieldSelectItem { FieldName = "", Alias = "（不分组）", FieldType = "" });
                        foreach (var fieldInfo in tempFieldInfos)
                            MainLayerFields?.Add(fieldInfo);
                        SelectedUniqueField = MainLayerFields?.FirstOrDefault();
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

        private double CalculateArea(Polygon polygon)
        {
            if (polygon == null || polygon.IsEmpty) return 0;
            if (polygon.SpatialReference != null && polygon.SpatialReference.IsGeographic)
                return Math.Abs(GeometryEngine.Instance.GeodesicArea(polygon));
            return Math.Abs(polygon.Area);
        }

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

        private string GetAreaFieldName()
        {
            return SelectedAreaUnit switch
            {
                "平方米" => "Area_M2",
                "公顷" => "Area_Ha",
                "亩" => "Area_Mu",
                _ => "Area_M2"
            };
        }
    }
}
