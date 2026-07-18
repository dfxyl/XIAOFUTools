using System;
using System.ComponentModel;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;

namespace XIAOFUTools.Features.Conversion.ExcelToPdf
{
    internal partial class ExcelToPdfDockPaneViewModel
    {
        public string InputFolder
        {
            get => _inputFolder;
            set
            {
                if (_inputFolder != value)
                {
                    _inputFolder = value;
                    OnPropertyChanged(nameof(InputFolder));
                }
            }
        }
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                if (_outputFolder != value)
                {
                    _outputFolder = value;
                    OnPropertyChanged(nameof(OutputFolder));
                    OnPropertyChanged(nameof(ShowKeepOriginalStructure));
                    OnPropertyChanged(nameof(ShowOutputFolder));
                }
            }
        }
        public bool SaveToSourcePath
        {
            get => _saveToSourcePath;
            set
            {
                if (_saveToSourcePath != value)
                {
                    _saveToSourcePath = value;
                    OnPropertyChanged(nameof(SaveToSourcePath));
                    OnPropertyChanged(nameof(ShowKeepOriginalStructure));
                    OnPropertyChanged(nameof(ShowOutputFolder));
                }
            }
        }
        public bool KeepOriginalStructure
        {
            get => _keepOriginalStructure;
            set
            {
                if (_keepOriginalStructure != value)
                {
                    _keepOriginalStructure = value;
                    OnPropertyChanged(nameof(KeepOriginalStructure));
                }
            }
        }
        public bool SeparateWorksheets
        {
            get => _separateWorksheets;
            set
            {
                if (_separateWorksheets != value)
                {
                    _separateWorksheets = value;
                    OnPropertyChanged(nameof(SeparateWorksheets));
                }
            }
        }
        public bool TraverseSubfolders
        {
            get => _traverseSubfolders;
            set
            {
                if (_traverseSubfolders != value)
                {
                    _traverseSubfolders = value;
                    OnPropertyChanged(nameof(TraverseSubfolders));
                }
            }
        }
        public int SelectedPageOrientation
        {
            get => _selectedPageOrientation;
            set
            {
                if (_selectedPageOrientation != value)
                {
                    _selectedPageOrientation = value;
                    OnPropertyChanged(nameof(SelectedPageOrientation));
                }
            }
        }
        public int SelectedPageSize
        {
            get => _selectedPageSize;
            set
            {
                if (_selectedPageSize != value)
                {
                    _selectedPageSize = value;
                    OnPropertyChanged(nameof(SelectedPageSize));
                }
            }
        }
        public int SelectedPrintLayout
        {
            get => _selectedPrintLayout;
            set
            {
                if (_selectedPrintLayout != value)
                {
                    _selectedPrintLayout = value;
                    OnPropertyChanged(nameof(SelectedPrintLayout));
                }
            }
        }

        public bool ShowKeepOriginalStructure => !SaveToSourcePath && !string.IsNullOrWhiteSpace(OutputFolder);
        public bool ShowOutputFolder => !SaveToSourcePath;
        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) > 0.01)
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                }
            }
        }
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set
            {
                if (_isProgressIndeterminate != value)
                {
                    _isProgressIndeterminate = value;
                    OnPropertyChanged(nameof(IsProgressIndeterminate));
                }
            }
        }
        public string LogText
        {
            get => _logText;
            set
            {
                if (_logText != value)
                {
                    _logText = value;
                    OnPropertyChanged(nameof(LogText));
                }
            }
        }
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged(nameof(IsProcessing));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand SelectInputFolderCommand { get; }
        public ICommand SelectOutputFolderCommand { get; }
        public ICommand StartConversionCommand { get; }
        public ICommand StopConversionCommand { get; }
        public ICommand HelpCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
