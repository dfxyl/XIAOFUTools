using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.DocumentBatchReplace
{
    internal partial class DocumentBatchReplaceDockPaneViewModel : INotifyPropertyChanged
    {
        private readonly IDocumentInputPathResolver _inputPathResolver;
        private readonly IDocumentBatchReplacementService _replacementService;

        private string _selectedInputFile;
        private string _outputFolder;
        private bool _saveAsCopy = true;
        private bool _traverseSubfolders = true;
        private bool _matchCase;
        private bool _matchWholeWord;
        private bool _matchByte;
        private bool _useWildcards;
        private double _progress;
        private bool _isProgressIndeterminate;
        private string _logText = string.Empty;
        private string _statusText = "等待开始";
        private bool _isProcessing;
        private CancellationTokenSource _cancellationTokenSource;

        public DocumentBatchReplaceDockPaneViewModel()
            : this(
                new DocumentInputPathResolver(),
                new WordDocumentBatchReplacementService())
        {
        }

        internal DocumentBatchReplaceDockPaneViewModel(
            IDocumentInputPathResolver inputPathResolver,
            IDocumentBatchReplacementService replacementService)
        {
            _inputPathResolver = inputPathResolver ??
                throw new ArgumentNullException(nameof(inputPathResolver));
            _replacementService = replacementService ??
                throw new ArgumentNullException(nameof(replacementService));
            AddFilesCommand = new RelayCommand(_ => AddFiles());
            AddFolderCommand = new RelayCommand(_ => AddFolder());
            RemoveSelectedFileCommand = new RelayCommand(_ => RemoveSelectedFile(), _ => !string.IsNullOrWhiteSpace(SelectedInputFile));
            ClearFilesCommand = new RelayCommand(_ => ClearFiles(), _ => InputFiles.Count > 0);
            AddRuleCommand = new RelayCommand(_ => AddRule());
            RemoveRuleCommand = new RelayCommand(RemoveRule, CanRemoveRule);
            ClearRulesCommand = new RelayCommand(_ => ClearRules(), _ => ReplaceRules.Count > 0);
            SelectOutputFolderCommand = new RelayCommand(_ => SelectOutputFolder());
            StartReplaceCommand = new RelayCommand(async _ => await StartReplaceAsync(), _ => !IsProcessing);
            StopReplaceCommand = new RelayCommand(_ => StopReplace(), _ => IsProcessing);
            HelpCommand = new RelayCommand(_ => ShowHelp());

            InputFiles.CollectionChanged += OnInputFilesCollectionChanged;
            ReplaceRules.CollectionChanged += OnReplaceRulesCollectionChanged;

            ReplaceRules.Add(new ReplaceRuleItem());
            LogMessage("文档批量替换工具已加载");
        }


    }

    internal class ReplaceRuleItem : INotifyPropertyChanged
    {
        private string _findText = string.Empty;
        private string _replaceText = string.Empty;

        public string FindText
        {
            get => _findText;
            set
            {
                if (_findText != value)
                {
                    _findText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ReplaceText
        {
            get => _replaceText;
            set
            {
                if (_replaceText != value)
                {
                    _replaceText = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
