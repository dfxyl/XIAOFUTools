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
using XIAOFUTools.Features.DataManagement.BatchMergeShp.Infrastructure;

namespace XIAOFUTools.Features.DataManagement.BatchMergeShp
{
    /// <summary>
    /// SHP 列表项。
    /// </summary>
    public sealed class ShpMergeItem : PropertyChangedBase
    {
        private bool _isSelected = true;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Name { get; set; } = string.Empty;
        public string GeometryType { get; set; } = "未知";
        public string GeometryKind { get; set; } = "Unknown";
        public string Directory { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// 批量合并 SHP 视图模型。
    /// </summary>
    internal partial class BatchMergeShpViewModel : PropertyChangedBase
    {
        private readonly BatchMergeShpFileStore _fileStore = new();
        private readonly RelayCommand _browseInputFolderCommand;
        private readonly RelayCommand _refreshShpListCommand;
        private readonly RelayCommand _browseOutputPathCommand;
        private readonly RelayCommand _selectAllCommand;
        private readonly RelayCommand _invertSelectionCommand;
        private readonly RelayCommand _clearSelectionCommand;
        private readonly RelayCommand _selectPointCommand;
        private readonly RelayCommand _selectLineCommand;
        private readonly RelayCommand _selectPolygonCommand;
        private readonly RelayCommand _runCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _showHelpCommand;

        private CancellationTokenSource _cancellationTokenSource;
        private string _inputFolderPath = string.Empty;
        private bool _includeSubfolders = true;
        private string _outputPath = string.Empty;
        private bool _addSourceFileField = true;
        private bool _isProcessing;
        private bool _isScanning;
        private int _progress;
        private string _logText = string.Empty;
        private string _selectionSummary = "已选择 0 / 0";

        public BatchMergeShpViewModel()
        {
            ShpItems = new ObservableCollection<ShpMergeItem>();

            _browseInputFolderCommand = new RelayCommand(BrowseInputFolder, () => !IsBusy);
            _refreshShpListCommand = new RelayCommand(async () => await RefreshShpListAsync(), () => CanRefresh);
            _browseOutputPathCommand = new RelayCommand(BrowseOutputPath, () => !IsBusy);
            _selectAllCommand = new RelayCommand(SelectAll, () => CanEditSelection);
            _invertSelectionCommand = new RelayCommand(InvertSelection, () => CanEditSelection);
            _clearSelectionCommand = new RelayCommand(ClearSelection, () => CanEditSelection);
            _selectPointCommand = new RelayCommand(() => SelectByGeometryKind("Point", "点"), () => CanEditSelection);
            _selectLineCommand = new RelayCommand(() => SelectByGeometryKind("Polyline", "线"), () => CanEditSelection);
            _selectPolygonCommand = new RelayCommand(() => SelectByGeometryKind("Polygon", "面"), () => CanEditSelection);
            _runCommand = new RelayCommand(async () => await RunAsync(), () => CanRun);
            _cancelCommand = new RelayCommand(Cancel, () => IsProcessing);
            _showHelpCommand = new RelayCommand(ShowHelp);

            OutputPath = OutputDatasetUtils.NormalizeOutputPath(Path.Combine(PathDialogUtils.GetProjectDefaultGdb(), "MergedSHP"), "MergedSHP");
            AppendInfo("工具已加载，选择输入文件夹后将自动扫描 SHP。");
        }
    }
}
