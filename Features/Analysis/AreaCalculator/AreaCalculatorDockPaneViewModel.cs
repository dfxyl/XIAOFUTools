using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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

namespace XIAOFUTools.Features.Analysis.AreaCalculator
{
    /// <summary>
    /// 字段显示信息类
    /// </summary>
    public class FieldDisplayInfo
    {
        public string FieldName { get; set; }
        public string Alias { get; set; }
        public string FieldType { get; set; }

        public string DisplayText => string.IsNullOrEmpty(Alias) || Alias == FieldName
            ? $"{FieldName}（{FieldType}）"
            : $"{FieldName}（{Alias}）（{FieldType}）";

        public override string ToString() => DisplayText;
    }

    /// <summary>
    /// 计算面积DockPane视图模型
    /// </summary>
    internal partial class AreaCalculatorDockPaneViewModel : PropertyChangedBase
    {
        private const string SquareMetersUnit = "平方米";
        private const int SquareMetersDecimalPlaces = 2;
        private const int OtherUnitsDecimalPlaces = 4;

        // 取消操作标志
        private bool _cancelRequested = false;
        
        // 是否正在处理
        private bool _isProcessing = false;

        // 面图层列表
        private ObservableCollection<FeatureLayer> _polygonLayers;

        // 选中的面图层
        private FeatureLayer _selectedPolygonLayer;

        // 优先选中的图层名称（用于右键菜单打开时自动定位）
        private string _preferredLayerName;

        // 优先选中的图层 URI（用于图层重名时精确定位）
        private string _preferredLayerUri;

        // 字段信息列表
        private ObservableCollection<FieldDisplayInfo> _fieldInfos;

        // 选中的字段信息
        private FieldDisplayInfo _selectedFieldInfo;

        // 面积单位列表
        private ObservableCollection<string> _areaUnits;

        // 选中的面积单位
        private string _selectedAreaUnit;

        // 保留小数位数
        private int _decimalPlaces = 2;

        // 面积类型列表
        private ObservableCollection<string> _areaTypes;

        // 选中的面积类型
        private string _selectedAreaType;

        // 进度值
        private int _progress = 0;

        // 进度条是否不确定
        private bool _isProgressIndeterminate = false;

        // 状态消息
        private string _statusMessage = "请选择面图层和字段。";

        // 日志内容
        private string _logContent = "";

        // 运行命令
        private ICommand _runCommand;

        // 取消命令
        private ICommand _cancelCommand;

        // 显示帮助命令
        private ICommand _showHelpCommand;

        // 刷新图层命令
        private ICommand _refreshLayersCommand;

        /// <summary>
        /// 构造函数
        /// </summary>
        public AreaCalculatorDockPaneViewModel()
        {
            // 初始化属性
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            FieldInfos = new ObservableCollection<FieldDisplayInfo>();

            // 初始化面积单位
            AreaUnits = new ObservableCollection<string> { "平方米", "公顷", "亩", "平方公里" };
            SelectedAreaUnit = SquareMetersUnit;

            // 初始化面积类型
            AreaTypes = new ObservableCollection<string> { "平面", "椭球" };
            SelectedAreaType = "平面";

            StatusMessage = "请选择面图层和字段。";
            LogContent = "";
            Progress = 0;
            IsProgressIndeterminate = false;

            // 加载面图层
            LoadPolygonLayers();
        }
    }
}
