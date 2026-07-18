using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using System.Globalization;
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
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.BatchLayerClip
{
    internal partial class BatchLayerClipViewModel
    {
        
        /// <summary>
        /// 获取当前项目文件夹路径
        /// </summary>
        private string GetProjectFolderPath()
        {
            string projectFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); // 默认路径
            
            try
            {
                // 尝试获取当前项目路径
                var project = Project.Current;
                if (project != null && !string.IsNullOrEmpty(project.Path))
                {
                    // 获取项目文件夹路径
                    projectFolder = System.IO.Path.GetDirectoryName(project.Path);
                    
                    // 如果项目文件夹存在，使用此路径
                    if (_fileStore.DirectoryExists(projectFolder))
                    {
                        return projectFolder;
                    }
                }
                return projectFolder;
            }
            catch (Exception ex)
            {
                // 如果无法获取项目路径，使用默认文档路径
                System.Diagnostics.Debug.WriteLine($"获取项目路径失败: {ex.Message}");
                return projectFolder;
            }
        }

        /// <summary>
        /// 加载要素图层
        /// </summary>
        private void LoadFeatureLayers()
        {
            QueuedTask.Run(() =>
            {
                try 
                {
                    // 获取所有图层的临时列表
                    var tempLayers = new List<FeatureLayer>();
                    var map = MapView.Active?.Map;
                    
                    if (map != null)
                    {
                        var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                        tempLayers.AddRange(layers);
                    }
                    
                    // 在UI线程更新图层列表
                    PresentationServices.UiThread.InvokeOrRun(() => 
                    {
                        // 清空图层列表
                        FeatureLayers.Clear();
                        
                        // 添加图层
                        foreach (var layer in tempLayers)
                        {
                            FeatureLayers.Add(layer);
                        }
                        
                        // 如果有图层，默认选择第一个
                        if (FeatureLayers.Count > 0)
                        {
                            SelectedFeatureLayer = FeatureLayers[0];
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

        private string GetActualFieldName(string selectedField)
        {
            if (string.IsNullOrWhiteSpace(selectedField))
                throw new InvalidOperationException("请先选择分组字段。");

            int aliasIndex = selectedField.IndexOf("(", StringComparison.Ordinal);
            if (aliasIndex > 0)
            {
                string actual = selectedField.Substring(0, aliasIndex).Trim();
                if (!string.IsNullOrWhiteSpace(actual))
                    return actual;
            }

            return selectedField.Trim();
        }
    }
}
