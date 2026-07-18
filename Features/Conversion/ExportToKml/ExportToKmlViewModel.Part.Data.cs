using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO.Compression;
using System.Xml;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Conversion.ExportToKml
{
    internal partial class ExportToKmlViewModel
    {

        private async void LoadFeatureLayers()
        {
            try
            {
                var layers = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null) return new List<FeatureLayer>();
                    return map.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .ToList();
                });

                FeatureLayers.Clear();

                if (layers.Count == 0)
                {
                    AddLog("当前没有活动地图");
                    return;
                }

                foreach (var layer in layers)
                {
                    FeatureLayers.Add(layer);
                }

                // 如果有图层，默认选择第一个
                if (FeatureLayers.Count > 0 && SelectedInputLayer == null)
                {
                    SelectedInputLayer = FeatureLayers[0];
                }

                NotifyPropertyChanged(nameof(FeatureLayers));
                NotifyPropertyChanged(nameof(CanProcess));
            }
            catch (Exception ex)
            {
                AddLog($"加载图层时出错: {ex.Message}");
            }
        }

        private async void LoadGroupFields()
        {
            try
            {
                var previousGroupField = SelectedGroupField;
                var previousLabelField = SelectedLabelField;

                PresentationServices.UiThread.Invoke(() =>
                {
                    GroupFields.Clear();
                    LabelFields.Clear();
                    
                    // 添加"不分组"选项
                    GroupFields.Add(NoGroupFieldOption);
                });

                var featureLayer = SelectedInputLayer as FeatureLayer;
                if (featureLayer == null)
                {
                    PresentationServices.UiThread.Invoke(() =>
                    {
                        SelectedGroupField = NoGroupFieldOption;
                        SelectedLabelField = null;
                    });
                    return;
                }

                await QueuedTask.Run(() =>
                {
                    using var table = featureLayer.GetTable();
                    var tableDefinition = table.GetDefinition();
                    var fields = tableDefinition.GetFields();

                    // 分组字段：仅支持字符串和整数类型
                    var groupFieldNames = fields
                        .Where(f => f.FieldType == FieldType.String || 
                                   f.FieldType == FieldType.Integer || 
                                   f.FieldType == FieldType.SmallInteger ||
                                   f.FieldType == FieldType.BigInteger)
                        .Select(f => f.Name)
                        .ToList();

                    // 标注字段：支持所有非几何/Blob类型
                    var labelFieldNames = fields
                        .Where(f => f.FieldType != FieldType.Geometry && 
                                   f.FieldType != FieldType.Blob &&
                                   f.FieldType != FieldType.Raster &&
                                   f.FieldType != FieldType.OID)
                        .Select(f => f.Name)
                        .ToList();

                    PresentationServices.UiThread.Invoke(() =>
                    {
                        foreach (var fieldName in groupFieldNames)
                        {
                            GroupFields.Add(fieldName);
                        }
                        
                        foreach (var fieldName in labelFieldNames)
                        {
                            LabelFields.Add(fieldName);
                        }
                        
                        SelectedGroupField = ExportToKmlFieldSelection.ResolveSelectedGroupField(
                            previousGroupField,
                            GroupFields,
                            NoGroupFieldOption);
                        SelectedLabelField = ExportToKmlFieldSelection.ResolveSelectedLabelField(
                            previousLabelField,
                            LabelFields);
                        
                        // 通知属性更新
                        NotifyPropertyChanged(nameof(GroupFields));
                        NotifyPropertyChanged(nameof(LabelFields));
                        NotifyPropertyChanged(nameof(SelectedLabelField));
                    });
                });
            }
            catch (Exception ex)
            {
                AddLog($"加载字段时出错: {ex.Message}");
            }
        }

        private string GetOutputExtension()
        {
            return SelectedExportFormat == "KMZ" ? "kmz" : "kml";
        }

        private static FeatureLayer ResolveFeatureLayer(object input)
        {
            if (input is FeatureLayer featureLayer)
            {
                return featureLayer;
            }

            return input as Layer as FeatureLayer;
        }

        private static MapPoint GetGeometryLabelPoint(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty)
            {
                return null;
            }

            switch (geometry)
            {
                case MapPoint point:
                    return point;

                case Multipoint multipoint when multipoint.PointCount > 0:
                    return multipoint.Points[0];

                case Polygon polygon:
                    {
                        var centroid = GeometryEngine.Instance.Centroid(polygon) as MapPoint;
                        if (centroid != null && !centroid.IsEmpty && GeometryEngine.Instance.Contains(polygon, centroid))
                        {
                            return centroid;
                        }

                        try
                        {
                            var labelPoint = GeometryEngine.Instance.LabelPoint(polygon);
                            if (labelPoint != null && !labelPoint.IsEmpty)
                            {
                                return labelPoint;
                            }
                        }
                        catch
                        {
                            // 忽略异常，使用兜底点
                        }

                        return centroid ?? polygon.Extent?.Center;
                    }

                case Polyline polyline:
                    return polyline.Extent?.Center;

                default:
                    return geometry.Extent?.Center;
            }
        }

    }
}
