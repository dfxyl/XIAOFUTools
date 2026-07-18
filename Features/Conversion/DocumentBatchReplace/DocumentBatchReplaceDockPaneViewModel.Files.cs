using System;
using System.Collections.Generic;
using System.Linq;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.DocumentBatchReplace
{
    internal partial class DocumentBatchReplaceDockPaneViewModel
    {
        public void AddInputPaths(IEnumerable<string> paths)
        {
            var resolution = _inputPathResolver.Resolve(paths, TraverseSubfolders);
            var addedCount = 0;
            var skippedCount = resolution.SkippedCount;
            foreach (var file in resolution.Files)
            {
                if (InputFiles.Any(path => string.Equals(
                        path,
                        file,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    skippedCount++;
                    continue;
                }

                InputFiles.Add(file);
                addedCount++;
            }

            if (addedCount > 0)
            {
                LogMessage($"已添加 {addedCount} 个文档文件");
            }

            if (skippedCount > 0)
            {
                LogMessage($"已忽略 {skippedCount} 个重复或不支持的路径");
            }
        }

        private void AddFiles()
        {
            var selectedFiles = PresentationServices.Files.OpenFiles(
                "Word文档 (*.docx;*.doc;*.docm)|*.docx;*.doc;*.docm|所有文件 (*.*)|*.*",
                title: "选择需要替换的文档");
            if (selectedFiles.Count > 0)
            {
                AddInputPaths(selectedFiles);
            }
        }

        private void AddFolder()
        {
            var folder = PathDialogUtils.PickFolder(
                "选择包含文档的文件夹",
                string.Empty);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                AddInputPaths([folder]);
            }
        }

        private void ClearFiles()
        {
            if (InputFiles.Count == 0)
            {
                return;
            }

            InputFiles.Clear();
            SelectedInputFile = null;
            LogMessage("已清空所有待处理文件");
        }

        private void AddRule()
        {
            ReplaceRules.Add(new ReplaceRuleItem());
        }

        private bool CanRemoveRule(object parameter)
        {
            return parameter is ReplaceRuleItem && ReplaceRules.Count > 1;
        }

        private void RemoveRule(object parameter)
        {
            if (parameter is ReplaceRuleItem rule && ReplaceRules.Count > 1)
            {
                ReplaceRules.Remove(rule);
            }
        }

        private void ClearRules()
        {
            ReplaceRules.Clear();
            ReplaceRules.Add(new ReplaceRuleItem());
        }

        private void StopReplace()
        {
            _cancellationTokenSource?.Cancel();
            LogMessage("正在停止替换...");
        }
    }
}
