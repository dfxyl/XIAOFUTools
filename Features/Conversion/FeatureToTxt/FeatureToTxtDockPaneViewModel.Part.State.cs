using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using Microsoft.Win32;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.FeatureToTxt
{
    internal partial class FeatureToTxtDockPaneViewModel
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
            }
        }
        
        // 是否可以处理
        public bool CanProcess => !IsProcessing && HasSelectedLayer && !string.IsNullOrEmpty(OutputPath);
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
                NotifyPropertyChanged(() => HasSelectedLayer);
                NotifyPropertyChanged(() => CanProcess);
                LoadFieldNames();
                // 切换图层后刷新选择信息
                UpdateSelectionInfo();
            }
        }

        // 是否有选中图层
        public bool HasSelectedLayer => SelectedPolygonLayer != null;
        public bool UseSelection
        {
            get => _useSelection;
            set => SetProperty(ref _useSelection, value);
        }
        public bool HasSelectionInLayer
        {
            get => _hasSelectionInLayer;
            set => SetProperty(ref _hasSelectionInLayer, value);
        }
        public int SelectedCount
        {
            get => _selectedCount;
            set => SetProperty(ref _selectedCount, value);
        }
        public string SelectionInfoText
        {
            get => _selectionInfoText;
            set => SetProperty(ref _selectionInfoText, value);
        }
        public string OutputPath
        {
            get => _outputPath;
            set
            {
                SetProperty(ref _outputPath, value);
                NotifyPropertyChanged(() => CanProcess);
            }
        }
        public ObservableCollection<FieldDisplayInfo> FieldInfos
        {
            get => _fieldInfos;
            set => SetProperty(ref _fieldInfos, value);
        }
        public ObservableCollection<OutputFieldItem> OutputFields
        {
            get => _outputFields;
            set
            {
                SetProperty(ref _outputFields, value);
                NotifyPropertyChanged(() => OutputFieldsCount);
            }
        }

        // 输出字段数量（用于界面显示）
        public int OutputFieldsCount => OutputFields?.Count ?? 0;
        public OutputFieldItem SelectedOutputField
        {
            get => _selectedOutputField;
            set => SetProperty(ref _selectedOutputField, value);
        }
        public bool OutputClosingPoint
        {
            get => _outputClosingPoint;
            set => SetProperty(ref _outputClosingPoint, value);
        }
        public bool InnerRingStartFromOne
        {
            get => _innerRingStartFromOne;
            set => SetProperty(ref _innerRingStartFromOne, value);
        }
        public bool ClosingPointContinueNumbering
        {
            get => _closingPointContinueNumbering;
            set => SetProperty(ref _closingPointContinueNumbering, value);
        }
        public bool ExportSeparately
        {
            get => _exportSeparately;
            set => SetProperty(ref _exportSeparately, value);
        }
        public bool EnableDetailedLogging
        {
            get => _enableDetailedLogging;
            set => SetProperty(ref _enableDetailedLogging, value);
        }
        public bool SwapXY
        {
            get => _swapXY;
            set => SetProperty(ref _swapXY, value);
        }
        public bool GroupByField
        {
            get => _groupByField;
            set
            {
                if (SetProperty(ref _groupByField, value) && value)
                {
                    // 勾选时，确保有可用字段并通知更新
                    if (GroupableFields == null || GroupableFields.Count == 0)
                    {
                        LogWarning("分组字段列表为空，请先选择图层");
                    }
                    else
                    {
                        LogInfo($"可用分组字段: {GroupableFields.Count} 个");
                    }
                    NotifyPropertyChanged(nameof(GroupableFields));
                }
            }
        }
        public string GroupByFieldName
        {
            get => _groupByFieldName;
            set => SetProperty(ref _groupByFieldName, value);
        }
        public string Prefix
        {
            get => _prefix;
            set => SetProperty(ref _prefix, value);
        }
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set => SetProperty(ref _decimalPlaces, value);
        }
        public ObservableCollection<string> TextFormats
        {
            get => _textFormats;
            set => SetProperty(ref _textFormats, value);
        }
        public string SelectedTextFormat
        {
            get => _selectedTextFormat;
            set => SetProperty(ref _selectedTextFormat, value);
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
        public ObservableCollection<HeaderConfig> HeaderConfigs
        {
            get => _headerConfigs;
            set => SetProperty(ref _headerConfigs, value);
        }
        public HeaderConfig SelectedHeaderConfig
        {
            get => _selectedHeaderConfig;
            set => SetProperty(ref _selectedHeaderConfig, value);
        }
        public ICommand RunCommand
        {
            get
            {
                return _runCommand ?? (_runCommand = new RelayCommand(() => ExecuteAsyncSafe(), () => CanProcess));
            }
        }
        public ICommand CancelCommand
        {
            get
            {
                return _cancelCommand ?? (_cancelCommand = new RelayCommand(() => Cancel(), () => IsProcessing));
            }
        }
        public ICommand BrowseOutputPathCommand
        {
            get
            {
                return _browseOutputPathCommand ?? (_browseOutputPathCommand = new RelayCommand(() => BrowseOutputPath()));
            }
        }
        public ICommand RefreshLayersCommand
        {
            get
            {
                return _refreshLayersCommand ?? (_refreshLayersCommand = new RelayCommand(() => RefreshLayers()));
            }
        }
        public ICommand RemoveOutputFieldCommand
        {
            get
            {
                return _removeOutputFieldCommand ?? (_removeOutputFieldCommand = new XIAOFUTools.Shared.Mvvm.RelayCommand<OutputFieldItem>((item) => RemoveOutputField(item)));
            }
        }
        public ICommand RemoveSelectedOutputFieldCommand
        {
            get
            {
                return _removeSelectedOutputFieldCommand ?? (_removeSelectedOutputFieldCommand = new RelayCommand(() =>
                {
                    if (SelectedOutputField != null)
                        RemoveOutputField(SelectedOutputField);
                }));
            }
        }
        public ICommand AddOutputFieldCommand
        {
            get
            {
                return _addOutputFieldCommand ?? (_addOutputFieldCommand = new XIAOFUTools.Shared.Mvvm.RelayCommand<string>((fieldName) => AddOutputField(fieldName)));
            }
        }
        public ICommand MoveUpCommand => _moveUpCommand ?? (_moveUpCommand = new RelayCommand(() => MoveSelectedOutputField(-1)));
        public ICommand MoveDownCommand => _moveDownCommand ?? (_moveDownCommand = new RelayCommand(() => MoveSelectedOutputField(1)));
        public ICommand ClearAllOutputFieldsCommand => _clearAllOutputFieldsCommand ?? (_clearAllOutputFieldsCommand = new RelayCommand(() =>
        {
            OutputFields?.Clear();
            NotifyPropertyChanged(() => OutputFieldsCount);
            StatusMessage = "已清空所有输出字段";
        }));
        public ICommand AddAllActualFieldsCommand => _addAllActualFieldsCommand ?? (_addAllActualFieldsCommand = new RelayCommand(() => AddAllActualFields()));
        public ICommand AddMappedFieldsCommand => _addMappedFieldsCommand ?? (_addMappedFieldsCommand = new RelayCommand(() => AddMappedFields()));
        public ICommand ResetDefaultFieldsCommand => _resetDefaultFieldsCommand ?? (_resetDefaultFieldsCommand = new RelayCommand(() => ResetDefaultOutputFields()));
        public ObservableCollection<string> AvailableFields
        {
            get => _availableFields;
            set => SetProperty(ref _availableFields, value);
        }
        public ObservableCollection<string> GroupableFields
        {
            get => _groupableFields;
            set => SetProperty(ref _groupableFields, value);
        }
        public ICommand ShowHelpCommand
        {
            get
            {
                return _showHelpCommand ?? (_showHelpCommand = new RelayCommand(() => ShowHelp()));
            }
        }
        public ICommand OpenFieldConfigCommand
        {
            get
            {
                return _openFieldConfigCommand ?? (_openFieldConfigCommand = new RelayCommand(() => OpenFieldConfigDialog()));
            }
        }
        public ICommand OpenHeaderConfigCommand
        {
            get
            {
                return _openHeaderConfigCommand ?? (_openHeaderConfigCommand = new RelayCommand(() => OpenHeaderConfigDialog()));
            }
        }
        public ICommand RefreshHeaderConfigsCommand
        {
            get
            {
                return _refreshHeaderConfigsCommand ?? (_refreshHeaderConfigsCommand = new RelayCommand(() => LoadHeaderConfigs()));
            }
        }
    }
}
