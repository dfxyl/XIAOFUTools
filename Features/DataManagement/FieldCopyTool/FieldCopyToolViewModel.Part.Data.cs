using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using System.Threading;
using ArcGIS.Desktop.Core.Geoprocessing;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.FieldCopyTool
{
    public partial class FieldCopyToolViewModel
    {

        /// <summary>
        /// 加载图层
        /// </summary>
        private async void LoadLayers()
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        var map = MapView.Active?.Map;
                        if (map == null) return;

                        var featureLayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();

                        PresentationServices.UiThread.InvokeOrRun(() =>
                        {
                            // 加载源图层列表
                            SourceLayerList.Clear();
                            foreach (var layer in featureLayers)
                            {
                                SourceLayerList.Add(layer);
                            }

                            // 加载目标图层列表
                            TargetLayerList.Clear();
                            foreach (var layer in featureLayers)
                            {
                                TargetLayerList.Add(new LayerInfo { Layer = layer });
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        LogError($"加载图层时出错: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载字段
        /// </summary>
        private async void LoadFields()
        {
            try
            {
                if (SelectedSourceLayer == null)
                {
                    FieldList.Clear();
                    return;
                }

                await QueuedTask.Run(() =>
                {
                    try
                    {
                        using (var table = SelectedSourceLayer.GetTable())
                        {
                            var tableDefinition = table.GetDefinition();
                            var fields = tableDefinition.GetFields();

                            PresentationServices.UiThread.InvokeOrRun(() =>
                            {
                                FieldList.Clear();
                                foreach (var field in fields)
                                {
                                    // 排除系统字段（按名称）
                                    if (field.Name.ToUpper() == "OBJECTID" ||
                                        field.Name.ToUpper() == "SHAPE" ||
                                        field.Name.ToUpper() == "SHAPE_LENGTH" ||
                                        field.Name.ToUpper() == "SHAPE_AREA")
                                        continue;

                                    // 排除不支持的字段类型
                                    if (field.FieldType == FieldType.Geometry ||
                                        field.FieldType == FieldType.OID ||
                                        field.FieldType == FieldType.GlobalID)
                                        continue;

                                    FieldList.Add(new FieldInfo
                                    {
                                        Name = field.Name,
                                        Alias = field.AliasName,
                                        FieldType = field.FieldType,
                                        Length = field.Length,
                                        IsSelected = false
                                    });
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"加载字段时出错: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 目标图层全选
        /// </summary>
        private void SelectAllTargetLayers()
        {
            foreach (var layer in TargetLayerList)
            {
                layer.IsSelected = true;
            }
        }

        /// <summary>
        /// 目标图层反选
        /// </summary>
        private void InvertTargetLayerSelection()
        {
            foreach (var layer in TargetLayerList)
            {
                layer.IsSelected = !layer.IsSelected;
            }
        }

        /// <summary>
        /// 将FieldType转换为Geoprocessing工具所需的字符串类型
        /// </summary>
        private string ConvertFieldTypeToString(FieldType fieldType)
        {
            switch (fieldType)
            {
                case FieldType.SmallInteger:
                    return "SHORT";
                case FieldType.Integer:
                    return "LONG";
                case FieldType.Single:
                    return "FLOAT";
                case FieldType.Double:
                    return "DOUBLE";
                case FieldType.String:
                    return "TEXT";
                case FieldType.Date:
                    return "DATE";
                case FieldType.Blob:
                    return "BLOB";
                case FieldType.Raster:
                    return "RASTER";
                case FieldType.GUID:
                    return "GUID";
                default:
                    return "TEXT"; // 默认为文本类型
            }
        }
    }
}
