using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace XIAOFUTools.Features.Conversion.DocumentBatchReplace
{
    internal partial class DocumentBatchReplaceDockPaneViewModel
    {

        public ObservableCollection<string> InputFiles { get; } = new();
        public ObservableCollection<ReplaceRuleItem> ReplaceRules { get; } = new();

        public string SelectedInputFile
        {
            get => _selectedInputFile;
            set
            {
                if (_selectedInputFile != value)
                {
                    _selectedInputFile = value;
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
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
                    OnPropertyChanged();
                }
            }
        }

        public bool SaveAsCopy
        {
            get => _saveAsCopy;
            set
            {
                if (_saveAsCopy != value)
                {
                    _saveAsCopy = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(OverwriteOriginal));
                    OnPropertyChanged(nameof(ShowOutputFolder));
                }
            }
        }

        public bool OverwriteOriginal
        {
            get => !SaveAsCopy;
            set
            {
                SaveAsCopy = !value;
            }
        }

        public bool ShowOutputFolder => SaveAsCopy;

        public bool TraverseSubfolders
        {
            get => _traverseSubfolders;
            set
            {
                if (_traverseSubfolders != value)
                {
                    _traverseSubfolders = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool MatchCase
        {
            get => _matchCase;
            set
            {
                if (_matchCase != value)
                {
                    _matchCase = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool MatchWholeWord
        {
            get => _matchWholeWord;
            set
            {
                if (_matchWholeWord != value)
                {
                    _matchWholeWord = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool MatchByte
        {
            get => _matchByte;
            set
            {
                if (_matchByte != value)
                {
                    _matchByte = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool UseWildcards
        {
            get => _useWildcards;
            set
            {
                if (_useWildcards != value)
                {
                    _useWildcards = value;
                    OnPropertyChanged();
                }
            }
        }

        public string FileCountText => $"已添加 {InputFiles.Count} 个文件";

        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) > 0.01)
                {
                    _progress = value;
                    OnPropertyChanged();
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
                    OnPropertyChanged();
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
                    OnPropertyChanged();
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
                    OnPropertyChanged();
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
                    OnPropertyChanged();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ICommand AddFilesCommand { get; }
        public ICommand AddFolderCommand { get; }
        public ICommand RemoveSelectedFileCommand { get; }
        public ICommand ClearFilesCommand { get; }
        public ICommand AddRuleCommand { get; }
        public ICommand RemoveRuleCommand { get; }
        public ICommand ClearRulesCommand { get; }
        public ICommand SelectOutputFolderCommand { get; }
        public ICommand StartReplaceCommand { get; }
        public ICommand StopReplaceCommand { get; }
        public ICommand HelpCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
