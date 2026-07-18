using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Conversion.DocumentBatchReplace
{
    internal partial class DocumentBatchReplaceDockPaneViewModel
    {
        private void OnInputFilesCollectionChanged(
            object sender,
            NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(FileCountText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void OnReplaceRulesCollectionChanged(
            object sender,
            NotifyCollectionChangedEventArgs e)
        {
            CommandManager.InvalidateRequerySuggested();
        }

        private void RemoveSelectedFile()
        {
            if (string.IsNullOrWhiteSpace(SelectedInputFile))
            {
                return;
            }

            var selected = SelectedInputFile;
            InputFiles.Remove(selected);
            SelectedInputFile = InputFiles.FirstOrDefault();
            LogMessage($"已移除文件: {Path.GetFileName(selected)}");
        }

        private void SelectOutputFolder()
        {
            var folder = PathDialogUtils.PickFolder(
                "选择副本输出文件夹",
                OutputFolder);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                OutputFolder = folder;
                LogMessage($"已选择输出文件夹: {folder}");
            }
        }

        private void ShowHelp()
        {
            const string helpMessage = "文档批量替换工具\n\n" +
                                       "功能说明:\n" +
                                       "1. 支持 .docx / .doc / .docm 多文档批量替换\n" +
                                       "2. 支持多条替换规则串行执行\n" +
                                       "3. 支持区分大小写、全字匹配、全/半角和通配符\n" +
                                       "4. 支持另存副本与覆盖原文件两种保存方式\n\n" +
                                       "使用步骤:\n" +
                                       "1. 添加文件或文件夹（可拖拽）\n" +
                                       "2. 配置替换规则和匹配选项\n" +
                                       "3. 选择保存方式并设置输出目录\n" +
                                       "4. 点击“开始替换”执行\n\n" +
                                       "注意:\n" +
                                       "- 运行前请关闭正在编辑的目标文档\n" +
                                       "- 建议先使用“另存为副本”验证替换结果\n" +
                                       "- 该功能依赖本机安装 Microsoft Word";

            PresentationServices.Dialogs.Show(helpMessage, "帮助");
        }

        private void LogMessage(string message)
        {
            InvokeOnUi(() => LogText += $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }

        private void LogError(string message)
        {
            InvokeOnUi(() =>
                LogText += $"[{DateTime.Now:HH:mm:ss}] [错误] {message}\n");
        }

        private static void InvokeOnUi(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            PresentationServices.UiThread.InvokeOrRun(action);
        }

        private void OnPropertyChanged(
            [CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
