using System;
using System.ComponentModel;
using System.Threading;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.ExcelToPdf
{
    internal partial class ExcelToPdfDockPaneViewModel : INotifyPropertyChanged
    {

        private string _inputFolder;

        private string _outputFolder;

        private bool _saveToSourcePath = true;

        private bool _keepOriginalStructure;

        private bool _separateWorksheets;

        private bool _traverseSubfolders = true;

        private int _selectedPageOrientation;

        private int _selectedPageSize;

        private int _selectedPrintLayout;

        private double _progress;

        private bool _isProgressIndeterminate;

        private string _logText;

        private string _statusText;

        private bool _isProcessing;

        private CancellationTokenSource _cancellationTokenSource;

        private readonly IExcelPdfConversionService _conversionService;

        public ExcelToPdfDockPaneViewModel()
            : this(new ExcelPdfConversionService())
        {
        }

        internal ExcelToPdfDockPaneViewModel(IExcelPdfConversionService conversionService)
        {
            _conversionService = conversionService ??
                throw new ArgumentNullException(nameof(conversionService));
            SelectInputFolderCommand = new RelayCommand(SelectInputFolder);
            SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
            StartConversionCommand = new RelayCommand(async () => await StartConversionAsync(), () => !IsProcessing);
            StopConversionCommand = new RelayCommand(StopConversion, () => IsProcessing);
            HelpCommand = new RelayCommand(ShowHelp);

            LogMessage("Excel批量转PDF工具已加载");
        }
    }
}
