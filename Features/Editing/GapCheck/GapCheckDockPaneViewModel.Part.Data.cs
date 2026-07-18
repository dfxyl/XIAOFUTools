using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.GapCheck
{
    internal partial class GapCheckDockPaneViewModel
    {
        /// <summary>
        /// 加载面要素图层
        /// </summary>
        private async void LoadPolygonLayers()
        {
            try
            {
                var layers = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null) return new List<FeatureLayer>();
                    return map.GetLayersAsFlattenedList()
                        ?.OfType<FeatureLayer>()
                        .Where(fl => fl != null && fl.ShapeType == esriGeometryType.esriGeometryPolygon)
                        .ToList() ?? new List<FeatureLayer>();
                });

                // 确保PolygonLayers不为null
                if (PolygonLayers == null)
                {
                    PolygonLayers = new ObservableCollection<FeatureLayer>();
                }
                else
                {
                    PolygonLayers.Clear();
                }

                if (layers.Count == 0)
                {
                    AddLog("当前没有活动地图");
                    return;
                }

                foreach (var layer in layers)
                {
                    PolygonLayers.Add(layer);
                }

                AddLog($"已加载 {PolygonLayers.Count} 个面要素图层");

                // 如果有图层，默认选择第一个
                if (PolygonLayers.Count > 0)
                {
                    SelectedPolygonLayer = PolygonLayers[0];
                }

                // 设置默认输出路径
                UpdateOutputPath();
            }
            catch (Exception ex)
            {
                AddLog($"加载图层时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取项目默认地理数据库路径
        /// </summary>
        private string GetProjectGDBPath()
        {
            try
            {
                var project = Project.Current;
                if (project != null)
                {
                    return project.DefaultGeodatabasePath;
                }
            }
            catch (Exception ex)
            {
                AddLog($"获取项目地理数据库路径失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 安全获取图层名称
        /// </summary>
        private string GetSafeLayerName(Layer layer)
        {
            try
            {
                if (layer == null) return null;
                return layer.Name;
            }
            catch (Exception ex)
            {
                AddLog($"获取图层名称时出错: {ex.Message}");
                return null;
            }
        }
    }
}
