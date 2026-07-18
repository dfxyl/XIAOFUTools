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
        /// 异步运行修复几何
        /// </summary>
        private async Task RunAsync()
        {
            try
            {
                IsProcessing = true;
                CancelRequested = false;
                Progress = 0;
                IsProgressIndeterminate = false;

                var selectedLayers = LayerList.Where(l => l.IsSelected).ToList();
                if (selectedLayers.Count == 0)
                {
                    StatusMessage = "请选择要修复的图层";
                    return;
                }

                StatusMessage = $"开始修复 {selectedLayers.Count} 个图层的几何...";
                LogInfo($"开始批量修复几何，共 {selectedLayers.Count} 个图层");

                int processedCount = 0;
                int totalCount = selectedLayers.Count;

                foreach (var layerInfo in selectedLayers)
                {
                    if (CancelRequested)
                    {
                        StatusMessage = "操作已取消";
                        LogWarning("用户取消了操作");
                        break;
                    }

                    try
                    {
                        StatusMessage = $"正在修复: {layerInfo.LayerName}";
                        LogInfo($"开始修复图层: {layerInfo.LayerName}");

                        await RepairGeometryForLayer(layerInfo.Layer as FeatureLayer);

                        processedCount++;
                        Progress = (int)((double)processedCount / totalCount * 100);

                        LogInfo($"完成修复图层: {layerInfo.LayerName}");
                    }
                    catch (Exception ex)
                    {
                        LogError($"修复图层 {layerInfo.LayerName} 时出错: {ex.Message}");
                    }
                }

                if (!CancelRequested)
                {
                    StatusMessage = $"修复完成，共处理 {processedCount} 个图层";
                    LogInfo($"批量修复几何完成，共处理 {processedCount} 个图层");
                    Progress = 100;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"处理出错: {ex.Message}";
                LogError($"批量修复几何出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                Progress = 0;
            }
        }
    }
}
