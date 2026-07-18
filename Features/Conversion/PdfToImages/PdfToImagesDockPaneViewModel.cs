using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.PdfToImages
{
    /// <summary>
    /// PDF批量转图片停靠窗格视图模型
    /// </summary>
    internal partial class PdfToImagesDockPaneViewModel : INotifyPropertyChanged
    {
        private readonly Infrastructure.PdfImageConversionService _conversionService = new();

        private string _inputFolder;

        private string _outputFolder;

        private bool _saveToSourcePath = true;

        private bool _keepOriginalStructure;

        private bool _createSeparateFolder = true;

        private bool _traverseSubfolders;

        private int _resolution = 300;

        private string _outputFormat = "JPG";

        private double _progress;

        private bool _isProgressIndeterminate;

        private string _logText;

        private string _statusText;

        private bool _isProcessing;

        private CancellationTokenSource _cancellationTokenSource;

        public PdfToImagesDockPaneViewModel()
        {
            SelectInputFolderCommand = new RelayCommand(SelectInputFolder);
            SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
            StartConversionCommand = new RelayCommand(async () => await StartConversionAsync(), () => !IsProcessing);
            StopConversionCommand = new RelayCommand(StopConversion, () => IsProcessing);
            HelpCommand = new RelayCommand(ShowHelp);

            LogMessage("PDF批量转图片工具已加载");
        }
    }
}
