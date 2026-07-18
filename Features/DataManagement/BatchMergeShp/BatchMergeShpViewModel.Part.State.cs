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

namespace XIAOFUTools.Features.DataManagement.BatchMergeShp
{
    internal partial class BatchMergeShpViewModel
    {

        public ICommand BrowseInputFolderCommand => _browseInputFolderCommand;
        public ICommand RefreshShpListCommand => _refreshShpListCommand;
        public ICommand BrowseOutputPathCommand => _browseOutputPathCommand;
        public ICommand SelectAllCommand => _selectAllCommand;
        public ICommand InvertSelectionCommand => _invertSelectionCommand;
        public ICommand ClearSelectionCommand => _clearSelectionCommand;
        public ICommand SelectPointCommand => _selectPointCommand;
        public ICommand SelectLineCommand => _selectLineCommand;
        public ICommand SelectPolygonCommand => _selectPolygonCommand;
        public ICommand RunCommand => _runCommand;
        public ICommand CancelCommand => _cancelCommand;
        public ICommand ShowHelpCommand => _showHelpCommand;

        public ObservableCollection<ShpMergeItem> ShpItems { get; }

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
                        _ = RefreshShpListAsync();
                    }
                }
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set
            {
                if (SetProperty(ref _outputPath, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool AddSourceFileField
        {
            get => _addSourceFileField;
            set => SetProperty(ref _addSourceFileField, value);
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

        public bool CanRun => !IsBusy
                              && !string.IsNullOrWhiteSpace(OutputPath)
                              && HasValidGeometrySelection();

        private bool CanEditSelection => !IsBusy && ShpItems.Count > 0;
    }
}
