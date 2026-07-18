using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BatchGeometryRepair
{
    internal partial class BatchGeometryRepairViewModel
    {

        /// <summary>
        /// 加载图层
        /// </summary>
        private void LoadLayers()
        {
            QueuedTask.Run(() =>
            {
                try 
                {
                    // 获取所有图层的临时列表
                    var tempLayers = new List<LayerGeometryInfo>();
                    var map = MapView.Active?.Map;
                    
                    if (map != null)
                    {
                        // 只获取要素图层，因为只有要素图层需要修复几何
                        var featureLayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                        
                        foreach (var layer in featureLayers)
                        {
                            var layerInfo = new LayerGeometryInfo
                            {
                                Layer = layer,
                                LayerName = layer.Name,
                                LayerType = GetLayerType(layer),
                                CoordinateSystem = GetLayerCoordinateSystem(layer),
                                IsSelected = false
                            };
                            
                            tempLayers.Add(layerInfo);
                        }
                    }
                    
                    // 在UI线程更新图层列表
                    PresentationServices.UiThread.InvokeOrRun(() => 
                    {
                        // 清空图层列表
                        LayerList.Clear();
                        
                        // 添加图层
                        foreach (var layer in tempLayers)
                        {
                            LayerList.Add(layer);
                        }
                        
                        StatusMessage = $"加载了 {LayerList.Count} 个要素图层";
                        LogInfo($"加载了 {LayerList.Count} 个要素图层");
                    });
                }
                catch (Exception ex)
                {
                    // 确保在UI线程显示错误信息
                    PresentationServices.UiThread.InvokeOrRun(() => 
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                        LogError($"加载图层出错: {ex.Message}");
                    });
                }
            });
        }

        /// <summary>
        /// 获取图层类型
        /// </summary>
        private string GetLayerType(FeatureLayer layer)
        {
            try
            {
                var geometryType = layer.GetFeatureClass()?.GetDefinition()?.GetShapeType();
                return geometryType switch
                {
                    ArcGIS.Core.Geometry.GeometryType.Point => "点图层",
                    ArcGIS.Core.Geometry.GeometryType.Polyline => "线图层",
                    ArcGIS.Core.Geometry.GeometryType.Polygon => "面图层",
                    ArcGIS.Core.Geometry.GeometryType.Multipoint => "多点图层",
                    _ => "要素图层"
                };
            }
            catch
            {
                return "要素图层";
            }
        }

        /// <summary>
        /// 获取图层坐标系
        /// </summary>
        private string GetLayerCoordinateSystem(FeatureLayer layer)
        {
            try
            {
                var spatialRef = layer.GetSpatialReference();
                return spatialRef?.Name ?? "未知";
            }
            catch
            {
                return "未知";
            }
        }
    }
}
