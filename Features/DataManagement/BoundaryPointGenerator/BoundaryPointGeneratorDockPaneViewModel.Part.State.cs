using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
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
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BoundaryPointGenerator
{
    internal partial class BoundaryPointGeneratorDockPaneViewModel
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
                // 当选择图层改变时，自动更新输出路径
                UpdateOutputPath();
                // 同步更新选择信息
                UpdateSelectionInfo();
            }
        }

        // 是否有选中图层
        public bool HasSelectedLayer => SelectedPolygonLayer != null;
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                // 使用通用工具规范化输出路径（自动识别 GDB/SHP，并补全缺省名称）
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_SZD" : "四至坐标点SZD";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPath, normalized);
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

        // 生成模式列表与当前选择（四向点/四角点）
        public ObservableCollection<string> GenerationModes { get; private set; } = new ObservableCollection<string>();
        public string SelectedGenerationMode
        {
            get => _selectedGenerationMode;
            set { SetProperty(ref _selectedGenerationMode, value); }
        }
        public bool UseSelection
        {
            get => _useSelection;
            set { SetProperty(ref _useSelection, value); }
        }
        public bool HasSelection
        {
            get => _hasSelection;
            set { SetProperty(ref _hasSelection, value); }
        }
        public int SelectedCount
        {
            get => _selectedCount;
            set { SetProperty(ref _selectedCount, value); }
        }
        public string SelectionInfoText
        {
            get => _selectionInfoText;
            set { SetProperty(ref _selectionInfoText, value); }
        }
        public ICommand BrowseOutputCommand
        {
            get
            {
                return _browseOutputCommand ?? (_browseOutputCommand = new RelayCommand(() =>
                {
                    try
                    {
                        var initialLocation = GetProjectGDBPath();
                        var picked = PathDialogUtils.PickSaveFeatureClassPath("选择输出位置", initialLocation);
                        if (!string.IsNullOrWhiteSpace(picked))
                            OutputPath = picked;
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
                        // 如果正在处理，则设置取消标志
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
                return _runCommand ?? (_runCommand = new RelayCommand(Execute, () => CanExecute()));
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
