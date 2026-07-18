using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.IntersectSummary
{
    internal partial class IntersectSummaryDockPaneViewModel
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

        // 是否可以处理（区域字段可选，类字段必选）
        public bool CanProcess => !IsProcessing && 
                                  SelectedRedlineLayer != null && 
                                  SelectedClassLayer != null &&
                                  ClassFields?.Any(f => f.IsSelected) == true;
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }
        public FeatureLayer SelectedRedlineLayer
        {
            get => _selectedRedlineLayer;
            set
            {
                SetProperty(ref _selectedRedlineLayer, value);
                NotifyPropertyChanged(() => HasSelectedRedlineLayer);
                NotifyPropertyChanged(() => CanProcess);
                LoadRegionFields();
            }
        }

        // 是否有选中红线图层
        public bool HasSelectedRedlineLayer => SelectedRedlineLayer != null;
        public FeatureLayer SelectedClassLayer
        {
            get => _selectedClassLayer;
            set
            {
                SetProperty(ref _selectedClassLayer, value);
                NotifyPropertyChanged(() => HasSelectedClassLayer);
                NotifyPropertyChanged(() => CanProcess);
                LoadClassFields();
            }
        }

        // 是否有选中类要素图层
        public bool HasSelectedClassLayer => SelectedClassLayer != null;
        public ObservableCollection<FieldSelectItem> RegionFields
        {
            get => _regionFields;
            set => SetProperty(ref _regionFields, value);
        }
        public ObservableCollection<FieldSelectItem> ClassFields
        {
            get => _classFields;
            set => SetProperty(ref _classFields, value);
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

        // 是否有结果
        public bool HasResult => ResultTable != null && ResultTable.Rows.Count > 0;
        public ICommand RunCommand
        {
            get
            {
                return _runCommand ?? (_runCommand = new RelayCommand(async () => await ExecuteAsync(), () => CanProcess));
            }
        }
        public ICommand CancelCommand
        {
            get
            {
                return _cancelCommand ?? (_cancelCommand = new RelayCommand(() => Cancel(), () => IsProcessing));
            }
        }
        public ICommand ShowHelpCommand
        {
            get
            {
                return _showHelpCommand ?? (_showHelpCommand = new RelayCommand(() => ShowHelp()));
            }
        }
        public ICommand RefreshLayersCommand
        {
            get
            {
                return _refreshLayersCommand ?? (_refreshLayersCommand = new RelayCommand(() => RefreshLayers()));
            }
        }
        public ICommand ExportCommand
        {
            get
            {
                return _exportCommand ?? (_exportCommand = new RelayCommand(() => ExportResult(), () => HasResult));
            }
        }
        public ICommand ShowResultCommand
        {
            get
            {
                return _showResultCommand ?? (_showResultCommand = new RelayCommand(() => ShowResultWindow(), () => HasResult));
            }
        }
    }
}
