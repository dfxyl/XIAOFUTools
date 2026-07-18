using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.Boundary.BoundaryPointLineGenerator
{
    internal partial class BoundaryPointLineGeneratorDockPaneViewModel
    {
        public bool CancelRequested { get => _cancelRequested; set => SetProperty(ref _cancelRequested, value); }
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
                // 更新命令可执行状态
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (_cancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && HasValidOutputPath;
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers; set => SetProperty(ref _polygonLayers, value);
        }
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                SetProperty(ref _selectedPolygonLayer, value);
                LoadAvailableFields();
                UpdateOutputName();
                UpdateOutputPaths();
                NotifyPropertyChanged(() => HasSelectedLayer);
                // 选中图层变化会影响 CanProcess
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
                // 同步更新选择信息
                UpdateSelectionInfo();
            }
        }
        public bool HasSelectedLayer => SelectedPolygonLayer != null;

        public ObservableCollection<string> OutputTypes { get; } = new() { "界址点(JZD)", "界址线(JZX)", "同时生成(JZD+JZX)" };
        public string SelectedOutputType
        {
            get => _selectedOutputType;
            set
            {
                SetProperty(ref _selectedOutputType, value);
                UpdateOutputName();
                UpdateOutputPaths();
                NotifyPropertyChanged(() => ShowJZDSettings);
                NotifyPropertyChanged(() => ShowJZXSettings);
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
        public ObservableCollection<string> AvailableFields { get => _availableFields; set => SetProperty(ref _availableFields, value); }
        public string SelectedCodeFieldName { get => _selectedCodeFieldName; set => SetProperty(ref _selectedCodeFieldName, value); }
        public string DefaultGdbPath
        {
            get => _defaultGdbPath;
            set
            {
                SetProperty(ref _defaultGdbPath, value);
                // 默认GDB路径变化会影响 CanProcess
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
        public string OutputPathJZD
        {
            get => _outputPathJZD;
            set
            {
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_JZD" : "JZD";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPathJZD, normalized);
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
        public string OutputPathJZX
        {
            get => _outputPathJZX;
            set
            {
                var defName = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_JZX" : "JZX";
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, defName);
                SetProperty(ref _outputPathJZX, normalized);
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private bool HasValidOutputPath
        {
            get
            {
                if (SelectedOutputType == null) return false;
                bool needJZD = SelectedOutputType.Contains("JZD");
                bool needJZX = SelectedOutputType.Contains("JZX");
                if (needJZD && string.IsNullOrWhiteSpace(OutputPathJZD)) return false;
                if (needJZX && string.IsNullOrWhiteSpace(OutputPathJZX)) return false;
                return true;
            }
        }
        public string OutputFeatureClassName { get => _outputFeatureClassName; set => SetProperty(ref _outputFeatureClassName, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public string LogContent { get => _logContent; set => SetProperty(ref _logContent, value); }
        public int Progress { get => _progress; set => SetProperty(ref _progress, value); }
        public bool IsProgressIndeterminate { get => _isProgressIndeterminate; set => SetProperty(ref _isProgressIndeterminate, value); }
        public int JZXLengthDecimals
        {
            get => _jzxLengthDecimals;
            set
            {
                var v = Math.Max(0, Math.Min(6, value));
                SetProperty(ref _jzxLengthDecimals, v);
            }
        }
        public string JZXYSDM { get => _jzxYSDM; set => SetProperty(ref _jzxYSDM, value); }
        public string JZDYSDM { get => _jzdYSDM; set => SetProperty(ref _jzdYSDM, value); }
        public string JZDHPrefix { get => _jzdhPrefix; set => SetProperty(ref _jzdhPrefix, value); }

        // 设置项可见性：根据选择的输出类型自动显示
        public bool ShowJZDSettings => SelectedOutputType != null && SelectedOutputType.Contains("JZD");
        public bool ShowJZXSettings => SelectedOutputType != null && SelectedOutputType.Contains("JZX");
        public ICommand RunCommand => _runCommand ??= new RelayCommand(Execute, () => CanProcess);
        public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(() => { if (IsProcessing) { CancelRequested = true; StatusMessage = "正在取消..."; LogWarning("用户请求取消"); } }, () => IsProcessing);
        public ICommand RefreshLayersCommand => _refreshLayersCommand ??= new RelayCommand(() => LoadPolygonLayers());
        public ICommand ShowHelpCommand => _showHelpCommand ??= new RelayCommand(ShowHelp);
        public ICommand BrowseOutputJZDCommand => _browseOutputJZDCommand ??= new RelayCommand(() =>
        {
            try
            {
                var initial = !string.IsNullOrWhiteSpace(OutputPathJZD) ? OutputPathJZD : PathDialogUtils.GetProjectDefaultGdb();
                var picked = PathDialogUtils.PickSaveFeatureClassPath("选择 JZD 输出位置", initial);
                if (!string.IsNullOrWhiteSpace(picked)) OutputPathJZD = picked;
            }
            catch (Exception ex) { PresentationServices.Dialogs.Show($"选择 JZD 输出位置失败: {ex.Message}", "错误"); }
        });
        public ICommand BrowseOutputJZXCommand => _browseOutputJZXCommand ??= new RelayCommand(() =>
        {
            try
            {
                var initial = !string.IsNullOrWhiteSpace(OutputPathJZX) ? OutputPathJZX : PathDialogUtils.GetProjectDefaultGdb();
                var picked = PathDialogUtils.PickSaveFeatureClassPath("选择 JZX 输出位置", initial);
                if (!string.IsNullOrWhiteSpace(picked)) OutputPathJZX = picked;
            }
            catch (Exception ex) { PresentationServices.Dialogs.Show($"选择 JZX 输出位置失败: {ex.Message}", "错误"); }
        });
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
    }
}
