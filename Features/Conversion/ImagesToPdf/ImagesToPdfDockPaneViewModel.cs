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

namespace XIAOFUTools.Features.Conversion.ImagesToPdf
{
    /// <summary>
    /// 图片批量转PDF停靠窗格视图模型
    /// </summary>
    internal partial class ImagesToPdfDockPaneViewModel : INotifyPropertyChanged
    {
        private readonly Infrastructure.ImagePdfFileStore _fileStore = new();
        private readonly Infrastructure.ImagePdfDocumentWriter _documentWriter;

        private string _inputFolder;

        private string _outputFolder;

        private bool _saveToSourcePath = true;

        private bool _keepOriginalStructure;

        private bool _mergeImages = true;

        private bool _traverseSubfolders = true;

        private int _dpi = 300;

        private double _progress;

        private bool _isProgressIndeterminate;

        private string _logText;

        private string _statusText;

        private bool _isProcessing;

        private CancellationTokenSource _cancellationTokenSource;

        // 支持的图片格式
        private static readonly string[] SupportedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

        public ImagesToPdfDockPaneViewModel()
        {
            _documentWriter = new Infrastructure.ImagePdfDocumentWriter(_fileStore);
            SelectInputFolderCommand = new RelayCommand(SelectInputFolder);
            SelectOutputFolderCommand = new RelayCommand(SelectOutputFolder);
            StartConversionCommand = new RelayCommand(async () => await StartConversionAsync(), () => !IsProcessing);
            StopConversionCommand = new RelayCommand(StopConversion, () => IsProcessing);
            HelpCommand = new RelayCommand(ShowHelp);

            LogMessage("图片批量转PDF工具已加载");
        }
    }
}
