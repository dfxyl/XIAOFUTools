using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Analysis.ExtractPolygonHoles
{
    internal partial class ExtractPolygonHolesViewModel
    {

        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }

        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                UpdateDefaultOutputPath();
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        public string OutputPath
        {
            get => _outputPath;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value)
                    ? value
                    : OutputDatasetUtils.NormalizeOutputPath(value, GetDefaultOutputName());

                SetProperty(ref _outputPath, normalized);
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        public bool CreateMultipartOutput
        {
            get => _createMultipartOutput;
            set => SetProperty(ref _createMultipartOutput, value);
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

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrWhiteSpace(OutputPath);

        public ICommand RefreshLayersCommand => new RelayCommand(RefreshLayers);
        public ICommand BrowseOutputCommand => new RelayCommand(BrowseOutput);
        public ICommand ShowHelpCommand => new RelayCommand(ShowHelp);
        public ICommand CancelCommand => new RelayCommand(() => _cts?.Cancel(), () => IsProcessing);
        public ICommand RunCommand => new RelayCommand(async () => await RunAsync(), () => CanProcess);
    }
}
