using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Shared;
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

namespace XIAOFUTools.Features.DataManagement.RangeClipTool
{
    internal partial class RangeClipToolViewModel
    {

        /// <summary>
        /// 获取项目文件夹路径
        /// </summary>
        private string GetProjectFolderPath()
        {
            try
            {
                var project = Project.Current;
                if (project != null)
                {
                    return Path.GetDirectoryName(project.Path);
                }
            }
            catch (Exception ex)
            {
                LogError($"获取项目文件夹路径失败: {ex.Message}");
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

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
                    var tempRangeLayers = new List<FeatureLayer>();
                    var tempClipLayers = new List<FeatureLayer>();
                    var map = MapView.Active?.Map;

                    if (map != null)
                    {
                        var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                        tempRangeLayers.AddRange(layers);
                        tempClipLayers.AddRange(layers);
                    }

                    // 在UI线程更新图层列表
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        // 清空图层列表
                        RangeLayers.Clear();
                        ClipLayerItems.Clear();

                        // 添加范围图层
                        foreach (var layer in tempRangeLayers)
                        {
                            RangeLayers.Add(layer);
                        }

                        // 添加裁剪图层项目
                        foreach (var layer in tempClipLayers)
                        {
                            ClipLayerItems.Add(new ClipLayerItem
                            {
                                LayerName = layer.Name,
                                Layer = layer,
                                IsSelected = false
                            });
                        }

                        // 如果有图层，默认选择第一个范围图层
                        if (RangeLayers.Count > 0)
                        {
                            SelectedRangeLayer = RangeLayers[0];
                        }
                    });
                }
                catch (Exception ex)
                {
                    // 确保在UI线程显示错误信息
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                    });
                }
            });
        }

        /// <summary>
        /// 加载范围字段
        /// </summary>
        private void LoadRangeFields()
        {
            if (SelectedRangeLayer == null)
            {
                RangeFieldNames.Clear();
                return;
            }

            QueuedTask.Run(() =>
            {
                try
                {
                    // 清空字段列表
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        RangeFieldNames.Clear();
                    });

                    // 获取字段的临时列表
                    var tempFields = new List<string>();

                    // 获取图层定义
                    var layerDef = SelectedRangeLayer.GetFeatureClass().GetDefinition();

                    // 遍历字段，只添加文本和数值字段
                    foreach (var field in layerDef.GetFields())
                    {
                        if (field.FieldType == FieldType.String ||
                            field.FieldType == FieldType.Integer ||
                            field.FieldType == FieldType.SmallInteger ||
                            field.FieldType == FieldType.Double ||
                            field.FieldType == FieldType.Single)
                        {
                            tempFields.Add(field.Name);
                        }
                    }

                    // 在UI线程更新字段列表
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        // 将临时列表中的字段添加到字段名称列表
                        foreach (var field in tempFields)
                        {
                            RangeFieldNames.Add(field);
                        }

                        // 如果有字段，默认选择第一个
                        if (RangeFieldNames.Count > 0)
                        {
                            SelectedRangeField = RangeFieldNames[0];
                        }
                    });
                }
                catch (Exception ex)
                {
                    // 确保在UI线程显示错误信息
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        StatusMessage = $"加载字段出错: {ex.Message}";
                        LogError($"加载字段出错: {ex.Message}");
                    });
                }
            });
        }

        /// <summary>
        /// 获取范围字段的唯一值
        /// </summary>
        private Task<List<object>> GetUniqueRangeValues()
        {
            var uniqueValues = new List<object>();

            try
            {
                var featureClass = SelectedRangeLayer.GetFeatureClass();
                var queryFilter = new QueryFilter();

                using (var cursor = featureClass.Search(queryFilter, false))
                {
                    while (cursor.MoveNext())
                    {
                        if (CancelRequested) break;

                        using (var feature = cursor.Current)
                        {
                            var value = feature[SelectedRangeField];
                            if (!uniqueValues.Contains(value))
                            {
                                uniqueValues.Add(value);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"获取唯一值时出错: {ex.Message}");
            }

            return Task.FromResult(uniqueValues);
        }

        /// <summary>
        /// 获取安全的文件名
        /// </summary>
        private string GetSafeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "未命名";

            // 替换非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // 替换其他可能有问题的字符
            fileName = fileName.Replace(' ', '_')
                              .Replace('.', '_')
                              .Replace(',', '_')
                              .Replace(';', '_')
                              .Replace(':', '_');

            // 限制长度
            if (fileName.Length > 50)
            {
                fileName = fileName.Substring(0, 50);
            }

            return fileName;
        }
    }
}
