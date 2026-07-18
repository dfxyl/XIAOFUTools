using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.CIM;
using XIAOFUTools.Shared;
using ArcGIS.Desktop.Framework.Dialogs;

namespace XIAOFUTools.Features.DataManagement.MapSheetsSmall
{
    internal partial class GenerateSmallMapSheetsDockPaneViewModel
    {
        public ObservableCollection<FeatureLayer> PolygonLayers { get; private set; } = new ObservableCollection<FeatureLayer>();
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set { SetProperty(ref _selectedPolygonLayer, value); NotifyPropertyChanged(() => CanRun); }
        }

        public ObservableCollection<string> ScaleNames { get; } = new ObservableCollection<string>
        {
            "100万","50万","25万","10万","5万","2.5万","1万","5千"
        };
        public string SelectedScaleName { get => _selectedScaleName; set => SetProperty(ref _selectedScaleName, value); }
        public string OutputFeatureClassPath
        {
            get => _outputFeatureClassPath;
            set
            {
                var normalized = OutputDatasetUtils.NormalizeOutputPath(value, "MapSheets");
                SetProperty(ref _outputFeatureClassPath, normalized);
                NotifyPropertyChanged(() => CanRun);
            }
        }
        public bool UseLayerExtent { get => _useLayerExtent; set => SetProperty(ref _useLayerExtent, value); }
        public bool IsProcessing { get => _isProcessing; set { SetProperty(ref _isProcessing, value); NotifyPropertyChanged(() => CanRun); } }
        public string LogContent { get => _logContent; set => SetProperty(ref _logContent, value); }
        public string DrawnExtentText { get => _drawnExtent == null ? "未选择" : $"X:[{_drawnExtent.XMin:F4},{_drawnExtent.XMax:F4}] Y:[{_drawnExtent.YMin:F4},{_drawnExtent.YMax:F4}]"; }
        public bool HasDrawnExtent => _drawnExtent != null && !_drawnExtent.IsEmpty;
        public string SelectedRangeMode
        {
            get => _selectedRangeMode;
            set
            {
                SetProperty(ref _selectedRangeMode, value);
                NotifyPropertyChanged(() => IsLayerMode);
                NotifyPropertyChanged(() => IsMapMode);
                NotifyPropertyChanged(() => IsCustomMode);
                NotifyPropertyChanged(() => CanRun);
            }
        }
        public bool IsLayerMode { get => SelectedRangeMode == "Layer"; set { if (value) SelectedRangeMode = "Layer"; } }
        public bool IsMapMode { get => SelectedRangeMode == "Map"; set { if (value) SelectedRangeMode = "Map"; } }
        public bool IsCustomMode { get => SelectedRangeMode == "Custom"; set { if (value) SelectedRangeMode = "Custom"; } }

        public bool CanRun =>
            !IsProcessing &&
            !string.IsNullOrWhiteSpace(OutputFeatureClassPath) &&
            (
                (IsLayerMode && SelectedPolygonLayer != null) ||
                (IsCustomMode && HasDrawnExtent) ||
                IsMapMode
            );

        public ICommand RefreshLayersCommand => new RelayCommand(() => RefreshLayers());
        public ICommand BrowseOutputPathCommand => new RelayCommand(() => BrowseOutputPath());
        public ICommand RunCommand => new RelayCommand(async () => await RunAsync(), () => CanRun);
        public ICommand DrawExtentCommand => new RelayCommand(async () => await FrameworkApplication.SetCurrentToolAsync("XIAOFUTools_CustomExtentTool"));
        public ICommand ClearExtentCommand => new RelayCommand(() => { _drawnExtent = null; NotifyPropertyChanged(() => DrawnExtentText); NotifyPropertyChanged(() => CanRun); });
        public ICommand CancelCommand => new RelayCommand(() => { /* 可扩展取消逻辑 */ }, () => IsProcessing);
    }
}
