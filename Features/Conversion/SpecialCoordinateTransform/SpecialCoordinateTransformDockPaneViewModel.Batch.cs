using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Dialogs;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.SpecialCoordinateTransform
{
    internal sealed partial class SpecialCoordinateTransformDockPaneViewModel
    {
        private void BrowseBatchInputFolder()
        {
            string pickedPath = PathDialogUtils.PickFolder("选择包含 SHP 的文件夹", BatchInputFolderPath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            BatchInputFolderPath = pickedPath;
            AddLog($"已选择批量输入文件夹: {pickedPath}");

            if (string.IsNullOrWhiteSpace(BatchOutputFolderPath))
            {
                BatchOutputFolderPath = pickedPath;
            }

            _ = RefreshBatchItemsAsync();
        }

        private void BrowseBatchOutputFolder()
        {
            string pickedPath = PathDialogUtils.PickFolder("选择批量输出文件夹", BatchOutputFolderPath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            BatchOutputFolderPath = pickedPath;
            AddLog($"已选择批量输出文件夹: {pickedPath}");
        }

        private async Task RefreshBatchItemsAsync()
        {
            await RefreshFileItemsAsync(
                BatchInputFolderPath,
                BatchIncludeSubfolders,
                BatchItems,
                "*.shp",
                false,
                UpdateBatchSelectionSummary,
                OnBatchItemPropertyChanged,
                "SHP");
        }

        private void SelectAllBatchItems()
        {
            SetItemSelection(BatchItems, true);
            UpdateBatchSelectionSummary();
        }

        private void InvertBatchItems()
        {
            foreach (BatchTransformItem item in BatchItems)
            {
                item.IsSelected = !item.IsSelected;
            }

            UpdateBatchSelectionSummary();
        }

        private void ClearBatchItemsSelection()
        {
            SetItemSelection(BatchItems, false);
            UpdateBatchSelectionSummary();
        }

        private void BrowseGdbInputFolder()
        {
            string pickedPath = PathDialogUtils.PickFolder("选择包含 GDB 的文件夹", GdbInputFolderPath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            GdbInputFolderPath = pickedPath;
            AddLog($"已选择 GDB 输入文件夹: {pickedPath}");

            if (string.IsNullOrWhiteSpace(GdbOutputFolderPath))
            {
                GdbOutputFolderPath = pickedPath;
            }

            _ = RefreshGdbItemsAsync();
        }

        private void BrowseGdbOutputFolder()
        {
            string pickedPath = PathDialogUtils.PickFolder("选择 GDB 输出文件夹", GdbOutputFolderPath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            GdbOutputFolderPath = pickedPath;
            AddLog($"已选择 GDB 输出文件夹: {pickedPath}");
        }

        private async Task RefreshGdbItemsAsync()
        {
            await RefreshFileItemsAsync(
                GdbInputFolderPath,
                GdbIncludeSubfolders,
                GdbItems,
                "*.gdb",
                true,
                UpdateGdbSelectionSummary,
                OnGdbItemPropertyChanged,
                "GDB");
        }

        private void SelectAllGdbItems()
        {
            SetItemSelection(GdbItems, true);
            UpdateGdbSelectionSummary();
        }

        private void InvertGdbItems()
        {
            foreach (BatchTransformItem item in GdbItems)
            {
                item.IsSelected = !item.IsSelected;
            }

            UpdateGdbSelectionSummary();
        }

        private void ClearGdbItemsSelection()
        {
            SetItemSelection(GdbItems, false);
            UpdateGdbSelectionSummary();
        }

        private async Task RefreshFileItemsAsync(
            string inputFolderPath,
            bool includeSubfolders,
            ObservableCollection<BatchTransformItem> targetItems,
            string searchPattern,
            bool searchDirectories,
            Action updateSummaryAction,
            PropertyChangedEventHandler itemPropertyChangedHandler,
            string displayType)
        {
            if (IsBusy)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(inputFolderPath))
            {
                AddLog($"请先选择{displayType}输入文件夹。");
                return;
            }

            if (!_fileStore.DirectoryExists(inputFolderPath))
            {
                AddLog($"输入文件夹不存在: {inputFolderPath}");
                ReplaceItems(targetItems, Enumerable.Empty<string>(), inputFolderPath, itemPropertyChangedHandler, updateSummaryAction);
                return;
            }

            IsScanning = true;
            SetStatus($"正在扫描 {displayType}...");

            try
            {
                var paths = await Task.Run(() => _fileStore.EnumeratePaths(
                    inputFolderPath,
                    includeSubfolders,
                    searchPattern,
                    searchDirectories));
                ReplaceItems(targetItems, paths, inputFolderPath, itemPropertyChangedHandler, updateSummaryAction);
                AddLog($"扫描完成，找到 {paths.Count} 个 {displayType}。");
            }
            catch (Exception ex)
            {
                AddLog($"扫描 {displayType} 失败: {ex.Message}");
            }
            finally
            {
                IsScanning = false;
                SetStatus("准备就绪");
            }
        }

        private void ReplaceItems(
            ObservableCollection<BatchTransformItem> targetItems,
            IEnumerable<string> paths,
            string rootFolder,
            PropertyChangedEventHandler itemPropertyChangedHandler,
            Action updateSummaryAction)
        {
            foreach (BatchTransformItem item in targetItems)
            {
                item.PropertyChanged -= itemPropertyChangedHandler;
            }

            targetItems.Clear();
            foreach (string path in paths)
            {
                var item = new BatchTransformItem
                {
                    IsSelected = true,
                    FullPath = path,
                    Name = Path.GetFileNameWithoutExtension(path),
                    RelativePath = BuildRelativePath(rootFolder, path),
                    Status = string.Empty
                };

                item.PropertyChanged += itemPropertyChangedHandler;
                targetItems.Add(item);
            }

            updateSummaryAction();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private static string BuildRelativePath(string rootFolder, string path)
        {
            try
            {
                return string.IsNullOrWhiteSpace(rootFolder) ? Path.GetFileName(path) : Path.GetRelativePath(rootFolder, path);
            }
            catch
            {
                return Path.GetFileName(path);
            }
        }

        private void OnBatchItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(BatchTransformItem.IsSelected), StringComparison.Ordinal))
            {
                UpdateBatchSelectionSummary();
            }
        }

        private void OnGdbItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(BatchTransformItem.IsSelected), StringComparison.Ordinal))
            {
                UpdateGdbSelectionSummary();
            }
        }

        private void UpdateBatchSelectionSummary()
        {
            BatchSelectionSummary = $"已选择 {BatchItems.Count(item => item.IsSelected)} / {BatchItems.Count}";
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void UpdateGdbSelectionSummary()
        {
            GdbSelectionSummary = $"已选择 {GdbItems.Count(item => item.IsSelected)} / {GdbItems.Count}";
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private static void SetItemSelection(IEnumerable<BatchTransformItem> items, bool isSelected)
        {
            foreach (BatchTransformItem item in items)
            {
                item.IsSelected = isSelected;
            }
        }

        private void CancelTransform()
        {
            _cancellationTokenSource?.Cancel();
            AddLog("正在请求停止...");
        }

        private void ShowHelp()
        {
            const string helpMessage =
                "特殊坐标格式转换工具说明\n\n" +
                "1. 单层\n" +
                "   - 从当前地图选择一个要素图层。\n" +
                "   - 输出支持 GDB 要素类或 Shapefile。\n\n" +
                "2. 批量\n" +
                "   - 扫描文件夹中的 SHP。\n" +
                "   - 支持勾选、全选、反选、清空选择。\n" +
                "   - 支持输出到源文件夹或指定输出文件夹。\n\n" +
                "3. gdb\n" +
                "   - 扫描文件夹中的 GDB。\n" +
                "   - 每个 GDB 会输出为新的转换结果 GDB，并尽量保留要素数据集结构。\n\n" +
                "4. 转换类型\n" +
                "   - 支持 WGS84 / GCJ02 / BD09 之间互转。\n" +
                "   - 算法主要适用于中国境内坐标。\n\n" +
                "5. 其他\n" +
                "   - 已存在的同名输出会被覆盖。";

            PresentationServices.Dialogs.Show(helpMessage, "特殊坐标格式转换 - 帮助");
        }
    }
}
