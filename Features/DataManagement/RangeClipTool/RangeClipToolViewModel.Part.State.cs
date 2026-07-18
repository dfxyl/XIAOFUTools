using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Shared;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.DataManagement.RangeClipTool
{
    internal partial class RangeClipToolViewModel
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
        public bool CanProcess => !IsProcessing;
        public ObservableCollection<FeatureLayer> RangeLayers
        {
            get => _rangeLayers;
            set
            {
                SetProperty(ref _rangeLayers, value);
            }
        }
        public FeatureLayer SelectedRangeLayer
        {
            get => _selectedRangeLayer;
            set
            {
                SetProperty(ref _selectedRangeLayer, value);
                NotifyPropertyChanged(() => HasSelectedRangeLayer);
                LoadRangeFields();
            }
        }

        // 是否有选中的范围图层
        public bool HasSelectedRangeLayer => SelectedRangeLayer != null;
        public ObservableCollection<string> RangeFieldNames
        {
            get => _rangeFieldNames;
            set
            {
                SetProperty(ref _rangeFieldNames, value);
            }
        }
        public string SelectedRangeField
        {
            get => _selectedRangeField;
            set
            {
                SetProperty(ref _selectedRangeField, value);
            }
        }
        public ObservableCollection<ClipLayerItem> ClipLayerItems
        {
            get => _clipLayerItems;
            set
            {
                SetProperty(ref _clipLayerItems, value);
            }
        }
        public bool CreateSubFolder
        {
            get => _createSubFolder;
            set
            {
                SetProperty(ref _createSubFolder, value);
            }
        }
        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                SetProperty(ref _outputFolder, value);
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
        public ICommand SelectAllLayersCommand
        {
            get
            {
                return _selectAllLayersCommand ?? (_selectAllLayersCommand = new RelayCommand(() =>
                {
                    foreach (var item in ClipLayerItems)
                    {
                        item.IsSelected = true;
                    }
                }));
            }
        }
        public ICommand InvertSelectionCommand
        {
            get
            {
                return _invertSelectionCommand ?? (_invertSelectionCommand = new RelayCommand(() =>
                {
                    foreach (var item in ClipLayerItems)
                    {
                        item.IsSelected = !item.IsSelected;
                    }
                }));
            }
        }
        public ICommand CancelCommand
        {
            get
            {
                return _cancelCommand ?? (_cancelCommand = new RelayCommand(() =>
                {
                    if (IsProcessing)
                    {
                        // 如果正在处理，则设置取消标志
                        CancelRequested = true;
                        StatusMessage = "正在取消操作...";
                        LogWarning("用户请求取消操作");
                    }
                    else
                    {
                        // 如果没有正在处理的操作，则关闭窗口
                        PresentationServices.Windows.CloseProWindow("根据范围批量裁剪要素图层");
                    }
                }));
            }
        }
        public ICommand RunCommand
        {
            get
            {
                return _runCommand ?? (_runCommand = new RelayCommand(Execute, () => CanExecute() && CanProcess));
            }
        }
        public ICommand ShowHelpCommand
        {
            get
            {
                return _showHelpCommand ?? (_showHelpCommand = new RelayCommand(() => ShowHelp()));
            }
        }
        public ICommand BrowseFolderCommand
        {
            get
            {
                return _browseFolderCommand ?? (_browseFolderCommand = new RelayCommand(() => BrowseFolder()));
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
