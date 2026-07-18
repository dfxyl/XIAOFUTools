using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Geometry;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Collections.Generic;

namespace XIAOFUTools.Features.DataManagement.BatchProjectionDefinition
{
    internal partial class BatchProjectionDefinitionViewModel
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
        public bool CanProcess => !IsProcessing && SelectedSpatialReference != null && LayerList.Any(l => l.IsSelected);
        public ObservableCollection<LayerProjectionInfo> LayerList
        {
            get => _layerList;
            set
            {
                SetProperty(ref _layerList, value);
            }
        }
        public SpatialReference SelectedSpatialReference
        {
            get => _selectedSpatialReference;
            set
            {
                SetProperty(ref _selectedSpatialReference, value);
                NotifyPropertyChanged(() => CanProcess);
                NotifyPropertyChanged(() => SelectedCoordinateSystemName);
            }
        }

        // 选择的坐标系名称
        public string SelectedCoordinateSystemName
        {
            get
            {
                if (SelectedSpatialReference == null)
                    return "未选择坐标系";
                return SelectedSpatialReference.Name;
            }
        }
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
        public string LogContent
        {
            get => _logContent;
            set
            {
                SetProperty(ref _logContent, value);
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
        public ICommand SelectAllCommand
        {
            get
            {
                return _selectAllCommand ?? (_selectAllCommand = new RelayCommand(() => SelectAll(), () => LayerList.Count > 0));
            }
        }
        public ICommand SelectNoneCommand
        {
            get
            {
                return _selectNoneCommand ?? (_selectNoneCommand = new RelayCommand(() => SelectNone(), () => LayerList.Count > 0));
            }
        }
        public ICommand SelectCoordinateSystemCommand
        {
            get
            {
                return _selectCoordinateSystemCommand ?? (_selectCoordinateSystemCommand = new RelayCommand(() => SelectCoordinateSystem()));
            }
        }
        public ICommand RunCommand
        {
            get
            {
                return _runCommand ?? (_runCommand = new RelayCommand(async () => await RunAsync(), () => CanProcess));
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
