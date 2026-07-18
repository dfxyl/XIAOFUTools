using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.Boundary.BoundaryPointLineGenerator
{
    internal partial class BoundaryPointLineGeneratorDockPaneViewModel
    {

        private async void Execute()
        {
            if (IsProcessing) return;
            CancelRequested = false; IsProcessing = true; IsProgressIndeterminate = true; Progress = 0; ClearLog();
            StatusMessage = "正在生成...";
            try
            {
                await QueuedTask.Run(async () =>
                {
                    if (SelectedPolygonLayer == null) { LogError("未选择面图层"); return; }

                    var sr = LayerUtils.GetSpatialReference(SelectedPolygonLayer);
                    var baseName = SelectedPolygonLayer.Name;

                    bool genJZD = SelectedOutputType.Contains("JZD");
                    bool genJZX = SelectedOutputType.Contains("JZX");

                    // JZD
                    if (genJZD)
                    {
                        var infoJZD = OutputDatasetUtils.ParseOutputPath(OutputPathJZD, $"{baseName}_JZD");
                        if (OutputDatasetUtils.Exists(infoJZD))
                        {
                            bool overwrite = false;
                            PresentationServices.UiThread.InvokeOrRun(() =>
                            {
                                var msg = infoJZD.IsGdb ? $"目标要素类已存在：{infoJZD.CatalogPath}。是否覆盖？" : $"目标Shapefile已存在：{infoJZD.CatalogPath}。是否覆盖？";
                                var result = PresentationServices.Dialogs.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                                overwrite = result == System.Windows.MessageBoxResult.Yes;
                            });
                            if (!overwrite)
                            {
                                LogWarning("用户取消覆盖，操作已中止。");
                                return;
                            }
                            try { await OutputDatasetUtils.DeleteIfExistsAsync(infoJZD); LogInfo($"删除已存在的数据集: {infoJZD.CatalogPath}"); } catch (Exception delEx) { LogWarning($"删除已有数据集失败: {delEx.Message}"); }
                        }

                        var pathJZD = await OutputDatasetUtils.CreateFeatureClassAsync(infoJZD, "POINT", sr);
                        await AddJZDFields(pathJZD);
                        await GeneratePointFeatures(pathJZD);
                    }

                    // JZX
                    if (genJZX)
                    {
                        var infoJZX = OutputDatasetUtils.ParseOutputPath(OutputPathJZX, $"{baseName}_JZX");
                        if (OutputDatasetUtils.Exists(infoJZX))
                        {
                            bool overwrite = false;
                            PresentationServices.UiThread.InvokeOrRun(() =>
                            {
                                var msg = infoJZX.IsGdb ? $"目标要素类已存在：{infoJZX.CatalogPath}。是否覆盖？" : $"目标Shapefile已存在：{infoJZX.CatalogPath}。是否覆盖？";
                                var result = PresentationServices.Dialogs.Show(msg, "覆盖确认", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                                overwrite = result == System.Windows.MessageBoxResult.Yes;
                            });
                            if (!overwrite)
                            {
                                LogWarning("用户取消覆盖，操作已中止。");
                                return;
                            }
                            try { await OutputDatasetUtils.DeleteIfExistsAsync(infoJZX); LogInfo($"删除已存在的数据集: {infoJZX.CatalogPath}"); } catch (Exception delEx) { LogWarning($"删除已有数据集失败: {delEx.Message}"); }
                        }

                        var pathJZX = await OutputDatasetUtils.CreateFeatureClassAsync(infoJZX, "POLYLINE", sr);
                        await AddJZXFields(pathJZX);
                        await GenerateLineFeatures(pathJZX);
                    }

                    if (!CancelRequested)
                    {
                        LogInfo("生成完成");
                        PresentationServices.UiThread.InvokeOrRun(() => { Progress = 100; IsProgressIndeterminate = false; StatusMessage = "完成"; });
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"执行失败: {ex.Message}");
            }
            finally { IsProcessing = false; IsProgressIndeterminate = false; }
        }
    }
}
