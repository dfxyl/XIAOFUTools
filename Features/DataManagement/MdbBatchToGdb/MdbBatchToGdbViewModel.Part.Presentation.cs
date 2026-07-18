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

        private void BrowseInputFolder()
        {
            string pickedPath = PathDialogUtils.PickFolder("选择需要遍历的文件夹", InputFolderPath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            InputFolderPath = pickedPath;
            AppendInfo($"已选择输入文件夹: {pickedPath}");

            if (string.IsNullOrWhiteSpace(OutputFolderPath))
            {
                OutputFolderPath = pickedPath;
            }

            _ = RefreshMdbListAsync();
        }

        private void BrowseOutputFolder()
        {
            string pickedPath = PathDialogUtils.PickFolder("选择输出文件夹", OutputFolderPath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            pickedPath = MdbOutputPathPlanner.NormalizeOutputFolder(pickedPath);

            OutputFolderPath = pickedPath;
            AppendInfo($"已选择输出文件夹: {pickedPath}");
        }

        private async Task RefreshMdbListAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(InputFolderPath))
            {
                AppendWarning("请先选择输入文件夹。");
                return;
            }

            if (!_fileStore.DirectoryExists(InputFolderPath))
            {
                AppendError("输入文件夹不存在。");
                ClearItems();
                return;
            }

            SetScanning(true);
            Progress = 0;

            try
            {
                AppendInfo($"开始扫描 MDB: {InputFolderPath}");
                List<string> files = await Task.Run(() => _fileStore.EnumerateMdbFiles(InputFolderPath, IncludeSubfolders));
                List<string> sortedFiles = files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();

                ReplaceItems(sortedFiles);
                Progress = sortedFiles.Count > 0 ? 100 : 0;
                AppendInfo($"扫描完成，共找到 {sortedFiles.Count} 个 MDB。");
            }
            catch (Exception ex)
            {
                AppendError($"扫描失败: {ex.Message}");
            }
            finally
            {
                SetScanning(false);
            }
        }

        private void SelectAll()
        {
            foreach (MdbFileItem item in MdbItems)
            {
                item.IsSelected = true;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private void InvertSelection()
        {
            foreach (MdbFileItem item in MdbItems)
            {
                item.IsSelected = !item.IsSelected;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private void ClearSelection()
        {
            foreach (MdbFileItem item in MdbItems)
            {
                item.IsSelected = false;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private async Task ExecuteConversionBatchV2Async(IReadOnlyList<MdbConversionPlan> plans, bool overwriteExisting, CancellationToken token)
        {
            int total = plans.Count;
            int completed = 0;
            var runnablePlans = new List<MdbConversionPlan>();
            var syncRoot = new object();

            foreach (MdbConversionPlan plan in plans)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    bool outputExists = _fileStore.PathExists(plan.OutputPath);
                    if (outputExists)
                    {
                        if (!overwriteExisting)
                        {
                            UpdateItemStatus(plan.Item, "已跳过(已存在)");
                            AppendWarning($"跳过已存在输出: {plan.OutputPath}");
                            completed++;
                            UpdateProgress(completed, total);
                            continue;
                        }

                        _fileStore.DeletePath(plan.OutputPath);
                        AppendInfo($"已删除旧输出: {plan.OutputPath}");
                    }

                    UpdateItemStatus(plan.Item, "待转换");
                    runnablePlans.Add(plan);
                }
                catch (Exception ex)
                {
                    UpdateItemStatus(plan.Item, "失败");
                    AppendError($"{plan.Item.Name} 转换失败: {ex.Message}");
                    completed++;
                    UpdateProgress(completed, total);
                }
            }

            if (runnablePlans.Count == 0)
            {
                return;
            }

            await _converter.ConvertBatchAsync(
                runnablePlans,
                evt =>
                {
                    MdbConversionPlan plan = runnablePlans[evt.Index];
                    switch (evt.Kind)
                    {
                        case BatchConversionEventKind.Started:
                            UpdateItemStatus(plan.Item, "转换中");
                            AppendInfo($"[{evt.Index + 1}/{runnablePlans.Count}] {plan.Item.Name} -> {plan.OutputPath}");
                            break;

                        case BatchConversionEventKind.Completed:
                            UpdateItemStatus(plan.Item, "完成");
                            AppendInfo($"完成: {plan.OutputPath}");
                            lock (syncRoot)
                            {
                                completed++;
                                UpdateProgress(completed, total);
                            }
                            break;

                        case BatchConversionEventKind.Failed:
                            UpdateItemStatus(plan.Item, "失败");
                            AppendError($"{plan.Item.Name} 转换失败: {evt.Message}");
                            lock (syncRoot)
                            {
                                completed++;
                                UpdateProgress(completed, total);
                            }
                            break;
                    }
                },
                message => AppendInfo($"  {message}"),
                token);
        }


        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(MdbFileItem.IsSelected), StringComparison.Ordinal))
            {
                return;
            }

            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void UpdateSelectionSummary()
        {
            int total = MdbItems.Count;
            int selected = MdbItems.Count(item => item.IsSelected);
            SelectionSummary = $"已选择 {selected} / {total}";
        }

        private bool HasSelection()
        {
            return MdbItems.Any(item => item.IsSelected);
        }

        private void Cancel()
        {
            if (!IsProcessing)
            {
                return;
            }

            _cancellationTokenSource?.Cancel();
            AppendWarning("正在请求停止...");
        }

        private void ShowHelp()
        {
            const string helpText =
                "MDB批量格式转换工具说明\n\n" +
                "1. 选择输入文件夹后，工具会自动扫描并列出所有 MDB。\n" +
                "2. 可勾选“遍历子文件夹”控制是否递归扫描。\n" +
                "3. 列表支持按需勾选需要转换的 MDB。\n" +
                "4. 输出支持两种模式：\n" +
                "   - 输出到源路径：每个 MDB 在原目录生成同名的所选格式文件。\n" +
                "   - 指定输出文件夹：统一输出到同一目录。\n" +
                "5. 转换直接调用 ArcGIS Pro 3.7 内置 Convert Personal Geodatabase 工具：\n" +
                "   - 文件地理数据库 (.gdb)。\n" +
                "   - 移动地理数据库 (.geodatabase)。\n" +
                "   - XML Workspace Document (.xml)。\n\n" +
                "注意：转换 MDB 需系统安装 Access Database Engine。";

            PresentationServices.Dialogs.Show(helpText, "MDB批量格式转换工具说明", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UpdateItemStatus(MdbFileItem item, string status)
        {
            ExecuteOnUiThread(() => item.Status = status);
        }

        private void UpdateProgress(int completed, int total)
        {
            int value = total <= 0 ? 0 : (int)Math.Round((double)completed / total * 100);
            ExecuteOnUiThread(() => Progress = value);
        }

        private void NotifyStatePropertiesChanged()
        {
            NotifyPropertyChanged(() => CanRun);
            NotifyPropertyChanged(() => CanRefresh);
        }

        private void AppendLog(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            ExecuteOnUiThread(() => LogText += line);
        }
    }
}
