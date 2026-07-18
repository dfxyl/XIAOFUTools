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

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    internal sealed partial class HistoricalImageryDownloadViewModel
    {

        public ObservableCollection<ProviderOption> ProviderOptions { get; }


        public ObservableCollection<GoogleFallbackModeOption> GoogleFallbackModeOptions { get; }


        public ProviderOption? SelectedProviderOption
        {
            get => _selectedProviderOption;
            set
            {
                if (SetProperty(ref _selectedProviderOption, value))
                {
                    ResetVersions();
                    OnPropertyChanged(nameof(IsGoogleProviderSelected));
                    UpdateCommands();
                }
            }
        }


        public ObservableCollection<AreaSourceOption> AreaSourceOptions { get; } 


        public GoogleFallbackModeOption? SelectedGoogleFallbackModeOption
        {
            get => _selectedGoogleFallbackModeOption;
            set => SetProperty(ref _selectedGoogleFallbackModeOption, value);
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


        public ObservableCollection<int> ZoomLevels { get; }


        public int SelectedZoomLevel
        {
            get => _selectedZoomLevel;
            set
            {
                if (SetProperty(ref _selectedZoomLevel, value))
                {
                    UpdateCommands();
                }
            }
        }


        public ObservableCollection<HistoricalVersionSelectionItem> Versions { get; }


        public HistoricalVersionSelectionItem? SelectedVersionPreview
        {
            get => _selectedVersionPreview;
            set
            {
                if (SetProperty(ref _selectedVersionPreview, value))
                {
                    UpdateSelectedVersionSummary();
                }
            }
        }


        public ObservableCollection<FeatureLayer> FeatureLayers { get; }


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


        public string SelectedVersionSummary
        {
            get => _selectedVersionSummary;
            set => SetProperty(ref _selectedVersionSummary, value);
        }


        public bool IsQueryAllVersions
        {
            get => _isQueryAllVersions;
            set
            {
                if (SetProperty(ref _isQueryAllVersions, value))
                {
                    ResetVersions();
                    OnPropertyChanged(nameof(QueryModeDisplayText));
                }
            }
        }


        public string QueryModeDisplayText => IsQueryAllVersions ? "查询全部" : "查询历史";


        public GoogleNearestDateFallbackMode GoogleFallbackMode
            => SelectedGoogleFallbackModeOption?.Mode ?? GoogleNearestDateFallbackMode.SeparateOutputs;


        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    OnPropertyChanged(nameof(CanQuery));
                    OnPropertyChanged(nameof(CanDownload));
                    OnPropertyChanged(nameof(CanConfigureVersions));
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


        public bool IsCustomExtentSource => SelectedAreaSourceOption?.AreaSourceType == HistoricalAreaSourceType.CustomExtent;


        public bool IsFeatureLayerSource => SelectedAreaSourceOption?.AreaSourceType == HistoricalAreaSourceType.FeatureLayer;


        public bool IsGoogleProviderSelected => SelectedProviderOption?.ProviderType == HistoricalImageryProviderType.GoogleEarth;


        public bool CanQuery => !IsProcessing && SelectedProviderOption != null;


        public bool CanDownload =>
            !IsProcessing &&
            SelectedProviderOption != null &&
            Versions.Any(item => item.IsSelected) &&
            !string.IsNullOrWhiteSpace(OutputFolderPath) &&
            (!IsFeatureLayerSource || SelectedFeatureLayer != null) &&
            (!IsCustomExtentSource || _customExtent != null);


        public bool CanConfigureVersions => _hasVersionResults && !IsProcessing;


        public ICommand QueryVersionsCommand { get; }


        public ICommand DownloadCommand { get; }


        public ICommand CancelCommand { get; }


        public ICommand BrowseOutputFolderCommand { get; }


        public ICommand OpenVersionSelectionCommand { get; }


        public ICommand PickSpatialReferenceCommand { get; }


        public ICommand DrawExtentCommand { get; }


        public ICommand RefreshLayersCommand { get; }


        public ICommand ShowHelpCommand { get; }


        public event PropertyChangedEventHandler? PropertyChanged;

    }
}
