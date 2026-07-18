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
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.BatchMergeShp
{
    internal partial class BatchMergeShpViewModel
    {

        private void BrowseInputFolder()
        {
            var pickedPath = PresentationServices.Files.SelectFolder("选择需要遍历的文件夹", InputFolderPath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            InputFolderPath = pickedPath;
            AppendInfo($"已选择输入文件夹: {pickedPath}");

            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                OutputPath = Path.Combine(pickedPath, "MergedSHP.shp");
            }

            _ = RefreshShpListAsync();
        }

        private async Task RefreshShpListAsync()
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
                AppendInfo($"开始扫描: {InputFolderPath}");
                var files = await Task.Run(() =>
                {
                    var list = _fileStore.EnumerateShapefiles(InputFolderPath, IncludeSubfolders);
                    return list.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
                });

                ReplaceItems(files);
                Progress = files.Count > 0 ? 100 : 0;
                AppendInfo($"扫描完成，共找到 {files.Count} 个 SHP。");
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

        private void BrowseOutputPath()
        {
            string initialLocation = null;
            if (!string.IsNullOrWhiteSpace(OutputPath))
            {
                if (OutputPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    initialLocation = OutputPath;
                }
                else
                {
                    initialLocation = Path.GetDirectoryName(OutputPath);
                }
            }

            if (string.IsNullOrWhiteSpace(initialLocation))
            {
                initialLocation = string.IsNullOrWhiteSpace(InputFolderPath)
                    ? PathDialogUtils.GetProjectDefaultGdb()
                    : InputFolderPath;
            }

            var selectedPath = PathDialogUtils.PickSaveFeatureClassPath("选择合并输出位置", initialLocation);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            OutputPath = OutputDatasetUtils.NormalizeOutputPath(selectedPath, "MergedSHP");
            AppendInfo($"输出位置: {OutputPath}");
        }

        private void SelectAll()
        {
            foreach (var item in ShpItems)
            {
                item.IsSelected = true;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private void InvertSelection()
        {
            foreach (var item in ShpItems)
            {
                item.IsSelected = !item.IsSelected;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private void ClearSelection()
        {
            foreach (var item in ShpItems)
            {
                item.IsSelected = false;
            }

            UpdateSelectionSummary();
            RaiseCommandCanExecuteChanged();
        }

        private void SelectByGeometryKind(string geometryKind, string geometryName)
        {
            var count = 0;
            foreach (var item in ShpItems)
            {
                var isMatch = string.Equals(item.GeometryKind, geometryKind, StringComparison.OrdinalIgnoreCase);
                item.IsSelected = isMatch;
                if (isMatch)
                {
                    count++;
                }
            }

            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();

            AppendInfo($"快捷勾选“{geometryName}”完成，共 {count} 个。");
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

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(ShpMergeItem.IsSelected), StringComparison.Ordinal))
            {
                return;
            }

            UpdateSelectionSummary();
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void UpdateSelectionSummary()
        {
            var total = ShpItems.Count;
            var selected = ShpItems.Count(item => item.IsSelected);
            SelectionSummary = $"已选择 {selected} / {total}";
        }

        private bool HasValidGeometrySelection()
        {
            var selected = ShpItems.Where(item => item.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return false;
            }

            if (selected.Any(item => string.Equals(item.GeometryKind, "Unknown", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return selected
                .Select(item => item.GeometryKind)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == 1;
        }

        private void NotifyStatePropertiesChanged()
        {
            NotifyPropertyChanged(() => CanRun);
            NotifyPropertyChanged(() => CanRefresh);
        }

        private void ShowHelp()
        {
            const string helpText =
                "批量合并 SHP 工具说明\n\n" +
                "1. 选择输入文件夹后会自动扫描并列出所有 SHP。\n" +
                "2. 可勾选“读取子文件夹”决定是否递归遍历。\n" +
                "3. 列表默认全选，可手动全选/反选/清空。\n" +
                "4. 合并使用 Merge 工具，自动进行字段并集。\n" +
                "5. 勾选“创建源文件名字段”时，会基于 MERGE_SRC 写入源文件名。\n" +
                "6. 输出位置支持 GDB 要素类或 Shapefile。\n\n" +
                "注意：输入 SHP 的几何类型需要一致。";

            PresentationServices.Dialogs.Show(helpText, "批量合并 SHP 工具说明", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AppendLog(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            PresentationServices.UiThread.InvokeOrRun(() => LogText += line);
        }
    }
}
