using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Common;

namespace XIAOFUTools.Tools.SpecialCoordinateTransform
{
    internal enum TransformMode
    {
        SingleLayer = 0,
        BatchShapefile = 1,
        FileGeodatabase = 2
    }

    public sealed class ConversionTypeInfo
    {
        public string Key { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }

    public sealed class BatchTransformItem : PropertyChangedBase
    {
        private bool _isSelected = true;
        private string _status = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Name { get; set; } = string.Empty;

        public string RelativePath { get; set; } = string.Empty;

        public string FullPath { get; set; } = string.Empty;

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }
    }

    internal sealed partial class SpecialCoordinateTransformDockPaneViewModel : PropertyChangedBase
    {
        private readonly RelayCommand _browseOutputPathCommand;
        private readonly RelayCommand _browseBatchInputFolderCommand;
        private readonly RelayCommand _browseBatchOutputFolderCommand;
        private readonly RelayCommand _refreshBatchItemsCommand;
        private readonly RelayCommand _selectAllBatchItemsCommand;
        private readonly RelayCommand _invertBatchItemsCommand;
        private readonly RelayCommand _clearBatchItemsSelectionCommand;
        private readonly RelayCommand _browseGdbInputFolderCommand;
        private readonly RelayCommand _browseGdbOutputFolderCommand;
        private readonly RelayCommand _refreshGdbItemsCommand;
        private readonly RelayCommand _selectAllGdbItemsCommand;
        private readonly RelayCommand _invertGdbItemsCommand;
        private readonly RelayCommand _clearGdbItemsSelectionCommand;
        private readonly RelayCommand _runCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _showHelpCommand;
        private readonly RelayCommand _refreshLayersCommand;

        private CancellationTokenSource _cancellationTokenSource;
        private bool _isProcessing;
        private bool _isScanning;
        private double _progress;
        private bool _isProgressIndeterminate;
        private string _statusMessage = "准备就绪";
        private string _logContent = string.Empty;
        private bool _hasLoggedNoFeatureLayers;
        private int _selectedModeIndex;
        private Layer _selectedInputLayer;
        private string _outputPath = string.Empty;
        private string _batchInputFolderPath = string.Empty;
        private bool _batchIncludeSubfolders = true;
        private bool _batchSaveToSourceFolder = true;
        private string _batchOutputFolderPath = string.Empty;
        private string _batchSelectionSummary = "已选择 0 / 0";
        private string _gdbInputFolderPath = string.Empty;
        private bool _gdbIncludeSubfolders = true;
        private bool _gdbSaveToSourceFolder = true;
        private string _gdbOutputFolderPath = string.Empty;
        private string _gdbSelectionSummary = "已选择 0 / 0";
        private ConversionTypeInfo _selectedConversionType;

        public SpecialCoordinateTransformDockPaneViewModel()
        {
            FeatureLayers = new ObservableCollection<Layer>();
            ConversionTypes = new ObservableCollection<ConversionTypeInfo>();
            BatchItems = new ObservableCollection<BatchTransformItem>();
            GdbItems = new ObservableCollection<BatchTransformItem>();

            _browseOutputPathCommand = new RelayCommand(BrowseOutputPath, () => !IsBusy);
            _browseBatchInputFolderCommand = new RelayCommand(BrowseBatchInputFolder, () => !IsBusy);
            _browseBatchOutputFolderCommand = new RelayCommand(BrowseBatchOutputFolder, () => !IsBusy && ShowBatchOutputFolder);
            _refreshBatchItemsCommand = new RelayCommand(async () => await RefreshBatchItemsAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(BatchInputFolderPath));
            _selectAllBatchItemsCommand = new RelayCommand(SelectAllBatchItems, () => !IsBusy && BatchItems.Count > 0);
            _invertBatchItemsCommand = new RelayCommand(InvertBatchItems, () => !IsBusy && BatchItems.Count > 0);
            _clearBatchItemsSelectionCommand = new RelayCommand(ClearBatchItemsSelection, () => !IsBusy && BatchItems.Count > 0);
            _browseGdbInputFolderCommand = new RelayCommand(BrowseGdbInputFolder, () => !IsBusy);
            _browseGdbOutputFolderCommand = new RelayCommand(BrowseGdbOutputFolder, () => !IsBusy && ShowGdbOutputFolder);
            _refreshGdbItemsCommand = new RelayCommand(async () => await RefreshGdbItemsAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(GdbInputFolderPath));
            _selectAllGdbItemsCommand = new RelayCommand(SelectAllGdbItems, () => !IsBusy && GdbItems.Count > 0);
            _invertGdbItemsCommand = new RelayCommand(InvertGdbItems, () => !IsBusy && GdbItems.Count > 0);
            _clearGdbItemsSelectionCommand = new RelayCommand(ClearGdbItemsSelection, () => !IsBusy && GdbItems.Count > 0);
            _runCommand = new RelayCommand(async () => await RunTransformAsync(), () => CanProcess);
            _cancelCommand = new RelayCommand(CancelTransform, () => IsProcessing);
            _showHelpCommand = new RelayCommand(ShowHelp);
            _refreshLayersCommand = new RelayCommand(RefreshLayers, () => !IsBusy);

            string defaultOutputFolder = GetDefaultOutputFolder();
            _batchOutputFolderPath = defaultOutputFolder;
            _gdbOutputFolderPath = defaultOutputFolder;

            InitializeConversionTypes();
            LoadFeatureLayers();
        }

        public ObservableCollection<Layer> FeatureLayers { get; }

        public ObservableCollection<ConversionTypeInfo> ConversionTypes { get; }

        public ObservableCollection<BatchTransformItem> BatchItems { get; }

        public ObservableCollection<BatchTransformItem> GdbItems { get; }

        public ICommand BrowseOutputPathCommand => _browseOutputPathCommand;
        public ICommand BrowseBatchInputFolderCommand => _browseBatchInputFolderCommand;
        public ICommand BrowseBatchOutputFolderCommand => _browseBatchOutputFolderCommand;
        public ICommand RefreshBatchItemsCommand => _refreshBatchItemsCommand;
        public ICommand SelectAllBatchItemsCommand => _selectAllBatchItemsCommand;
        public ICommand InvertBatchItemsCommand => _invertBatchItemsCommand;
        public ICommand ClearBatchItemsSelectionCommand => _clearBatchItemsSelectionCommand;
        public ICommand BrowseGdbInputFolderCommand => _browseGdbInputFolderCommand;
        public ICommand BrowseGdbOutputFolderCommand => _browseGdbOutputFolderCommand;
        public ICommand RefreshGdbItemsCommand => _refreshGdbItemsCommand;
        public ICommand SelectAllGdbItemsCommand => _selectAllGdbItemsCommand;
        public ICommand InvertGdbItemsCommand => _invertGdbItemsCommand;
        public ICommand ClearGdbItemsSelectionCommand => _clearGdbItemsSelectionCommand;
        public ICommand RunCommand => _runCommand;
        public ICommand CancelCommand => _cancelCommand;
        public ICommand ShowHelpCommand => _showHelpCommand;
        public ICommand RefreshLayersCommand => _refreshLayersCommand;

        public int SelectedModeIndex
        {
            get => _selectedModeIndex;
            set
            {
                if (SetProperty(ref _selectedModeIndex, value))
                {
                    NotifyModeStateChanged();
                }
            }
        }

        public TransformMode SelectedMode => (TransformMode)SelectedModeIndex;

        public bool IsBusy => IsProcessing || IsScanning;

        public bool ShowBatchOutputFolder => !BatchSaveToSourceFolder;

        public bool ShowGdbOutputFolder => !GdbSaveToSourceFolder;

        public bool CanProcess => SelectedConversionType != null && !IsBusy && CanProcessCurrentMode();

        public void RefreshLayers() => LoadFeatureLayers();
    }
}
