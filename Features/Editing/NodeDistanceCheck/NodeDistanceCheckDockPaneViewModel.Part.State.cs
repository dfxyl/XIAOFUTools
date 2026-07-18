using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using ArcGIS.Desktop.Editing;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.NodeDistanceCheck
{
    internal partial class NodeDistanceCheckDockPaneViewModel
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
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // 是否可以处理
        public bool CanProcess
        {
            get
            {
                bool canProcess = !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrEmpty(OutputPath);
                System.Diagnostics.Debug.WriteLine($"CanProcess: {canProcess}, IsProcessing: {IsProcessing}, SelectedPolygonLayer: {SelectedPolygonLayer?.Name ?? "null"}, OutputPath: {OutputPath ?? "null"}");
                return canProcess;
            }
        }
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
                UpdateOutputPath();
                NotifyPropertyChanged(() => CanProcess);
                NotifyPropertyChanged(() => HasSelectedLayer);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
        public ObservableCollection<string> CheckOptions
        {
            get => _checkOptions;
            set => SetProperty(ref _checkOptions, value);
        }
        public string SelectedCheckOption
        {
            get => _selectedCheckOption;
            set => SetProperty(ref _selectedCheckOption, value);
        }
        public double CheckDistance
        {
            get => _checkDistance;
            set => SetProperty(ref _checkDistance, value);
        }
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                // 使用通用工具规范化输出路径（自动识别 GDB/SHP）
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_节点距离检查" : "节点距离检查结果";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPath, normalized);
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
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

        // 是否有选中图层
        public bool HasSelectedLayer => SelectedPolygonLayer != null;
        public List<string> SelectedFields
        {
            get => _selectedFields;
            set
            {
                _selectedFields = value ?? new List<string>();
                SetProperty(ref _selectedFields, value);
                NotifyPropertyChanged(() => SelectedFieldsDisplayText);
            }
        }

        // 选中字段的显示文本
        public string SelectedFieldsDisplayText
        {
            get
            {
                if (SelectedFields == null || SelectedFields.Count == 0)
                {
                    return "未选择字段";
                }
                return $"已选择 {SelectedFields.Count} 个字段";
            }
        }
        public ICommand BrowseOutputCommand
        {
            get
            {
                return _browseOutputCommand ?? (_browseOutputCommand = new RelayCommand(() =>
                {
                    try
                    {
                        var saveItemDialog = new SaveItemDialog
                        {
                            Title = "选择输出位置",
                            OverwritePrompt = true,
                            DefaultExt = "shp",
                            Filter = ItemFilters.FeatureClasses_All
                        };

                        var initialLocation = GetProjectGDBPath();
                        if (!string.IsNullOrEmpty(initialLocation))
                        {
                            saveItemDialog.InitialLocation = initialLocation;
                        }

                        bool? dialogResult = saveItemDialog.ShowDialog();
                        if (dialogResult == true)
                        {
                            OutputPath = saveItemDialog.FilePath;
                        }
                    }
                    catch (Exception ex)
                    {
                        PresentationServices.Dialogs.Show($"选择输出位置出错: {ex.Message}", "错误");
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
                        CancelRequested = true;
                        StatusMessage = "正在取消操作...";
                        LogWarning("用户请求取消操作");
                    }
                }, () => IsProcessing));
            }
        }
        public ICommand RunCommand
        {
            get
            {
                if (_runCommand == null)
                {
                    _runCommand = new RelayCommand(Execute, () => CanProcess);
                }
                return _runCommand;
            }
        }
        public ICommand ShowHelpCommand
        {
            get
            {
                return _showHelpCommand ?? (_showHelpCommand = new RelayCommand(() => ShowHelp()));
            }
        }
        public ICommand SelectFieldsCommand
        {
            get
            {
                return _selectFieldsCommand ?? (_selectFieldsCommand = new RelayCommand(() => SelectFields(), () => HasSelectedLayer));
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
