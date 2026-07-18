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
using XIAOFUTools.Features.General.InternetTileDownload.Infrastructure;
using XIAOFUTools.Features.General.InternetTileDownload.Services;

namespace XIAOFUTools.Features.General.InternetTileDownload
{
    internal sealed partial class InternetTileDownloadViewModel
    {

        private void BrowseOutputFolder()
        {
            var initialDirectory = string.IsNullOrWhiteSpace(OutputFolderPath)
                ? ArcGIS.Desktop.Core.Project.Current?.HomeFolderPath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : OutputFolderPath;
            var selectedFolder = PresentationServices.Files.SelectFolder(
                "选择互联网切片下载输出文件夹",
                initialDirectory);
            if (!string.IsNullOrWhiteSpace(selectedFolder))
            {
                OutputFolderPath = selectedFolder;
            }
        }


        private void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }


        private void ShowHelp()
        {
            PresentationServices.Dialogs.Show(
                InternetTileHelpTextBuilder.Build(),
                "互联网切片下载帮助");
        }


        private void UpdateAreaSummary()
        {
            AreaSummary = SelectedAreaSourceOption?.AreaSourceType switch
            {
                InternetTileAreaSourceType.CurrentView => "使用当前地图可见范围",
                InternetTileAreaSourceType.CustomExtent => _customExtent == null ? "未框选范围" : $"已框选范围: {_customExtent.Width:F2} x {_customExtent.Height:F2}",
                InternetTileAreaSourceType.FeatureLayer => SelectedFeatureLayer == null ? "未选择面图层" : $"使用图层: {SelectedFeatureLayer.Name}",
                _ => "未设置范围"
            };
        }


        private void AppendLog(string message)
        {
            var builder = new StringBuilder(LogContent);
            builder.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(message);
            LogContent = builder.ToString();
        }


        private void UpdateCommands()
        {
            OnPropertyChanged(nameof(CanResolve));
            OnPropertyChanged(nameof(CanDownload));
            (ResolveServiceCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DownloadCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }


        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    }
}
