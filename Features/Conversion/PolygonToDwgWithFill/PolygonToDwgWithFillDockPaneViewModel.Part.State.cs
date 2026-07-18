using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill
{
    internal partial class PolygonToDwgWithFillDockPaneViewModel
    {
        public ObservableCollection<FeatureLayer> PolygonLayers { get; }
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                if (SetProperty(ref _selectedPolygonLayer, value))
                {
                    NotifyPropertyChanged(nameof(CanProcess));
                    StatusMessage = value == null ? "未选择图层" : $"已选择图层: {value.Name}";
                    // 载入可用字段供命名多选
                    if (value != null) LoadNamingFieldsAsync(value);
                }
            }
        }
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                if (SetProperty(ref _outputPath, value))
                {
                    NotifyPropertyChanged(nameof(CanProcess));
                }
            }
        }

        // DWG 版本选择
        public ObservableCollection<DwgVersionOption> DwgVersions { get; }
        public DwgVersionOption SelectedDwgVersion
        {
            get => _selectedDwgVersion;
            set => SetProperty(ref _selectedDwgVersion, value);
        }
        public bool ExportBoundary
        {
            get => _exportBoundary;
            set => SetProperty(ref _exportBoundary, value);
        }
        public bool ExportHatch
        {
            get => _exportHatch;
            set => SetProperty(ref _exportHatch, value);
        }
        public double LineWidth
        {
            get => _lineWidth;
            set => SetProperty(ref _lineWidth, value);
        }
        public int HatchTransparency
        {
            get => _hatchTransparency;
            set => SetProperty(ref _hatchTransparency, Math.Clamp(value, 0, 90)); // DWG常用0-90
        }
        public bool UseFieldNaming
        {
            get => _useFieldNaming;
            set => SetProperty(ref _useFieldNaming, value);
        }
        public string FieldNamingSeparator
        {
            get => _fieldNamingSeparator;
            set => SetProperty(ref _fieldNamingSeparator, value);
        }

        // 图层命名：可选字段集合（UI 多选）
        public ObservableCollection<CadFieldOption> NamingFields { get; }
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
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    NotifyPropertyChanged(nameof(CanProcess));
                }
            }
        }
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
        }
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrWhiteSpace(OutputPath);
        public ICommand RefreshLayersCommand { get; }
        public ICommand BrowseOutputPathCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand RunCommand { get; }
    }
}
