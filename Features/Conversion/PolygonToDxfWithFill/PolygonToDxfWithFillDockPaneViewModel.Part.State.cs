using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Conversion.PolygonToDxfWithFill
{
    internal partial class PolygonToDxfWithFillDockPaneViewModel
    {
        public ObservableCollection<FeatureLayer> PolygonLayers { get; }

        // 命名字段选项（使用复选框多选）
        public ObservableCollection<CadFieldOption> NamingFields { get; }
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
        public FeatureLayer SelectedPolygonLayer
        {
            get => _selectedPolygonLayer;
            set
            {
                if (SetProperty(ref _selectedPolygonLayer, value))
                {
                    try
                    {
                        // 仅当当前未指定输出路径时，按所选图层名补全默认输出路径
                        if (string.IsNullOrWhiteSpace(OutputPath))
                        {
                            var name = value != null ? SanitizeFileName(value.Name) : "output";
                            var defaultPath = _outputPathResolver.CreateDefaultOutputPath(name + ".dxf");
                            if (!string.IsNullOrWhiteSpace(defaultPath))
                            {
                                OutputPath = defaultPath;
                            }
                        }

                        // 切换图层时，刷新命名字段列表
                        LoadNamingFieldsAsync(value);
                    }
                    catch { }
                    NotifyPropertyChanged(nameof(CanProcess));
                    // 立刻刷新按钮可用性
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
                    // 立刻刷新按钮可用性
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public ObservableCollection<DxfVersionOption> DxfVersions { get; }
        public DxfVersionOption SelectedDxfVersion
        {
            get => _selectedDxfVersion;
            set => SetProperty(ref _selectedDxfVersion, value);
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
            set => SetProperty(ref _lineWidth, Math.Max(0.0, value));
        }
        public int HatchTransparency
        {
            get => _hatchTransparency;
            set => SetProperty(ref _hatchTransparency, Math.Clamp(value, 0, 100));
        }
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrWhiteSpace(OutputPath);
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetProperty(ref _isProcessing, value))
                {
                    NotifyPropertyChanged(nameof(CanProcess));
                    // 立刻刷新按钮可用性
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
        public ICommand RefreshLayersCommand { get; }
        public ICommand BrowseOutputPathCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand RunCommand { get; }
    }
}
