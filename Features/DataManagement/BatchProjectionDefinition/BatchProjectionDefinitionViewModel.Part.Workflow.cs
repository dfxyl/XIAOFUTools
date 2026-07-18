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
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Collections.Generic;

namespace XIAOFUTools.Features.DataManagement.BatchProjectionDefinition
{
    internal partial class BatchProjectionDefinitionViewModel
    {

        /// <summary>
        /// 执行批量定义投影
        /// </summary>
        private async Task RunAsync()
        {
            if (SelectedSpatialReference == null)
            {
                LogError("请先选择坐标系");
                return;
            }

            var selectedLayers = LayerList.Where(l => l.IsSelected).ToList();
            if (selectedLayers.Count == 0)
            {
                LogError("请至少选择一个图层");
                return;
            }

            IsProcessing = true;
            CancelRequested = false;
            Progress = 0;
            IsProgressIndeterminate = false;
            
            try
            {
                StatusMessage = "正在定义投影...";
                LogMessage($"开始为 {selectedLayers.Count} 个图层定义投影");
                LogMessage($"目标坐标系: {SelectedSpatialReference.Name}");

                int processedCount = 0;
                int totalCount = selectedLayers.Count;

                foreach (var layerInfo in selectedLayers)
                {
                    if (CancelRequested)
                    {
                        LogMessage("操作已取消");
                        break;
                    }

                    try
                    {
                        await DefineProjectionForLayer(layerInfo.Layer);
                        processedCount++;
                        Progress = (int)((double)processedCount / totalCount * 100);
                        LogMessage($"已完成: {layerInfo.LayerName}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"处理图层 {layerInfo.LayerName} 时出错: {ex.Message}");
                    }
                }

                if (!CancelRequested)
                {
                    StatusMessage = $"完成！已处理 {processedCount} 个图层";
                    LogMessage($"批量定义投影完成，共处理 {processedCount} 个图层");
                    
                    // 刷新图层列表以更新坐标系信息
                    RefreshLayers();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"处理出错: {ex.Message}";
                LogError($"批量定义投影出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                Progress = 0;
            }
        }
    }
}
