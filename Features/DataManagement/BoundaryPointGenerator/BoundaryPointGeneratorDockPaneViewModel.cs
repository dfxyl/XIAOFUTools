using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.DataManagement.BoundaryPointGenerator.Infrastructure;

namespace XIAOFUTools.Features.DataManagement.BoundaryPointGenerator
{
    /// <summary>
    /// 生成四至坐标点DockPane视图模型
    /// </summary>
    internal partial class BoundaryPointGeneratorDockPaneViewModel : PropertyChangedBase
    {
        private dynamic _mapSelectionChangedToken;

        // 取消操作标志
        private bool _cancelRequested = false;
        private readonly ArcGisBoundaryPointPlanner _boundaryPointPlanner = new();
        
        // 是否正在处理
        private bool _isProcessing = false;

        // 面图层列表
        private ObservableCollection<FeatureLayer> _polygonLayers;

        // 选中的面图层
        private FeatureLayer _selectedPolygonLayer;

        // 输出路径
        private string _outputPath;

        // 状态信息
        private string _statusMessage;

        // 日志内容
        private string _logContent;

        // 日志构建器
        private StringBuilder _logBuilder = new StringBuilder();

        // 进度
        private int _progress;

        // 是否为不确定进度
        private bool _isProgressIndeterminate;

        // 选中的保留字段
        private List<string> _selectedFields = new List<string>();

        private string _selectedGenerationMode;

        // 选择集相关属性
        private bool _useSelection = true;

        private bool _hasSelection;

        private int _selectedCount;

        private string _selectionInfoText;

        // 浏览输出路径命令
        private ICommand _browseOutputCommand;

        // 取消命令（只用于停止处理）
        private ICommand _cancelCommand;

        // 运行命令
        private ICommand _runCommand;
        
        // 帮助命令
        private ICommand _showHelpCommand;

        // 选择字段命令
        private ICommand _selectFieldsCommand;

        // 刷新图层命令
        private ICommand _refreshLayersCommand;

        /// <summary>
        /// 构造函数
        /// </summary>
        public BoundaryPointGeneratorDockPaneViewModel()
        {
            // 初始化属性
            PolygonLayers = new ObservableCollection<FeatureLayer>();

            // 设置默认输出路径为工程数据库
            UpdateOutputPath();

            StatusMessage = "请选择面图层。";
            LogContent = "";
            Progress = 0;
            IsProgressIndeterminate = false;

            // 加载面图层
            LoadPolygonLayers();

            // 订阅地图选择变化事件并初始化一次选择信息
            _mapSelectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);
            UpdateSelectionInfo();

            // 初始化生成模式
            GenerationModes.Clear();
            GenerationModes.Add("四向点");
            GenerationModes.Add("四角点");
            SelectedGenerationMode = "四向点";
        }
    }

}
