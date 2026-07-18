using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.IntersectSummary
{
    /// <summary>
    /// 字段选择项
    /// </summary>
    public class FieldSelectItem : PropertyChangedBase
    {
        public string FieldName { get; set; }
        public string Alias { get; set; }
        public string FieldType { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string DisplayText => string.IsNullOrEmpty(Alias) || Alias == FieldName
            ? $"{FieldName}（{FieldType}）"
            : $"{FieldName}（{Alias}）（{FieldType}）";

        public override string ToString() => DisplayText;
    }

    /// <summary>
    /// 交集汇总结果项
    /// </summary>
    public class IntersectSummaryResultItem
    {
        public Dictionary<string, object> RegionValues { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, object> ClassValues { get; set; } = new Dictionary<string, object>();
        public double Area { get; set; }
        public double AdjustedArea { get; set; }
    }

    /// <summary>
    /// 交集汇总表DockPane视图模型
    /// </summary>
    internal partial class IntersectSummaryDockPaneViewModel : PropertyChangedBase
    {
        private readonly IIntersectSummaryResultWindowService _resultWindowService = new IntersectSummaryResultWindowService();

        // 取消操作标志
        private bool _cancelRequested = false;

        // 是否正在处理
        private bool _isProcessing = false;

        // 面图层列表
        private ObservableCollection<FeatureLayer> _polygonLayers;

        // 选中的红线图层
        private FeatureLayer _selectedRedlineLayer;

        // 选中的类要素图层
        private FeatureLayer _selectedClassLayer;

        // 区域字段列表（可多选）
        private ObservableCollection<FieldSelectItem> _regionFields;

        // 类字段列表（可多选）
        private ObservableCollection<FieldSelectItem> _classFields;

        // 面积单位列表
        private ObservableCollection<string> _areaUnits;

        // 选中的面积单位
        private string _selectedAreaUnit;

        // 保留小数位数
        private int _decimalPlaces = 2;

        // 进度值
        private int _progress = 0;

        // 进度条是否不确定
        private bool _isProgressIndeterminate = false;

        // 状态消息
        private string _statusMessage = "请选择图层和字段。";

        // 日志内容
        private string _logContent = "";

        // 结果数据表
        private DataTable _resultTable;

        // 当前结果的区域字段数量（用于导出时合并单元格）
        private int _regionFieldCount = 0;

        // 运行命令
        private ICommand _runCommand;

        // 取消命令
        private ICommand _cancelCommand;

        // 显示帮助命令
        private ICommand _showHelpCommand;

        // 刷新图层命令
        private ICommand _refreshLayersCommand;

        // 导出命令
        private ICommand _exportCommand;

        // 显示结果命令
        private ICommand _showResultCommand;

        /// <summary>
        /// 构造函数
        /// </summary>
        public IntersectSummaryDockPaneViewModel()
        {
            // 初始化属性
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            RegionFields = new ObservableCollection<FieldSelectItem>();
            ClassFields = new ObservableCollection<FieldSelectItem>();

            // 初始化面积单位
            AreaUnits = new ObservableCollection<string> { "平方米", "公顷", "亩" };
            SelectedAreaUnit = "平方米";

            StatusMessage = "请选择图层和字段。";
            LogContent = "";
            Progress = 0;
            IsProgressIndeterminate = false;

            // 加载面图层
            LoadPolygonLayers();
        }
    }
}
