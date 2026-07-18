using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary
{
    internal partial class MultiOverlaySummaryDockPaneViewModel
    {
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
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

        public bool CanProcess => !IsProcessing &&
                                  SelectedMainLayer != null &&
                                  OverlayLayerItems?.Any(l => l.IsSelected) == true;
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }
        public FeatureLayer SelectedMainLayer
        {
            get => _selectedMainLayer;
            set
            {
                SetProperty(ref _selectedMainLayer, value);
                NotifyPropertyChanged(() => CanProcess);
                LoadMainLayerFields();
                UpdateOverlayLayerItems();
            }
        }
        public ObservableCollection<FieldSelectItem> MainLayerFields
        {
            get => _mainLayerFields;
            set => SetProperty(ref _mainLayerFields, value);
        }
        public FieldSelectItem SelectedUniqueField
        {
            get => _selectedUniqueField;
            set => SetProperty(ref _selectedUniqueField, value);
        }
        public ObservableCollection<OverlayLayerItem> OverlayLayerItems
        {
            get => _overlayLayerItems;
            set => SetProperty(ref _overlayLayerItems, value);
        }
        public ObservableCollection<string> AreaUnits
        {
            get => _areaUnits;
            set => SetProperty(ref _areaUnits, value);
        }
        public string SelectedAreaUnit
        {
            get => _selectedAreaUnit;
            set
            {
                SetProperty(ref _selectedAreaUnit, value);
                UpdateDefaultDecimalPlaces();
            }
        }
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set => SetProperty(ref _decimalPlaces, value);
        }
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
        public DataTable ResultTable
        {
            get => _resultTable;
            set => SetProperty(ref _resultTable, value);
        }

        public bool HasResult => ResultTable != null && ResultTable.Rows.Count > 0;
        public ICommand RunCommand => _runCommand ?? (_runCommand = new RelayCommand(async () => await ExecuteAsync(), () => CanProcess));
        public ICommand CancelCommand => _cancelCommand ?? (_cancelCommand = new RelayCommand(() => Cancel(), () => IsProcessing));
        public ICommand ShowHelpCommand => _showHelpCommand ?? (_showHelpCommand = new RelayCommand(() => ShowHelp()));
        public ICommand RefreshLayersCommand => _refreshLayersCommand ?? (_refreshLayersCommand = new RelayCommand(() => RefreshLayers()));
        public ICommand ExportCommand => _exportCommand ?? (_exportCommand = new RelayCommand(async () => await ShowExportOptionsAsync(), () => HasResult));
        public ICommand ShowResultCommand => _showResultCommand ?? (_showResultCommand = new RelayCommand(() => ShowResultWindow(), () => HasResult));
    }
}
