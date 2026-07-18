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
    /// <summary>
    /// 地图生成界址点线 ViewModel
    /// 在布局的图形图层上生成界址点、界址线、点号、边长标注
    /// </summary>
    internal partial class MapBoundaryPointLineGeneratorDockPaneViewModel : PropertyChangedBase
    {
        private dynamic _mapViewInitializedToken;
        private dynamic _activeMapViewChangedToken;
        private dynamic _mapSelectionChangedToken;
        private bool _isProcessing;

        // 面要素图层
        private ObservableCollection<FeatureLayer> _polygonLayers = new();

        private FeatureLayer _selectedPolygonLayer;

        // 唯一字段
        private ObservableCollection<string> _availableFields = new();

        private string _selectedUniqueField;

        // 布局列表（存储名称字符串）
        private ObservableCollection<string> _layoutNames = new();

        private string _selectedLayoutName;

        // 选择集相关
        private bool _useSelection = true;

        private bool _hasSelection;

        private int _selectedCount;

        // ========== 界址点设置 ==========
        private bool _enableBoundaryPoints = true;

        private double _boundaryPointSize = 6.0;

        // ========== 界址线设置 ==========
        private bool _enableBoundaryLines = false;

        private double _boundaryLineWidth = 1.0;

        // ========== 点号设置 ==========
        private bool _enablePointLabels = true;

        private string _pointLabelPrefix = "J";

        private string _pointLabelSuffix = "";

        private double _pointLabelDistance = 3.0;

        private double _pointLabelSize = 12.0;

        private string _pointLabelOverlapMode = "压盖隐藏";

        // ========== 边长标注设置 ==========
        private bool _enableEdgeLabels = false;

        private string _edgeLabelPrefix = "";

        private string _edgeLabelSuffix = "";

        private int _edgeLabelDecimal = 2;

        private bool _edgeLabelPadZeros = false;

        private double _edgeLabelDistance = 2.0;

        private double _edgeLabelSize = 10.0;

        private string _edgeLabelOverlapMode = "压盖隐藏";

        // 状态与日志
        private string _statusMessage = "准备就绪";

        private string _logContent = string.Empty;
        private readonly StringBuilder _logBuilder = new();

        private int _progress;

        private bool _isProgressIndeterminate;
        private ICommand _runCommand;

        private ICommand _refreshCommand;

        private ICommand _createAllTemplatesCommand;

        private ICommand _showHelpCommand;
        public MapBoundaryPointLineGeneratorDockPaneViewModel()
        {
            LoadPolygonLayers();
            LoadLayouts();

            // 订阅事件
            _mapViewInitializedToken = MapViewInitializedEvent.Subscribe((args) => { LoadPolygonLayers(); });
            _activeMapViewChangedToken = ActiveMapViewChangedEvent.Subscribe((args) => { LoadPolygonLayers(); });
            _mapSelectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
        }
    }
}
