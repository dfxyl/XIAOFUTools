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
    internal partial class MdbBatchToGdbViewModel : PropertyChangedBase
    {
        private readonly ArcGisProMdbToGdbConverter _converter = new ArcGisProMdbToGdbConverter();
        private readonly Infrastructure.MdbBatchToGdbFileStore _fileStore = new Infrastructure.MdbBatchToGdbFileStore();

        private readonly RelayCommand _browseInputFolderCommand;
        private readonly RelayCommand _browseOutputFolderCommand;
        private readonly RelayCommand _refreshMdbListCommand;
        private readonly RelayCommand _selectAllCommand;
        private readonly RelayCommand _invertSelectionCommand;
        private readonly RelayCommand _clearSelectionCommand;
        private readonly RelayCommand _runCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _showHelpCommand;

        private CancellationTokenSource _cancellationTokenSource;

        private string _inputFolderPath = string.Empty;
        private bool _includeSubfolders = true;
        private bool _saveToSourcePath = true;
        private MdbOutputFormat _selectedOutputFormat = MdbOutputFormat.FileGeodatabase;
        private MdbOutputFormatOption _selectedOutputFormatOption;
        private string _outputFolderPath = string.Empty;
        private bool _isProcessing;
        private bool _isScanning;
        private int _progress;
        private string _logText = string.Empty;
        private string _selectionSummary = "已选择 0 / 0";

        public MdbBatchToGdbViewModel()
        {
            MdbItems = new ObservableCollection<MdbFileItem>();
            _selectedOutputFormatOption = OutputFormats[0];

            _browseInputFolderCommand = new RelayCommand(BrowseInputFolder, () => !IsBusy);
            _browseOutputFolderCommand = new RelayCommand(BrowseOutputFolder, () => !IsBusy && !SaveToSourcePath);
            _refreshMdbListCommand = new RelayCommand(async () => await RefreshMdbListAsync(), () => CanRefresh);
            _selectAllCommand = new RelayCommand(SelectAll, () => CanEditSelection);
            _invertSelectionCommand = new RelayCommand(InvertSelection, () => CanEditSelection);
            _clearSelectionCommand = new RelayCommand(ClearSelection, () => CanEditSelection);
            _runCommand = new RelayCommand(async () => await RunAsync(), () => CanRun);
            _cancelCommand = new RelayCommand(Cancel, () => IsProcessing);
            _showHelpCommand = new RelayCommand(ShowHelp);

            string defaultWorkspace = PathDialogUtils.GetProjectDefaultGdb();
            string defaultOutputFolder = Path.GetDirectoryName(defaultWorkspace);
            OutputFolderPath = string.IsNullOrWhiteSpace(defaultOutputFolder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : defaultOutputFolder;

            AppendInfo("工具已加载，请先选择包含 MDB 的文件夹。");
        }
    }
}
