using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary
{
    public class FieldSelectItem : PropertyChangedBase
    {
        public string FieldName { get; set; }
        public string Alias { get; set; }
        public string FieldType { get; set; }

        public string DisplayText => string.IsNullOrEmpty(Alias) || Alias == FieldName
            ? $"{FieldName}（{FieldType}）"
            : $"{FieldName}（{Alias}）（{FieldType}）";

        public override string ToString() => DisplayText;
    }

    public class OverlayLayerItem : PropertyChangedBase
    {
        public FeatureLayer Layer { get; set; }
        public string LayerName => Layer?.Name ?? "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    /// <summary>
    /// 交集几何结果项
    /// </summary>
    public class IntersectGeometryItem
    {
        public string GroupKey { get; set; }
        public string OverlayLayerName { get; set; }
        public Polygon Geometry { get; set; }
        public double Area { get; set; }
    }

    internal partial class MultiOverlaySummaryDockPaneViewModel : PropertyChangedBase
    {
        private readonly IMultiOverlaySummaryDialogService _dialogService = new MultiOverlaySummaryDialogService();

        private bool _cancelRequested = false;

        private bool _isProcessing = false;

        private ObservableCollection<FeatureLayer> _polygonLayers;

        private FeatureLayer _selectedMainLayer;

        private ObservableCollection<FieldSelectItem> _mainLayerFields;

        private FieldSelectItem _selectedUniqueField;

        private ObservableCollection<OverlayLayerItem> _overlayLayerItems;

        private ObservableCollection<string> _areaUnits;

        private string _selectedAreaUnit;

        private int _decimalPlaces = 2;

        private int _progress = 0;

        private bool _isProgressIndeterminate = false;

        private string _statusMessage = "请选择图层。";

        private string _logContent = "";

        private DataTable _resultTable;

        // 保存交集几何用于导出SHP
        private List<IntersectGeometryItem> _intersectGeometries;
        private SpatialReference _spatialReference;
        private string _mainLayerName;

        private ICommand _runCommand;

        private ICommand _cancelCommand;

        private ICommand _showHelpCommand;

        private ICommand _refreshLayersCommand;

        private ICommand _exportCommand;

        private ICommand _showResultCommand;

        public MultiOverlaySummaryDockPaneViewModel()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            MainLayerFields = new ObservableCollection<FieldSelectItem>();
            OverlayLayerItems = new ObservableCollection<OverlayLayerItem>();
            AreaUnits = new ObservableCollection<string> { "平方米", "公顷", "亩" };
            SelectedAreaUnit = "平方米";
            _intersectGeometries = new List<IntersectGeometryItem>();
            LoadPolygonLayers();
        }
    }
}
