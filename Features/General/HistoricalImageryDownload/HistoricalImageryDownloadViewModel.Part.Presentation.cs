#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    internal sealed partial class HistoricalImageryDownloadViewModel
    {

        private void BrowseOutputFolder()
        {
            var initialDirectory = string.IsNullOrWhiteSpace(OutputFolderPath)
                ? ArcGIS.Desktop.Core.Project.Current?.HomeFolderPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : OutputFolderPath;
            var selectedFolder = PresentationServices.Files.SelectFolder(
                "选择历史影像输出文件夹",
                initialDirectory);
            if (!string.IsNullOrWhiteSpace(selectedFolder))
            {
                OutputFolderPath = selectedFolder;
            }
        }


        private void OpenVersionSelection()
        {
            if (Versions.Count == 0)
            {
                return;
            }

            _versionSelectionDialogService.Show(Versions);
            UpdateSelectedVersionSummary();
            UpdateCommands();
        }


        private void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }


        private void ShowHelp()
        {
            PresentationServices.Dialogs.Show(
                HistoricalImageryHelpTextBuilder.Build(),
                "历史影像下载帮助");
        }


        private void UpdateAreaSummary()
        {
            AreaSummary = SelectedAreaSourceOption?.AreaSourceType switch
            {
                HistoricalAreaSourceType.CurrentView => "使用当前地图可见范围",
                HistoricalAreaSourceType.CustomExtent => _customExtent == null ? "未框选范围" : $"已框选范围: {_customExtent.Width:F2} x {_customExtent.Height:F2}",
                HistoricalAreaSourceType.FeatureLayer => SelectedFeatureLayer == null ? "未选择面图层" : $"使用图层: {SelectedFeatureLayer.Name}",
                _ => "未设置范围"
            };
        }


        private void AppendLog(string message)
        {
            var builder = new StringBuilder(LogContent);
            builder.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(message);
            LogContent = builder.ToString();
        }


        private void ResetVersions()
        {
            foreach (var selection in Versions)
            {
                selection.PropertyChanged -= OnVersionSelectionChanged;
            }

            Versions.Clear();
            SelectedVersionPreview = null;
            _hasVersionResults = false;
            UpdateSelectedVersionSummary();
            UpdateCommands();
            OnPropertyChanged(nameof(CanConfigureVersions));
        }


        private void OnVersionSelectionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(HistoricalVersionSelectionItem.IsSelected))
            {
                return;
            }

            UpdateSelectedVersionSummary();
            UpdateCommands();
        }


        private void UpdateSelectedVersionSummary()
        {
            var selectedCount = Versions.Count(item => item.IsSelected);
            SelectedVersionSummary = HistoricalVersionSelectionSummaryBuilder.Build(Versions.Count, selectedCount);
        }


        private void UpdateCommands()
        {
            OnPropertyChanged(nameof(CanQuery));
            OnPropertyChanged(nameof(CanDownload));
            (QueryVersionsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DownloadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenVersionSelectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }


        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    }
}
