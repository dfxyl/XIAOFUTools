using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BatchMergeShp
{
    internal partial class BatchMergeShpViewModel
    {

        private async Task RunAsync()
        {
            if (!CanRun)
            {
                return;
            }

            var selectedItems = ShpItems.Where(item => item.IsSelected).ToList();
            if (selectedItems.Count == 0)
            {
                AppendWarning("没有选中任何 SHP。");
                return;
            }

            var unknownGeometryItems = selectedItems
                .Where(item => string.Equals(item.GeometryKind, "Unknown", StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Name)
                .ToList();
            if (unknownGeometryItems.Count > 0)
            {
                var sample = string.Join("、", unknownGeometryItems.Take(10));
                PresentationServices.Dialogs.Show(
                    $"存在无法识别几何类型的 SHP，不能继续合并。\n{sample}",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                AppendError($"存在未知几何类型: {sample}");
                return;
            }

            var geometryGroups = selectedItems
                .GroupBy(item => item.GeometryKind, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (geometryGroups.Count > 1)
            {
                var detail = string.Join("\n", geometryGroups.Select(group =>
                    $"{GetGeometryDisplayName(group.Key)}: {group.Count()} 个"));
                PresentationServices.Dialogs.Show(
                    $"所选 SHP 几何类型不一致，无法合并。\n{detail}",
                    "提示",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                AppendError($"几何类型不一致，已阻止运行。{detail.Replace('\n', ' ')}");
                return;
            }

            IsProcessing = true;
            Progress = 0;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var normalizedOutput = OutputDatasetUtils.NormalizeOutputPath(OutputPath, "MergedSHP");
                OutputPath = normalizedOutput;

                var outputInfo = OutputDatasetUtils.ParseOutputPath(normalizedOutput, "MergedSHP");
                if (!outputInfo.IsGdb && !_fileStore.DirectoryExists(outputInfo.OutPathWorkspace))
                {
                    _fileStore.EnsureDirectory(outputInfo.OutPathWorkspace);
                }

                var exists = await QueuedTask.Run(() => OutputDatasetUtils.Exists(outputInfo));
                if (exists)
                {
                    var overwrite = PresentationServices.Dialogs.Show(
                        $"输出已存在，是否覆盖？\n{outputInfo.CatalogPath}",
                        "确认覆盖",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (overwrite != MessageBoxResult.Yes)
                    {
                        AppendWarning("用户取消了输出覆盖，操作已停止。");
                        return;
                    }

                    AppendInfo("正在删除已存在输出...");
                    var deleteParams = Geoprocessing.MakeValueArray(outputInfo.CatalogPath);
                    var deleteResult = await Geoprocessing.ExecuteToolAsync("Delete_management", deleteParams, null, _cancellationTokenSource.Token);
                    if (deleteResult.IsFailed)
                    {
                        throw new InvalidOperationException("删除旧输出失败: " + GetGpMessageText(deleteResult));
                    }
                }

                Progress = 20;
                AppendInfo($"开始合并，输入数量: {selectedItems.Count}");

                var mergeInputs = string.Join(";", selectedItems.Select(item => item.FullPath));
                var addSourceInfo = AddSourceFileField ? "ADD_SOURCE_INFO" : "NO_SOURCE_INFO";
                var mergeParams = Geoprocessing.MakeValueArray(mergeInputs, outputInfo.CatalogPath, null, addSourceInfo);
                var environments = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);

                var mergeResult = await Geoprocessing.ExecuteToolAsync(
                    "Merge_management",
                    mergeParams,
                    environments,
                    _cancellationTokenSource.Token);

                if (mergeResult.IsFailed)
                {
                    throw new InvalidOperationException(GetGpMessageText(mergeResult));
                }

                Progress = 80;

                if (AddSourceFileField)
                {
                    await CreateSourceFileNameFieldAsync(outputInfo.CatalogPath, _cancellationTokenSource.Token);
                }

                Progress = 100;
                AppendInfo($"合并完成: {outputInfo.CatalogPath}");
            }
            catch (OperationCanceledException)
            {
                AppendWarning("操作已取消。");
            }
            catch (Exception ex)
            {
                AppendError($"合并失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void RaiseCommandCanExecuteChanged()
        {
            _browseInputFolderCommand.RaiseCanExecuteChanged();
            _refreshShpListCommand.RaiseCanExecuteChanged();
            _browseOutputPathCommand.RaiseCanExecuteChanged();
            _selectAllCommand.RaiseCanExecuteChanged();
            _invertSelectionCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
            _selectPointCommand.RaiseCanExecuteChanged();
            _selectLineCommand.RaiseCanExecuteChanged();
            _selectPolygonCommand.RaiseCanExecuteChanged();
            _runCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
        }
    }
}
