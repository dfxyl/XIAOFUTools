using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Features.User.Settings;

namespace XIAOFUTools.Features.Analysis.ViewArea
{
    /// <summary>
    /// 坐标系类型枚举
    /// </summary>
    public enum CoordinateSystemType
    {
        None,        // 无坐标系
        Geographic,  // 地理坐标系
        Projected    // 投影坐标系
    }

    /// <summary>
    /// 计算结果数据模型
    /// </summary>
    public class CalculationResult : PropertyChangedBase
    {
        public string CalculationType { get; set; }
        public string Unit1Value { get; set; }
        public string Unit2Value { get; set; }
        public string Unit3Value { get; set; }
        public string Unit4Value { get; set; }
    }

    /// <summary>
    /// 图层计算汇总数据模型
    /// </summary>
    public class LayerCalculationSummary : PropertyChangedBase
    {
        public string LayerName { get; set; }
        public int FeatureCount { get; set; }

        private ObservableCollection<CalculationResult> _areaResults;
        public ObservableCollection<CalculationResult> AreaResults
        {
            get => _areaResults;
            set
            {
                SetProperty(ref _areaResults, value);
                NotifyPropertyChanged(() => HasAreaResults);
            }
        }

        private ObservableCollection<CalculationResult> _lengthResults;
        public ObservableCollection<CalculationResult> LengthResults
        {
            get => _lengthResults;
            set
            {
                SetProperty(ref _lengthResults, value);
                NotifyPropertyChanged(() => HasLengthResults);
            }
        }

        public bool HasAreaResults => AreaResults != null && AreaResults.Count > 0;
        public bool HasLengthResults => LengthResults != null && LengthResults.Count > 0;

        public LayerCalculationSummary()
        {
            AreaResults = new ObservableCollection<CalculationResult>();
            LengthResults = new ObservableCollection<CalculationResult>();
        }

        /// <summary>
        /// 通知HasAreaResults和HasLengthResults属性变化
        /// </summary>
        public void RefreshHasResultsProperties()
        {
            NotifyPropertyChanged(() => HasAreaResults);
            NotifyPropertyChanged(() => HasLengthResults);
        }
    }

    /// <summary>
    /// 查看面积DockPane视图模型
    /// </summary>
    internal partial class ViewAreaDockPaneViewModel : PropertyChangedBase
    {
        private dynamic _mapSelectionChangedToken;

        // 选择状态信息
        private string _selectionInfo = "未选择任何要素";

        // 合并计算结果
        private ObservableCollection<CalculationResult> _combinedAreaResults;

        private ObservableCollection<CalculationResult> _combinedLengthResults;

        // 分图层计算结果
        private ObservableCollection<LayerCalculationSummary> _layerResults;

        // 是否显示分图层结果
        private bool _showLayerResults = false;

        // 是否正在计算
        private bool _isCalculating = false;

        // 是否有面积结果
        private bool _hasAreaResults = false;

        // 是否有长度结果
        private bool _hasLengthResults = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ViewAreaDockPaneViewModel()
        {
            // 初始化集合
            CombinedAreaResults = new ObservableCollection<CalculationResult>();
            CombinedLengthResults = new ObservableCollection<CalculationResult>();
            LayerResults = new ObservableCollection<LayerCalculationSummary>();

            // 初始化命令
            RefreshCommand = new RelayCommand(RefreshCalculation);
            CopyResultsCommand = new RelayCommand(CopyResults);

            // 订阅地图选择变化事件
            _mapSelectionChangedToken = MapSelectionChangedEvent.Subscribe(OnMapSelectionChanged);

            // 初始计算
            RefreshCalculation();
        }
    }
}
