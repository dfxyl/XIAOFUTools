using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.IO.Compression;
using System.Xml;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Conversion.ExportToKml
{
    internal partial class ExportToKmlViewModel
    {

        public ObservableCollection<Layer> FeatureLayers
        {
            get => _featureLayers;
            set => SetProperty(ref _featureLayers, value);
        }

        public Layer SelectedInputLayer
        {
            get => _selectedInputLayer;
            set
            {
                SetProperty(ref _selectedInputLayer, value);
                NotifyPropertyChanged(nameof(CanProcess));
                // 当选择图层改变时，加载字段列表
                LoadGroupFields();
            }
        }

        public ObservableCollection<string> GroupFields
        {
            get => _groupFields;
            set => SetProperty(ref _groupFields, value);
        }

        public string SelectedGroupField
        {
            get => _selectedGroupField;
            set
            {
                SetProperty(ref _selectedGroupField, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        public ObservableCollection<string> ExportFormats
        {
            get => _exportFormats;
            set => SetProperty(ref _exportFormats, value);
        }

        public string SelectedExportFormat
        {
            get => _selectedExportFormat;
            set
            {
                SetProperty(ref _selectedExportFormat, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                SetProperty(ref _outputFolder, value);
                NotifyPropertyChanged(nameof(CanProcess));
            }
        }

        public bool EnableLabel
        {
            get => _enableLabel;
            set
            {
                SetProperty(ref _enableLabel, value);
                NotifyPropertyChanged(nameof(ShowLabelField));
            }
        }

        public bool ShowLabelField => EnableLabel;

        public ObservableCollection<string> LabelFields
        {
            get => _labelFields;
            set => SetProperty(ref _labelFields, value);
        }

        public string SelectedLabelField
        {
            get => _selectedLabelField;
            set => SetProperty(ref _selectedLabelField, value);
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(nameof(CanProcess));
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

        public bool CanProcess => !IsProcessing && 
                                  SelectedInputLayer != null && 
                                  !string.IsNullOrEmpty(OutputFolder) && 
                                  !string.IsNullOrEmpty(SelectedExportFormat) &&
                                  !string.IsNullOrEmpty(SelectedGroupField);

        public ICommand BrowseOutputFolderCommand { get; }
        public ICommand RunCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand RefreshLayersCommand { get; }
    }
}
