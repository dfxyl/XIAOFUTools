using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BatchGeometryRepair
{
    internal partial class BatchGeometryRepairViewModel
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
        public bool CanProcess => !IsProcessing && LayerList.Any(l => l.IsSelected);
        public ObservableCollection<LayerGeometryInfo> LayerList
        {
            get => _layerList;
            set
            {
                SetProperty(ref _layerList, value);
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
