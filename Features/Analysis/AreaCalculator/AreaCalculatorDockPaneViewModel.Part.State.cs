using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.AreaCalculator
{
    internal partial class AreaCalculatorDockPaneViewModel
    {
        public bool CancelRequested
        {
            get => _cancelRequested;
            set
            {
                SetProperty(ref _cancelRequested, value);
            }
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
        
        // 是否可以处理
        public bool CanProcess => !IsProcessing && HasSelectedLayer && !string.IsNullOrEmpty(SelectedFieldName);
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set
            {
                SetProperty(ref _polygonLayers, value);
            }
        }
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                NotifyPropertyChanged(() => HasSelectedLayer);
                NotifyPropertyChanged(() => CanProcess);
                LoadFieldNames();
            }
        }

        // 是否有选中图层
        public bool HasSelectedLayer => SelectedPolygonLayer != null;
        public string PreferredLayerName
        {
            get => _preferredLayerName;
            set => SetProperty(ref _preferredLayerName, value);
        }
        public string PreferredLayerUri
        {
            get => _preferredLayerUri;
            set => SetProperty(ref _preferredLayerUri, value);
        }
        public ObservableCollection<FieldDisplayInfo> FieldInfos
        {
            get => _fieldInfos;
            set
            {
                SetProperty(ref _fieldInfos, value);
            }
        }
        public FieldDisplayInfo SelectedFieldInfo
        {
            get => _selectedFieldInfo;
            set
            {
                SetProperty(ref _selectedFieldInfo, value);
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        // 选中的字段名称（用于内部处理）
        public string SelectedFieldName => SelectedFieldInfo?.FieldName;
        public ObservableCollection<string> AreaUnits
        {
            get => _areaUnits;
            set
            {
                SetProperty(ref _areaUnits, value);
            }
        }
        public string SelectedAreaUnit
        {
            get => _selectedAreaUnit;
            set
            {
                if (SetProperty(ref _selectedAreaUnit, value))
                {
                    DecimalPlaces = GetDefaultDecimalPlacesByUnit(value);
                }
            }
        }
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set
            {
                var normalizedValue = Math.Clamp(value, 0, 10);
                SetProperty(ref _decimalPlaces, normalizedValue);
            }
        }
        public ObservableCollection<string> AreaTypes
        {
            get => _areaTypes;
            set
            {
                SetProperty(ref _areaTypes, value);
            }
        }
        public string SelectedAreaType
        {
            get => _selectedAreaType;
            set
            {
                SetProperty(ref _selectedAreaType, value);
                NotifyPropertyChanged(() => IsEllipsoidSelected);
            }
        }

        // 是否选择了椭球面积
        public bool IsEllipsoidSelected => SelectedAreaType == "椭球";
        public int Progress
        {
            get => _progress;
            set
            {
                SetProperty(ref _progress, value);
            }
        }
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set
            {
                SetProperty(ref _isProgressIndeterminate, value);
            }
        }
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                SetProperty(ref _statusMessage, value);
            }
        }
        public string LogContent
        {
            get => _logContent;
            set
            {
                SetProperty(ref _logContent, value);
            }
        }
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
    }
}
