using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using System.Globalization;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.BatchLayerClip
{
    internal partial class BatchLayerClipViewModel
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
        public ObservableCollection<FeatureLayer> FeatureLayers
        {
            get => _featureLayers;
            set
            {
                SetProperty(ref _featureLayers, value);
            }
        }
        public FeatureLayer SelectedFeatureLayer
        {
            get => _selectedFeatureLayer;
            set
            {
                SetProperty(ref _selectedFeatureLayer, value);
                UpdateFieldNames();
                NotifyPropertyChanged(() => HasSelectedLayer);
            }
        }

        // 是否有选中图层
        public bool HasSelectedLayer => SelectedFeatureLayer != null;
        public ObservableCollection<string> FieldNames
        {
            get => _fieldNames;
            set
            {
                SetProperty(ref _fieldNames, value);
            }
        }
        public string SelectedField
        {
            get => _selectedField;
            set
            {
                SetProperty(ref _selectedField, value);
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
        public bool CreateSubFolder
        {
            get => _createSubFolder;
            set
            {
                SetProperty(ref _createSubFolder, value);
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
        public ICommand BrowseFolderCommand
        {
            get
            {
                return _browseFolderCommand ?? (_browseFolderCommand = new RelayCommand(() =>
                {
                    try
                    {
                        // OpenItemDialog必须在UI线程上创建和显示
                        // 使用ArcGIS Pro的OpenItemDialog选择文件夹
                        var initialLocation = Project.Current?.HomeFolderPath ?? OutputFolder;
                        if (!_fileStore.DirectoryExists(initialLocation))
                        {
                            initialLocation = GetProjectFolderPath();
                        }

                        var selectedPath = PresentationServices.Files.SelectFolder(
                            "选择输出文件夹",
                            initialLocation);
                        if (!string.IsNullOrWhiteSpace(selectedPath))
                        {
                            OutputFolder = selectedPath;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 错误也必须在UI线程显示
                        PresentationServices.Dialogs.Show($"选择文件夹出错: {ex.Message}", "错误");
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
                    // 设置取消标志停止当前处理
                    CancelRequested = true;
                    StatusMessage = "正在取消操作...";
                    LogWarning("用户请求取消操作");
                    _geoprocessingCancellationSource?.Cancel();
                }, () => IsProcessing));
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
        public ICommand RefreshLayersCommand
        {
            get
            {
                return _refreshLayersCommand ?? (_refreshLayersCommand = new RelayCommand(() => RefreshLayers()));
            }
        }
    }
}
