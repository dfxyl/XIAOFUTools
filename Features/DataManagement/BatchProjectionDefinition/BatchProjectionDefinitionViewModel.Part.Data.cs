using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Geometry;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Collections.Generic;

namespace XIAOFUTools.Features.DataManagement.BatchProjectionDefinition
{
    internal partial class BatchProjectionDefinitionViewModel
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
                    var tempLayers = new List<LayerProjectionInfo>();
                    var map = MapView.Active?.Map;
                    
                    if (map != null)
                    {
                        var layers = map.GetLayersAsFlattenedList().ToList();
                        
                        foreach (var layer in layers)
                        {
                            // 排除网络图层
                            if (layer is ServiceLayer) continue;
                            
                            var layerInfo = new LayerProjectionInfo
                            {
                                Layer = layer,
                                LayerName = layer.Name,
                                LayerType = GetLayerType(layer),
                                CurrentCoordinateSystem = GetLayerCoordinateSystem(layer),
                                IsSelected = false
                            };
                            
                            tempLayers.Add(layerInfo);
                        }
                    }
                    
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        // 清空图层列表
                        LayerList.Clear();
                        
                        // 添加图层
                        foreach (var layerInfo in tempLayers)
                        {
                            LayerList.Add(layerInfo);
                        }
                        
                        LogMessage($"已加载 {LayerList.Count} 个图层");
                    });
                }
                catch (Exception ex)
                {
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
        private string GetLayerType(Layer layer)
        {
            return layer switch
            {
                FeatureLayer => "要素图层",
                RasterLayer => "栅格图层",
                ImageServiceLayer => "影像服务图层",
                _ => layer.GetType().Name
            };
        }

        /// <summary>
        /// 获取图层坐标系
        /// </summary>
        private string GetLayerCoordinateSystem(Layer layer)
        {
            try
            {
                SpatialReference spatialRef = null;
                
                if (layer is FeatureLayer featureLayer)
                {
                    spatialRef = featureLayer.GetSpatialReference();
                }
                else if (layer is RasterLayer rasterLayer)
                {
                    spatialRef = rasterLayer.GetSpatialReference();
                }
                
                return spatialRef?.Name ?? "未知";
            }
            catch
            {
                return "未知";
            }
        }
    }
}
