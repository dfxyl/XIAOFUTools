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
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.MdbBatchToGdb
{
    internal partial class MdbBatchToGdbViewModel
    {

        private async Task RunAsync()
        {
            if (!CanRun)
            {
                return;
            }

            List<MdbFileItem> selectedItems = MdbItems
                .Where(item => item.IsSelected)
                .ToList();
            if (selectedItems.Count == 0)
            {
                AppendWarning("没有选中任何 MDB。\n");
                return;
            }

            if (!SaveToSourcePath)
            {
                try
                {
                    string outputFolder = MdbOutputPathPlanner.NormalizeOutputFolder(OutputFolderPath);
                    _fileStore.EnsureDirectory(outputFolder);
                }
                catch (Exception ex)
                {
                    AppendError($"无法创建输出目录: {ex.Message}");
                    return;
                }
            }

            var plans = MdbConversionPlanner.BuildPlans(
                selectedItems,
                SaveToSourcePath,
                InputFolderPath,
                OutputFolderPath,
                SelectedOutputFormat,
                AppendWarning);
            if (plans.Count == 0)
            {
                AppendWarning("没有可执行的转换任务。");
                return;
            }

            var existingOutputs = plans
                .Where(plan => _fileStore.PathExists(plan.OutputPath))
                .ToList();

            bool overwriteExisting = false;
            if (existingOutputs.Count > 0)
            {
                var result = PresentationServices.Dialogs.Show(
                    $"检测到 {existingOutputs.Count} 个输出已存在，是否覆盖？\n选择“否”将跳过这些任务。",
                    "确认覆盖",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                overwriteExisting = result == MessageBoxResult.Yes;
                if (!overwriteExisting)
                {
                    AppendWarning("已存在输出将被跳过。");
                }
            }

            IsProcessing = true;
            Progress = 0;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                await ExecuteConversionBatchV2Async(plans, overwriteExisting, _cancellationTokenSource.Token);

                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    AppendWarning("操作已取消。");
                }
                else
                {
                    AppendInfo("批量转换已完成。");
                    PresentationServices.Dialogs.Show("MDB 批量格式转换完成。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (OperationCanceledException)
            {
                AppendWarning("操作已取消。");
            }
            catch (Exception ex)
            {
                AppendError($"批量转换失败: {ex.Message}");
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
            _browseOutputFolderCommand.RaiseCanExecuteChanged();
            _refreshMdbListCommand.RaiseCanExecuteChanged();
            _selectAllCommand.RaiseCanExecuteChanged();
            _invertSelectionCommand.RaiseCanExecuteChanged();
            _clearSelectionCommand.RaiseCanExecuteChanged();
            _runCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
        }
    }
}
