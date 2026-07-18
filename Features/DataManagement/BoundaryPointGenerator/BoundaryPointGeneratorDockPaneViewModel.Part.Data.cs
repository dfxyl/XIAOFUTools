using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.BoundaryPointGenerator
{
    internal partial class BoundaryPointGeneratorDockPaneViewModel
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
                    var layers = await LayerUtils.GetPolygonLayersAsync();
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var fl in layers) PolygonLayers.Add(fl);
                        if (PolygonLayers.Count > 0)
                            SelectedPolygonLayer = PolygonLayers[0];
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
        
        /// <summary>
        /// 获取当前项目地理数据库路径
        /// </summary>
        private string GetProjectGDBPath()
        {
            return PathDialogUtils.GetProjectDefaultGdb();
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
    }
}
