using System;
using System.ComponentModel;
using System.Threading;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.ExcelToPdf
{
    internal partial class ExcelToPdfDockPaneViewModel
    {

        private void SelectInputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择包含Excel文件的输入文件夹", InputFolder);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                InputFolder = picked;
                LogMessage($"已选择输入文件夹: {InputFolder}");
            }
        }

        private void SelectOutputFolder()
        {
            var picked = PathDialogUtils.PickFolder("选择输出文件夹", OutputFolder);
            if (!string.IsNullOrWhiteSpace(picked))
            {
                OutputFolder = picked;
                LogMessage($"已选择输出文件夹: {OutputFolder}");
            }
        }

        private async System.Threading.Tasks.Task StartConversionAsync()
        {
            if (!_conversionService.DirectoryExists(InputFolder))
            {
                PresentationServices.Dialogs.Show("请选择有效的输入文件夹", "错误");
                return;
            }

            if (!SaveToSourcePath && !_conversionService.DirectoryExists(OutputFolder))
            {
                PresentationServices.Dialogs.Show("请选择有效的输出文件夹或勾选保存在源路径", "错误");
                return;
            }

            IsProcessing = true;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            Progress = 0;
            IsProgressIndeterminate = false;
            LogText = "";
            StatusText = "开始转换...";
            var cancellationToken = _cancellationTokenSource.Token;
            var progress = new Progress<ExcelPdfConversionProgress>(HandleConversionProgress);

            try
            {
                await _conversionService.ConvertAsync(
                    new ExcelPdfConversionRequest(
                        InputFolder,
                        OutputFolder,
                        SaveToSourcePath,
                        KeepOriginalStructure,
                        SeparateWorksheets,
                        TraverseSubfolders,
                        SelectedPageOrientation,
                        SelectedPageSize,
                        SelectedPrintLayout),
                    progress,
                    cancellationToken);
                
                StatusText = "转换完成";
                LogMessage("所有Excel转PDF完成");
                PresentationServices.Dialogs.Show("Excel转PDF完成!", "完成");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                StatusText = "转换已取消";
                LogMessage("转换已被用户取消");
            }
            catch (Exception ex)
            {
                StatusText = "转换失败";
                LogError($"转换失败: {ex.Message}");
                PresentationServices.Dialogs.Show($"转换失败: {ex.Message}", "错误");
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void StopConversion()
        {
            _cancellationTokenSource?.Cancel();
            LogMessage("正在停止转换...");
        }

        private void HandleConversionProgress(ExcelPdfConversionProgress progress)
        {
            if (!string.IsNullOrWhiteSpace(progress.Message))
            {
                if (progress.IsError)
                {
                    LogError(progress.Message);
                }
                else
                {
                    LogMessage(progress.Message);
                }
            }

            Progress = progress.Total == 0 ? 0 : (double)progress.Processed / progress.Total * 100;
            if (progress.Total > 0)
            {
                StatusText = $"已处理 {progress.Processed}/{progress.Total} 个文件";
            }
        }

        private void ShowHelp()
        {
            string helpMessage = "Excel批量转PDF工具\n\n" +
                "功能说明:\n" +
                "1. 批量将Excel文件转换为PDF格式\n" +
                "2. 支持xlsx、xls、xlsm格式\n" +
                "3. 支持工作表分开或整个工作簿导出\n" +
                "4. 可自定义页面方向、大小和打印布局\n\n" +
                "使用步骤:\n" +
                "1. 选择包含Excel文件的输入文件夹\n" +
                "2. 选择输出文件夹（或勾选保存在源路径）\n" +
                "3. 配置转换选项:\n" +
                "   - 是否按原结构输出\n" +
                "   - 是否单独工作表分开\n" +
                "   - 是否遍历子文件夹\n" +
                "4. 设置页面选项（页面方向、大小、打印布局）\n" +
                "5. 点击开始按钮进行转换\n\n" +
                "注意事项:\n" +
                "- 需要安装Microsoft Excel\n" +
                "- 处理大量文件可能需要较长时间\n" +
                "- 处理过程中可以点击停止按钮中止操作";

            PresentationServices.Dialogs.Show(helpMessage, "帮助");
        }

        private void LogMessage(string message)
        {
            try
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            }
            catch
            {
            }
        }

        private void LogError(string error)
        {
            try
            {
                LogText += $"[{DateTime.Now:HH:mm:ss}] [错误] {error}\n";
            }
            catch
            {
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
