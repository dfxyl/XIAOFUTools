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
        /// 确定命令是否可以执行
        /// </summary>
        private bool CanExecute()
        {
            return SelectedFeatureLayer != null && 
                   !string.IsNullOrEmpty(SelectedField) && 
                   !string.IsNullOrEmpty(OutputFolder) && 
                   _fileStore.DirectoryExists(OutputFolder);
        }

        /// <summary>
        /// 执行批量裁剪操作
        /// </summary>
        private async void Execute()
        {
            if (SelectedFeatureLayer == null || string.IsNullOrWhiteSpace(SelectedField))
                return;

            CancelRequested = false;
            IsProcessing = true;
            StatusMessage = "正在处理...";
            ClearLog();
            Progress = 0;
            IsProgressIndeterminate = true;

            var layer = SelectedFeatureLayer;
            string fieldName = GetActualFieldName(SelectedField);

            try
            {
                LogInfo($"===== 开始批量裁剪操作 - {DateTime.Now} =====");
                LogInfo($"选择的图层: {layer.Name}");
                LogInfo($"选择的分组字段: {SelectedField}");
                LogInfo($"输出文件夹: {OutputFolder}");
                LogInfo($"是否创建子文件夹: {(CreateSubFolder ? "是" : "否")}");

                var preparationResult = await QueuedTask.Run(() => PrepareFieldGroups(layer, fieldName));
                LogInfo($"已生成 {preparationResult.Groups.Count} 个分组，准备导出...");

                var exportedCount = await ProcessGroupsAsync(layer, preparationResult);

                if (CancelRequested)
                {
                    LogWarning("操作已取消");
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        StatusMessage = "操作已取消";
                    });
                }
                else
                {
                    LogInfo($"===== 批量裁剪完成 - {DateTime.Now} =====");
                    LogInfo($"共导出 {exportedCount} 个分组至 {OutputFolder}");

                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        Progress = 100;
                        StatusMessage = $"导出完成! 共导出 {exportedCount} 个分组。";
                    });
                }
            }
            catch (OperationCanceledException)
            {
                LogWarning("操作已取消");
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    StatusMessage = "操作已取消";
                });
            }
            catch (Exception ex)
            {
                string errorMsg = $"处理出错: {ex.Message}";
                LogError(errorMsg);
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    StatusMessage = errorMsg;
                    Progress = 0;
                });
            }
            finally
            {
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    IsProcessing = false;
                    IsProgressIndeterminate = false;
                    if (CancelRequested && Progress < 100)
                    {
                        Progress = 0;
                    }
                });
            }
        }

        private async Task<int> ProcessGroupsAsync(FeatureLayer layer, GroupPreparationResult preparationResult)
        {
            if (preparationResult.Groups.Count == 0)
            {
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    StatusMessage = "未找到可导出的分组";
                    IsProgressIndeterminate = false;
                    Progress = 0;
                });
                return 0;
            }

            var envSettings = Geoprocessing.MakeEnvironmentArray("addOutputsToMap", "False", "overwriteoutput", "True");
            int total = preparationResult.Groups.Count;
            int currentIndex = 0;
            int completedCount = 0;

            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                IsProgressIndeterminate = false;
                Progress = 0;
            });

            foreach (var group in preparationResult.Groups)
            {
                if (CancelRequested)
                {
                    LogWarning("操作已取消，停止处理剩余分组");
                    break;
                }

                currentIndex++;
                int progressValue = (int)((double)currentIndex / total * 100);
                string statusText = $"正在导出 {group.DisplayValue} ({currentIndex}/{total})...";

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    StatusMessage = statusText;
                    Progress = progressValue;
                });

                string outputFolder = OutputFolder;
                if (CreateSubFolder)
                {
                    outputFolder = Path.Combine(OutputFolder, group.OutputName);
                    if (!_fileStore.DirectoryExists(outputFolder))
                    {
                        _fileStore.EnsureDirectory(outputFolder);
                        LogInfo($"创建目录: {outputFolder}");
                    }
                }

                string shapeName = group.OutputName;
                string whereClause = BuildWhereClause(preparationResult.FieldName, preparationResult.FieldType, group);

                LogInfo($"开始导出分组: {group.DisplayValue} (要素 {group.FeatureCount})");
                LogInfo($"导出文件名: {shapeName}");
                LogInfo($"查询条件: {whereClause}");

                bool success = await ExportGroupAsync(layer, outputFolder, shapeName, whereClause, envSettings);
                if (success)
                {
                    completedCount++;
                }
            }

            return completedCount;
        }
    }
}
