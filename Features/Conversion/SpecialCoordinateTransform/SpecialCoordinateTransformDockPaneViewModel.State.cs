using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Conversion.SpecialCoordinateTransform
{
    internal sealed partial class SpecialCoordinateTransformDockPaneViewModel
    {
        public Layer SelectedInputLayer
        {
            get => _selectedInputLayer;
            set
            {
                if (SetProperty(ref _selectedInputLayer, value))
                {
                    UpdateSingleOutputPath();
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
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

        public string BatchInputFolderPath
        {
            get => _batchInputFolderPath;
            set
            {
                if (SetProperty(ref _batchInputFolderPath, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool BatchIncludeSubfolders
        {
            get => _batchIncludeSubfolders;
            set
            {
                if (SetProperty(ref _batchIncludeSubfolders, value))
                {
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool BatchSaveToSourceFolder
        {
            get => _batchSaveToSourceFolder;
            set
            {
                if (SetProperty(ref _batchSaveToSourceFolder, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public string BatchOutputFolderPath
        {
            get => _batchOutputFolderPath;
            set
            {
                if (SetProperty(ref _batchOutputFolderPath, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public string BatchSelectionSummary
        {
            get => _batchSelectionSummary;
            set => SetProperty(ref _batchSelectionSummary, value);
        }

        public string GdbInputFolderPath
        {
            get => _gdbInputFolderPath;
            set
            {
                if (SetProperty(ref _gdbInputFolderPath, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool GdbIncludeSubfolders
        {
            get => _gdbIncludeSubfolders;
            set
            {
                if (SetProperty(ref _gdbIncludeSubfolders, value))
                {
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool GdbSaveToSourceFolder
        {
            get => _gdbSaveToSourceFolder;
            set
            {
                if (SetProperty(ref _gdbSaveToSourceFolder, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public string GdbOutputFolderPath
        {
            get => _gdbOutputFolderPath;
            set
            {
                if (SetProperty(ref _gdbOutputFolderPath, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public string GdbSelectionSummary
        {
            get => _gdbSelectionSummary;
            set => SetProperty(ref _gdbSelectionSummary, value);
        }

        public ConversionTypeInfo SelectedConversionType
        {
            get => _selectedConversionType;
            set
            {
                if (SetProperty(ref _selectedConversionType, value))
                {
                    UpdateSingleOutputPath();
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
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public bool IsScanning
        {
            get => _isScanning;
            private set
            {
                if (SetProperty(ref _isScanning, value))
                {
                    NotifyStatePropertiesChanged();
                    RaiseCommandCanExecuteChanged();
                }
            }
        }

        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        private void InitializeConversionTypes()
        {
            ConversionTypes.Clear();
            foreach (KeyValuePair<string, string> pair in CoordinateConversionTypes.Types)
            {
                ConversionTypes.Add(new ConversionTypeInfo
                {
                    Key = pair.Key,
                    DisplayName = pair.Value
                });
            }

            if (ConversionTypes.Count > 0)
            {
                SelectedConversionType = ConversionTypes[0];
            }
        }

        private async void LoadFeatureLayers()
        {
            try
            {
                List<FeatureLayer> layers = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null)
                    {
                        return new List<FeatureLayer>();
                    }

                    return map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                });

                FeatureLayers.Clear();
                foreach (FeatureLayer layer in layers)
                {
                    FeatureLayers.Add(layer);
                }

                if (FeatureLayers.Count == 0)
                {
                    SelectedInputLayer = null;
                    if (!_hasLoggedNoFeatureLayers)
                    {
                        AddLog("当前没有可用的地图要素图层。");
                        _hasLoggedNoFeatureLayers = true;
                    }
                }
                else if (SelectedInputLayer == null || !FeatureLayers.Contains(SelectedInputLayer))
                {
                    _hasLoggedNoFeatureLayers = false;
                    SelectedInputLayer = FeatureLayers[0];
                }
                else
                {
                    _hasLoggedNoFeatureLayers = false;
                }

                NotifyStatePropertiesChanged();
                RaiseCommandCanExecuteChanged();
            }
            catch (Exception ex)
            {
                AddLog($"加载图层失败: {ex.Message}");
            }
        }

        private void BrowseOutputPath()
        {
            string pickedPath = PathDialogUtils.PickSaveFeatureClassPath("选择输出位置", Project.Current?.DefaultGeodatabasePath);
            if (string.IsNullOrWhiteSpace(pickedPath))
            {
                return;
            }

            string defaultName = $"{SelectedInputLayer?.Name ?? "Feature"}_{GetConversionShortName(SelectedConversionType?.Key ?? "Converted")}";
            OutputPath = OutputDatasetUtils.NormalizeOutputPath(pickedPath, defaultName);
            AddLog($"已选择输出路径: {OutputPath}");
        }

        private bool CanProcessCurrentMode()
        {
            return SelectedMode switch
            {
                TransformMode.SingleLayer => SelectedInputLayer != null && !string.IsNullOrWhiteSpace(OutputPath),
                TransformMode.BatchShapefile => BatchItems.Any(item => item.IsSelected)
                                               && !string.IsNullOrWhiteSpace(BatchInputFolderPath)
                                               && (BatchSaveToSourceFolder || !string.IsNullOrWhiteSpace(BatchOutputFolderPath)),
                TransformMode.FileGeodatabase => GdbItems.Any(item => item.IsSelected)
                                                && !string.IsNullOrWhiteSpace(GdbInputFolderPath)
                                                && (GdbSaveToSourceFolder || !string.IsNullOrWhiteSpace(GdbOutputFolderPath)),
                _ => false
            };
        }

        private string GetDefaultOutputFolder()
        {
            string projectGdb = PathDialogUtils.GetProjectDefaultGdb();
            string folder = System.IO.Path.GetDirectoryName(projectGdb);
            return string.IsNullOrWhiteSpace(folder)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                : folder;
        }

        private void NotifyModeStateChanged()
        {
            NotifyStatePropertiesChanged();
            RaiseCommandCanExecuteChanged();
        }

        private void NotifyStatePropertiesChanged()
        {
            NotifyPropertyChanged(nameof(CanProcess));
            NotifyPropertyChanged(nameof(IsBusy));
            NotifyPropertyChanged(nameof(ShowBatchOutputFolder));
            NotifyPropertyChanged(nameof(ShowGdbOutputFolder));
        }

        private void RaiseCommandCanExecuteChanged()
        {
            _browseOutputPathCommand.RaiseCanExecuteChanged();
            _browseBatchInputFolderCommand.RaiseCanExecuteChanged();
            _browseBatchOutputFolderCommand.RaiseCanExecuteChanged();
            _refreshBatchItemsCommand.RaiseCanExecuteChanged();
            _selectAllBatchItemsCommand.RaiseCanExecuteChanged();
            _invertBatchItemsCommand.RaiseCanExecuteChanged();
            _clearBatchItemsSelectionCommand.RaiseCanExecuteChanged();
            _browseGdbInputFolderCommand.RaiseCanExecuteChanged();
            _browseGdbOutputFolderCommand.RaiseCanExecuteChanged();
            _refreshGdbItemsCommand.RaiseCanExecuteChanged();
            _selectAllGdbItemsCommand.RaiseCanExecuteChanged();
            _invertGdbItemsCommand.RaiseCanExecuteChanged();
            _clearGdbItemsSelectionCommand.RaiseCanExecuteChanged();
            _runCommand.RaiseCanExecuteChanged();
            _cancelCommand.RaiseCanExecuteChanged();
            _refreshLayersCommand.RaiseCanExecuteChanged();
        }

        private static void ExecuteOnUiThread(Action action)
        {
            PresentationServices.UiThread.InvokeOrRun(action);
        }
    }
}
