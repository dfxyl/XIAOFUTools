using System;
using System.ComponentModel;
using System.Threading;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.WordToPdf
{
    internal partial class WordToPdfDockPaneViewModel : INotifyPropertyChanged
    {

        private string _inputFolder;

        private string _outputFolder;

        private bool _saveToSourcePath = true;

        private bool _keepOriginalStructure;

        private bool _traverseSubfolders = true;

        private double _progress;

        private bool _isProgressIndeterminate;

        private string _logText;

        private string _statusText;

        private bool _isProcessing;

        private CancellationTokenSource _cancellationTokenSource;

        private readonly IWordPdfConversionService _conversionService;

        public WordToPdfDockPaneViewModel()
            : this(new WordPdfConversionService())
        {
        }

        internal WordToPdfDockPaneViewModel(IWordPdfConversionService conversionService)
        {
            _conversionService = conversionService ??
                throw new ArgumentNullException(nameof(conversionService));
            SelectInputFolderCommand = new RelayCommand(SelectInputFolder);
            SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
            StartConversionCommand = new RelayCommand(async () => await StartConversionAsync(), () => !IsProcessing);
            StopConversionCommand = new RelayCommand(StopConversion, () => IsProcessing);
            HelpCommand = new RelayCommand(ShowHelp);

            LogMessage("Word批量转PDF工具已加载");
        }
    }
}
