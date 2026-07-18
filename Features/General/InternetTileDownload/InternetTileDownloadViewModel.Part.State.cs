#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;
using XIAOFUTools.Features.General.InternetTileDownload.Infrastructure;
using XIAOFUTools.Features.General.InternetTileDownload.Services;

namespace XIAOFUTools.Features.General.InternetTileDownload
{
    internal sealed partial class InternetTileDownloadViewModel
    {

        public ObservableCollection<LevelOption> LevelOptions { get; }


        public ObservableCollection<FeatureLayer> FeatureLayers { get; }


        public AreaSourceOption[] AreaSourceOptions { get; }


        public string ServiceUrl
        {
            get => _serviceUrl;
            set
            {
                if (SetProperty(ref _serviceUrl, value))
                {
                    UpdateCommands();
                }
            }
        }


        public string ServiceSummary
        {
            get => _serviceSummary;
            set => SetProperty(ref _serviceSummary, value);
        }


        public LevelOption? SelectedLevelOption
        {
            get => _selectedLevelOption;
            set
            {
                if (SetProperty(ref _selectedLevelOption, value))
                {
                    UpdateCommands();
                }
            }
        }


        public AreaSourceOption? SelectedAreaSourceOption
        {
            get => _selectedAreaSourceOption;
            set
            {
                if (SetProperty(ref _selectedAreaSourceOption, value))
                {
                    UpdateAreaSummary();
                    UpdateCommands();
                    OnPropertyChanged(nameof(IsCustomExtentSource));
                    OnPropertyChanged(nameof(IsFeatureLayerSource));
                }
            }
        }


        public FeatureLayer? SelectedFeatureLayer
        {
            get => _selectedFeatureLayer;
            set
            {
                if (SetProperty(ref _selectedFeatureLayer, value))
                {
                    UpdateAreaSummary();
                    UpdateCommands();
                }
            }
        }


        public string OutputFolderPath
        {
            get => _outputFolderPath;
            set
            {
                if (SetProperty(ref _outputFolderPath, value))
                {
                    UpdateCommands();
                }
            }
        }


        public string OutputSpatialReferenceDisplay
        {
            get => _outputSpatialReferenceDisplay;
            set => SetProperty(ref _outputSpatialReferenceDisplay, value);
        }


        public string AreaSummary
        {
            get => _areaSummary;
            set => SetProperty(ref _areaSummary, value);
        }


        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    UpdateCommands();
                }
            }
        }


        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }


        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }


        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }


        public bool IsCustomExtentSource => SelectedAreaSourceOption?.AreaSourceType == InternetTileAreaSourceType.CustomExtent;


        public bool IsFeatureLayerSource => SelectedAreaSourceOption?.AreaSourceType == InternetTileAreaSourceType.FeatureLayer;


        public bool CanResolve => !IsProcessing && !string.IsNullOrWhiteSpace(ServiceUrl);


        public bool CanDownload =>
            !IsProcessing &&
            _resolvedServiceDefinition != null &&
            SelectedLevelOption != null &&
            !string.IsNullOrWhiteSpace(OutputFolderPath) &&
            (!IsFeatureLayerSource || SelectedFeatureLayer != null) &&
            (!IsCustomExtentSource || _customExtent != null);


        public ICommand ResolveServiceCommand { get; }


        public ICommand DownloadCommand { get; }


        public ICommand CancelCommand { get; }


        public ICommand BrowseOutputFolderCommand { get; }


        public ICommand PickSpatialReferenceCommand { get; }


        public ICommand DrawExtentCommand { get; }


        public ICommand RefreshLayersCommand { get; }


        public ICommand ShowHelpCommand { get; }


        public event PropertyChangedEventHandler? PropertyChanged;

    }
}
