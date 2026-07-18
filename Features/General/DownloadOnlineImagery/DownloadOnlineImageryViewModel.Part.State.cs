using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks; 
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Text;
using System.Globalization;

namespace XIAOFUTools.Features.General.DownloadOnlineImagery
{
    internal partial class DownloadOnlineImageryViewModel
    {
        public ObservableCollection<Layer> FeatureLayers
        {
            get => _featureLayers;
            set => SetProperty(ref _featureLayers, value);
        }
        public Layer SelectedFeatureLayer
        {
            get => _selectedFeatureLayer;
            set
            {
                SetProperty(ref _selectedFeatureLayer, value);
                NotifyCanExecuteChanged();
            }
        }
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                SetProperty(ref _outputFolder, value);
                NotifyCanExecuteChanged();
            }
        }
        public ObservableCollection<DownloadLevelItem> DownloadLevels
        {
            get => _downloadLevels;
            set => SetProperty(ref _downloadLevels, value);
        }
        public DownloadLevelItem SelectedDownloadLevel
        {
            get => _selectedDownloadLevel;
            set
            {
                SetProperty(ref _selectedDownloadLevel, value);
                NotifyCanExecuteChanged();
            }
        }
        public bool MergeImages
        {
            get => _mergeImages;
            set => SetProperty(ref _mergeImages, value);
        }
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
                NotifyCanExecuteChanged();
            }
        }

        public bool CanProcess => !IsProcessing;
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }
        public string LogMessages
        {
            get => _logMessages;
            set => SetProperty(ref _logMessages, value);
        }
        public ICommand BrowseOutputFolderCommand
        {
            get => _browseOutputFolderCommand ?? (_browseOutputFolderCommand = new RelayCommand(BrowseOutputFolder));
        }
        public ICommand StartDownloadCommand
        {
            get => _startDownloadCommand ?? (_startDownloadCommand = new RelayCommand(StartDownload, CanStartDownload));
        }
        public ICommand ShowHelpCommand
        {
            get => _showHelpCommand ?? (_showHelpCommand = new RelayCommand(ShowHelp));
        }
        public ICommand StopDownloadCommand
        {
            get => _stopDownloadCommand ?? (_stopDownloadCommand = new RelayCommand(StopDownload, CanStopDownload));
        }
        public ICommand RefreshLayersCommand
        {
            get => _refreshLayersCommand ?? (_refreshLayersCommand = new RelayCommand(RefreshLayers));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
