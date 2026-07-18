using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.Boundary.MapBoundaryPointLineGenerator
{
    internal partial class MapBoundaryPointLineGeneratorDockPaneViewModel
    {
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
        public bool CanProcess => !IsProcessing && SelectedPolygonLayer != null && !string.IsNullOrEmpty(SelectedLayoutName);
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
                LoadAvailableFields();
                UpdateSelectionInfo();
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
        public ObservableCollection<string> AvailableFields
        {
            get => _availableFields;
            set => SetProperty(ref _availableFields, value);
        }
        public string SelectedUniqueField
        {
            get => _selectedUniqueField;
            set => SetProperty(ref _selectedUniqueField, value);
        }
        public ObservableCollection<string> LayoutNames
        {
            get => _layoutNames;
            set => SetProperty(ref _layoutNames, value);
        }
        public string SelectedLayoutName
        {
            get => _selectedLayoutName;
            set
            {
                SetProperty(ref _selectedLayoutName, value);
                NotifyPropertyChanged(() => CanProcess);
                (_runCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
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
        public bool EnableBoundaryPoints
        {
            get => _enableBoundaryPoints;
            set => SetProperty(ref _enableBoundaryPoints, value);
        }
        public double BoundaryPointSize
        {
            get => _boundaryPointSize;
            set => SetProperty(ref _boundaryPointSize, value);
        }
        public bool EnableBoundaryLines
        {
            get => _enableBoundaryLines;
            set => SetProperty(ref _enableBoundaryLines, value);
        }
        public double BoundaryLineWidth
        {
            get => _boundaryLineWidth;
            set => SetProperty(ref _boundaryLineWidth, value);
        }
        public bool EnablePointLabels
        {
            get => _enablePointLabels;
            set => SetProperty(ref _enablePointLabels, value);
        }
        public string PointLabelPrefix
        {
            get => _pointLabelPrefix;
            set => SetProperty(ref _pointLabelPrefix, value);
        }
        public string PointLabelSuffix
        {
            get => _pointLabelSuffix;
            set => SetProperty(ref _pointLabelSuffix, value);
        }
        public double PointLabelDistance
        {
            get => _pointLabelDistance;
            set => SetProperty(ref _pointLabelDistance, value);
        }
        public double PointLabelSize
        {
            get => _pointLabelSize;
            set => SetProperty(ref _pointLabelSize, value);
        }

        public ObservableCollection<string> OverlapModes { get; } = new() { "压盖隐藏", "压盖避让" };
        public string PointLabelOverlapMode
        {
            get => _pointLabelOverlapMode;
            set => SetProperty(ref _pointLabelOverlapMode, value);
        }
        public bool EnableEdgeLabels
        {
            get => _enableEdgeLabels;
            set => SetProperty(ref _enableEdgeLabels, value);
        }
        public string EdgeLabelPrefix
        {
            get => _edgeLabelPrefix;
            set => SetProperty(ref _edgeLabelPrefix, value);
        }
        public string EdgeLabelSuffix
        {
            get => _edgeLabelSuffix;
            set => SetProperty(ref _edgeLabelSuffix, value);
        }
        public int EdgeLabelDecimal
        {
            get => _edgeLabelDecimal;
            set => SetProperty(ref _edgeLabelDecimal, Math.Max(0, Math.Min(6, value)));
        }
        public bool EdgeLabelPadZeros
        {
            get => _edgeLabelPadZeros;
            set => SetProperty(ref _edgeLabelPadZeros, value);
        }
        public double EdgeLabelDistance
        {
            get => _edgeLabelDistance;
            set => SetProperty(ref _edgeLabelDistance, value);
        }
        public double EdgeLabelSize
        {
            get => _edgeLabelSize;
            set => SetProperty(ref _edgeLabelSize, value);
        }
        public string EdgeLabelOverlapMode
        {
            get => _edgeLabelOverlapMode;
            set => SetProperty(ref _edgeLabelOverlapMode, value);
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
        public ICommand RunCommand => _runCommand ??= new RelayCommand(Execute, () => CanProcess);
        public ICommand RefreshCommand => _refreshCommand ??= new RelayCommand(RefreshAll);
        public ICommand CreateAllTemplatesCommand => _createAllTemplatesCommand ??= new RelayCommand(CreateAllTemplates);
        public ICommand ShowHelpCommand => _showHelpCommand ??= new RelayCommand(ShowHelp);
    }
}
