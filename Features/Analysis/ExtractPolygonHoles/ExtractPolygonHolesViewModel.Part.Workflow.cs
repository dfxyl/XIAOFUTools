using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Analysis.ExtractPolygonHoles
{
    internal partial class ExtractPolygonHolesViewModel
    {

        private async Task RunAsync()
        {
            if (SelectedPolygonLayer == null)
            {
                AddLog("请选择输入面图层");
                return;
            }

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                AddLog("请指定输出面图层");
                return;
            }

            IsProcessing = true;
            StatusMessage = "正在提取面扣岛...";
            Progress = 0;
            _cts = new CancellationTokenSource();

            try
            {
                OutputPath = OutputDatasetUtils.NormalizeOutputPath(OutputPath, GetDefaultOutputName());

                var outputInfo = OutputDatasetUtils.ParseOutputPath(OutputPath, $"{SelectedPolygonLayer.Name}_扣岛");
                AddLog($"输出路径: {outputInfo.CatalogPath}");

                Progress = 10;
                await OutputDatasetUtils.DeleteIfExistsAsync(outputInfo);

                var env = Geoprocessing.MakeEnvironmentArray("addOutputsToMap", "False", "overwriteoutput", "True");
                var outName = outputInfo.IsGdb ? outputInfo.OutNameNoExt : outputInfo.OutNameNoExt + ".shp";
                var createParams = Geoprocessing.MakeValueArray(
                    outputInfo.OutPathWorkspace,
                    outName,
                    "POLYGON",
                    SelectedPolygonLayer);

                var createResult = await Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", createParams, env, _cts.Token);
                if (createResult.IsFailed)
                {
                    AddLog("创建输出要素类失败:");
                    foreach (var message in createResult.Messages)
                        AddLog($" - {message.Text}");
                    StatusMessage = "处理失败";
                    return;
                }

                Progress = 30;
                var stats = await QueuedTask.Run(() => ExtractAndWriteHoles(outputInfo, _cts.Token));

                Progress = 100;
                StatusMessage = "提取完成";
                AddLog($"处理完成：扫描 {stats.SourceFeatureCount} 个要素，提取 {stats.HoleRingCount} 个扣洞，输出 {stats.OutputFeatureCount} 个要素。");

                if (stats.OutputFeatureCount == 0)
                    AddLog("未发现扣洞，已生成空输出要素类。");
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
                AddLog("操作已取消");
            }
            catch (Exception ex)
            {
                StatusMessage = "处理失败";
                AddLog($"处理失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                _cts?.Dispose();
                _cts = null;
            }
        }
    }
}
