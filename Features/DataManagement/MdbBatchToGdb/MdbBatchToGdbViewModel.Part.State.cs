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

        public ICommand BrowseInputFolderCommand => _browseInputFolderCommand;

        public ICommand BrowseOutputFolderCommand => _browseOutputFolderCommand;

        public ICommand RefreshMdbListCommand => _refreshMdbListCommand;

        public ICommand SelectAllCommand => _selectAllCommand;

        public ICommand InvertSelectionCommand => _invertSelectionCommand;

        public ICommand ClearSelectionCommand => _clearSelectionCommand;

        public ICommand RunCommand => _runCommand;

        public ICommand CancelCommand => _cancelCommand;

        public ICommand ShowHelpCommand => _showHelpCommand;

        public ObservableCollection<MdbFileItem> MdbItems { get; }

        public string InputFolderPath
        {
            get => _inputFolderPath;
            set
            {
                if (SetProperty(ref _inputFolderPath, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set
            {
                if (SetProperty(ref _includeSubfolders, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();

                    if (!IsBusy && _fileStore.DirectoryExists(InputFolderPath))
                    {
                        _ = RefreshMdbListAsync();
                    }
                }
            }
        }

        public bool SaveToSourcePath
        {
            get => _saveToSourcePath;
            set
            {
                if (SetProperty(ref _saveToSourcePath, value))
                {
                    NotifyPropertyChanged(() => ShowOutputFolder);
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public IReadOnlyList<MdbOutputFormatOption> OutputFormats { get; } = new[]
        {
            new MdbOutputFormatOption(MdbOutputFormat.FileGeodatabase, "文件地理数据库 (.gdb)"),
            new MdbOutputFormatOption(MdbOutputFormat.MobileGeodatabase, "移动地理数据库 (.geodatabase)"),
            new MdbOutputFormatOption(MdbOutputFormat.XmlWorkspaceDocument, "XML Workspace Document (.xml)")
        };

        public MdbOutputFormat SelectedOutputFormat
        {
            get => _selectedOutputFormat;
            set
            {
                if (SetProperty(ref _selectedOutputFormat, value))
                {
                    MdbOutputFormatOption selectedOption = OutputFormats.FirstOrDefault(option => option.Format == value);
                    if (selectedOption != null && !ReferenceEquals(_selectedOutputFormatOption, selectedOption))
                    {
                        _selectedOutputFormatOption = selectedOption;
                        NotifyPropertyChanged(() => SelectedOutputFormatOption);
                    }

                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public MdbOutputFormatOption SelectedOutputFormatOption
        {
            get => _selectedOutputFormatOption;
            set
            {
                if (value != null && SetProperty(ref _selectedOutputFormatOption, value))
                {
                    SelectedOutputFormat = value.Format;
                }
            }
        }

        public bool ShowOutputFolder => !SaveToSourcePath;

        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set
            {
                if (SetProperty(ref _outputFolderPath, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    NotifyPropertyChanged(() => IsBusy);
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool IsBusy => IsProcessing || _isScanning;

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public string LogText
        {
            get => _logText;
            set => SetProperty(ref _logText, value);
        }

        public string SelectionSummary
        {
            get => _selectionSummary;
            set => SetProperty(ref _selectionSummary, value);
        }

        public bool CanRefresh => !IsBusy && !string.IsNullOrWhiteSpace(InputFolderPath);

        public bool CanRun
            => !IsBusy
               && HasSelection()
               && !string.IsNullOrWhiteSpace(InputFolderPath)
               && (SaveToSourcePath || !string.IsNullOrWhiteSpace(OutputFolderPath));

        private bool CanEditSelection => !IsBusy && MdbItems.Count > 0;
    }
}
