using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ProtocolLineExtract
{
    internal partial class ProtocolLineExtractViewModel
    {
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
                UpdateDefaultOutputPath();
                UpdateSelectionInfo();
                NotifyPropertyChanged(() => CanProcess);
                NotifyPropertyChanged(() => HasSelectedLayer);
            }
        }

        public bool HasSelectedLayer => SelectedPolygonLayer != null;
        public string OutputPath
        {
            get => _outputPath;
            set => SetProperty(ref _outputPath, value);
        }
        public bool MergeLines
        {
            get => _mergeLines;
            set => SetProperty(ref _mergeLines, value);
        }
        public bool UseSelection
        {
            get => _useSelection;
            set => SetProperty(ref _useSelection, value);
        }
        public bool HasSelection
        {
            get => _hasSelection;
            set => SetProperty(ref _hasSelection, value);
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
        public List<string> SelectedFields
        {
            get => _selectedFields;
            set
            {
                var newVal = value ?? new List<string>();
                SetProperty(ref _selectedFields, newVal);
                NotifyPropertyChanged(() => SelectedFieldsDisplayText);
            }
        }

        public string SelectedFieldsDisplayText
        {
            get
            {
                if (SelectedFields == null || SelectedFields.Count == 0)
                    return "未选择字段";
                return $"已选择 {SelectedFields.Count} 个字段";
            }
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
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrWhiteSpace(OutputPath);
        public ICommand RefreshLayersCommand => new RelayCommand(RefreshLayers);
        public ICommand BrowseOutputCommand => new RelayCommand(BrowseOutput);
        public ICommand ShowHelpCommand => new RelayCommand(ShowHelp);
        public ICommand CancelCommand => new RelayCommand(() => _cts?.Cancel(), () => IsProcessing);
        public ICommand RunCommand => new RelayCommand(async () => await RunAsync(), () => CanProcess);
        public ICommand SelectFieldsCommand => new RelayCommand(SelectFields, () => SelectedPolygonLayer != null);
    }
}
