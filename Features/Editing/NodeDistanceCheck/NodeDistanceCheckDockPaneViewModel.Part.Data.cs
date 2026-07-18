using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Editing;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.NodeDistanceCheck
{
    internal partial class NodeDistanceCheckDockPaneViewModel
    {

        /// <summary>
        /// 获取当前项目地理数据库路径
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
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取项目地理数据库路径失败: {ex.Message}");
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        /// <summary>
        /// 获取地理处理工具的字段类型
        /// </summary>
        private string GetGeoprocessingFieldType(FieldType fieldType)
        {
            return fieldType switch
            {
                FieldType.String => "TEXT",
                FieldType.Integer => "LONG",
                FieldType.SmallInteger => "SHORT",
                FieldType.Double => "DOUBLE",
                FieldType.Single => "FLOAT",
                FieldType.Date => "DATE",
                FieldType.GUID => "GUID",
                FieldType.GlobalID => "GUID",
                _ => "TEXT"
            };
        }

        /// <summary>
        /// 加载面图层
        /// </summary>
        private void LoadPolygonLayers()
        {
            QueuedTask.Run(() =>
            {
                try
                {
                    var tempLayers = new List<FeatureLayer>();
                    var map = MapView.Active?.Map;

                    if (map != null)
                    {
                        var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();

                        foreach (var layer in layers)
                        {
                            try
                            {
                                using (var table = layer.GetTable())
                                {
                                    if (table != null)
                                    {
                                        var definition = table.GetDefinition() as FeatureClassDefinition;
                                        if (definition?.GetShapeType() == GeometryType.Polygon)
                                        {
                                            tempLayers.Add(layer);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError($"无法访问图层 {layer.Name}: {ex.Message}");
                            }
                        }
                    }

                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        PolygonLayers?.Clear();
                            if (PolygonLayers != null)
                            {
                                foreach (var layer in tempLayers)
                                {
                                    PolygonLayers.Add(layer);
                                }

                                if (PolygonLayers.Count > 0)
                                {
                                    SelectedPolygonLayer = PolygonLayers[0];
                                    StatusMessage = "请设置检查参数，然后点击开始。";
                                }
                                else
                                {
                                    StatusMessage = "未找到面要素图层，请先添加面图层到地图。";
                                }
                            }
                    });
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                    });
                }
            });
        }
    }
}
