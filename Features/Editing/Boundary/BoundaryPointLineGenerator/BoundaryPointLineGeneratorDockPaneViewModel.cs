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
    /// <summary>
    /// 界址点线生成 ViewModel
    /// - 选择面图层
    /// - 选择生成类型（点/线）
    /// - 输出到默认工程GDB，命名为：<图层名>_JZD 或 <图层名>_JZX
    /// - 字段结构参考 TD/T 1066-2021（按截图常用字段）
    /// </summary>
    internal partial class BoundaryPointLineGeneratorDockPaneViewModel : PropertyChangedBase
    {
        private dynamic _mapViewInitializedToken;
        private dynamic _activeMapViewChangedToken;
        private dynamic _mapSelectionChangedToken;
        private bool _cancelRequested;

        private bool _isProcessing;

        private ObservableCollection<FeatureLayer> _polygonLayers = new();

        private FeatureLayer _selectedPolygonLayer;

        private string _selectedOutputType = "界址点(JZD)";

        // 宗地/宗海代码来源字段选择
        private ObservableCollection<string> _availableFields = new();

        private string _selectedCodeFieldName;

        private string _defaultGdbPath;

        // 自定义输出路径（根据类型显示对应组件）
        private string _outputPathJZD;

        private string _outputPathJZX;

        private string _outputFeatureClassName;

        private string _statusMessage = "准备就绪";

        private string _logContent = string.Empty;
        private readonly StringBuilder _logBuilder = new();

        private int _progress;
        private bool _isProgressIndeterminate;
        
        // JZX：长度小数位设置
        private int _jzxLengthDecimals = 2;

        // JZX：YSDM 设置
        private string _jzxYSDM = string.Empty;

        // JZD：YSDM 设置
        private string _jzdYSDM = string.Empty;

        // JZD：JZDH 前缀
        private string _jzdhPrefix = string.Empty;
        private ICommand _runCommand;

        private ICommand _cancelCommand;

        private ICommand _refreshLayersCommand;

        private ICommand _showHelpCommand;

        private ICommand _browseOutputJZDCommand;

        private ICommand _browseOutputJZXCommand;

        public BoundaryPointLineGeneratorDockPaneViewModel()
        {
            DefaultGdbPath = GetProjectGDBPath();
            LoadPolygonLayers();
            UpdateOutputName();
            UpdateOutputPaths();

            // 订阅地图视图相关事件，自动刷新图层列表
            _mapViewInitializedToken = MapViewInitializedEvent.Subscribe((args) =>
            {
                try
                {
                    DefaultGdbPath = GetProjectGDBPath();
                    LoadPolygonLayers();
                    UpdateOutputName();
                    UpdateOutputPaths();
                }
                catch { }
            });
            _activeMapViewChangedToken = ActiveMapViewChangedEvent.Subscribe((args) =>
            {
                try
                {
                    DefaultGdbPath = GetProjectGDBPath();
                    LoadPolygonLayers();
                    UpdateOutputName();
                    UpdateOutputPaths();
                }
                catch { }
            });
            // 订阅地图选择变化事件并初始化一次选择信息
            _mapSelectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
            UpdateSelectionInfo();
        }

        // 选择集相关属性与逻辑
        private bool _useSelection = true;

        private bool _hasSelection;

        private int _selectedCount;

        private string _selectionInfoText;
    }
}
