using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.DocumentBatchReplace
{
    internal partial class DocumentBatchReplaceDockPaneViewModel
    {
        private async Task StartReplaceAsync()
        {
            if (InputFiles.Count == 0)
            {
                PresentationServices.Dialogs.Show("请先添加需要处理的文档文件", "提示");
                return;
            }

            var activeRules = ReplaceRules
                .Where(rule => !string.IsNullOrWhiteSpace(rule.FindText))
                .Select(rule => new DocumentReplacementRule(
                    rule.FindText,
                    rule.ReplaceText ?? string.Empty))
                .ToArray();
            if (activeRules.Length == 0)
            {
                PresentationServices.Dialogs.Show("请至少填写一条查找内容", "提示");
                return;
            }

            if (SaveAsCopy && string.IsNullOrWhiteSpace(OutputFolder))
            {
                PresentationServices.Dialogs.Show("请选择副本输出文件夹", "提示");
                return;
            }

            IsProcessing = true;
            Progress = 0;
            IsProgressIndeterminate = false;
            StatusText = "正在替换...";
            LogText = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;
            var request = new DocumentBatchReplacementRequest(
                InputFiles.ToArray(),
                activeRules,
                SaveAsCopy,
                OutputFolder,
                MatchCase,
                MatchWholeWord,
                MatchByte,
                UseWildcards);
            var progress = new Progress<DocumentBatchReplacementProgress>(
                HandleReplacementProgress);

            try
            {
                await _replacementService.ReplaceAsync(
                    request,
                    progress,
                    cancellationToken);
                StatusText = "替换完成";
                LogMessage("所有文档替换完成");
                PresentationServices.Dialogs.Show("文档批量替换完成", "完成");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                StatusText = "替换已取消";
                LogMessage("替换已取消");
            }
            catch (Exception exception)
            {
                StatusText = "替换失败";
                LogError($"替换失败: {exception.Message}");
                PresentationServices.Dialogs.Show(
                    $"替换失败: {exception.Message}",
                    "错误");
            }
            finally
            {
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private void HandleReplacementProgress(
            DocumentBatchReplacementProgress progress)
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

            Progress = progress.Total == 0
                ? 0
                : (double)progress.Processed / progress.Total * 100;
            StatusText = $"已处理 {progress.Processed}/{progress.Total} 个文件";
        }
    }
}
