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
        /// 执行范围裁剪
        /// </summary>
        private async Task PerformRangeClip()
        {
            try
            {
                // 使用用户选择的输出文件夹
                string baseOutputFolder = OutputFolder;

                _outputFolderStore.EnsureDirectory(baseOutputFolder);

                LogInfo($"输出文件夹: {baseOutputFolder}");

                // 获取范围图层的唯一字段值
                var rangeValues = await GetUniqueRangeValues();
                if (rangeValues.Count == 0)
                {
                    LogWarning("范围图层中没有找到有效的字段值");
                    return;
                }

                LogInfo($"找到 {rangeValues.Count} 个范围值");

                // 获取选中的裁剪图层
                var selectedLayers = ClipLayerItems.Where(x => x.IsSelected).ToList();
                LogInfo($"选中 {selectedLayers.Count} 个图层进行裁剪");

                int totalOperations = rangeValues.Count * selectedLayers.Count;
                int currentOperation = 0;

                // 更新进度条为确定模式
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    IsProgressIndeterminate = false;
                    Progress = 0;
                });

                // 对每个范围值进行处理
                foreach (var rangeValue in rangeValues)
                {
                    if (CancelRequested) break;

                    string rangeValueStr = rangeValue?.ToString() ?? "空值";
                    LogInfo($"处理范围值: {rangeValueStr}");

                    // 创建子文件夹（如果需要）
                    string currentOutputFolder = baseOutputFolder;
                    if (CreateSubFolder)
                    {
                        string safeFolderName = GetSafeFileName(rangeValueStr);
                        currentOutputFolder = Path.Combine(baseOutputFolder, safeFolderName);
                        _outputFolderStore.EnsureDirectory(currentOutputFolder);
                    }

                    // 创建范围几何体
                    var rangeGeometry = await CreateRangeGeometry(rangeValue);
                    if (rangeGeometry == null)
                    {
                        LogWarning($"无法为范围值 {rangeValueStr} 创建几何体");
                        continue;
                    }

                    // 对每个选中的图层进行裁剪
                    foreach (var layerItem in selectedLayers)
                    {
                        if (CancelRequested) break;

                        currentOperation++;
                        int progressPercent = (int)((double)currentOperation / totalOperations * 100);

                        PresentationServices.UiThread.InvokeOrRun(() =>
                        {
                            Progress = progressPercent;
                            StatusMessage = $"正在裁剪 {layerItem.LayerName} (范围: {rangeValueStr})...";
                        });

                        await ClipLayerWithRange(layerItem.Layer, rangeGeometry, rangeValueStr, currentOutputFolder);
                    }
                }

                if (!CancelRequested)
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        Progress = 100;
                        StatusMessage = "根据范围批量裁剪完成。";
                    });
                    LogInfo("所有裁剪操作完成");
                }
            }
            catch (Exception ex)
            {
                LogError($"执行范围裁剪时出错: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 使用范围裁剪图层
        /// </summary>
        private async Task ClipLayerWithRange(FeatureLayer layer, Geometry rangeGeometry, string rangeValueStr, string outputFolder)
        {
            try
            {
                // 保持原始图层名称，不添加范围值
                string safeFileName = GetSafeFileName(layer.Name);
                string outputPath = Path.Combine(outputFolder, $"{safeFileName}.shp");

                LogInfo($"开始裁剪图层: {layer.Name} -> {outputPath}");

                // 使用地理处理工具进行裁剪
                var parameters = Geoprocessing.MakeValueArray(
                    layer,               // 输入要素（直接传递图层对象，避免名称解析问题）
                    rangeGeometry,       // 裁剪要素（几何）
                    outputPath,          // 输出要素类
                    ""                   // XY 容差
                );

                var environments = Geoprocessing.MakeEnvironmentArray("addOutputsToMap", "False", "overwriteoutput", "True");

                var result = await Geoprocessing.ExecuteToolAsync("analysis.Clip", parameters, environments, null, null, GPExecuteToolFlags.AddToHistory);

                if (result == null)
                {
                    LogError($"裁剪失败: {layer.Name} - GP 结果为空");
                }
                else if (result.IsFailed)
                {
                    LogError($"裁剪失败: {layer.Name}");
                    foreach (var msg in result.Messages)
                    {
                        switch (msg.Type)
                        {
                            case GPMessageType.Error:
                                LogError($"GP错误: {msg.Text}");
                                break;
                            case GPMessageType.Warning:
                                LogWarning($"GP警告: {msg.Text}");
                                break;
                            default:
                                LogInfo($"GP信息: {msg.Text}");
                                break;
                        }
                    }
                }
                else
                {
                    foreach (var msg in result.Messages)
                    {
                        if (msg.Type == GPMessageType.Warning)
                            LogWarning($"GP警告: {msg.Text}");
                    }
                    LogInfo($"裁剪成功: {layer.Name} -> {outputPath}");
                }
            }
            catch (Exception ex)
            {
                LogError($"裁剪图层时出错: {layer.Name} - {ex.Message}");
            }
        }
    }
}
