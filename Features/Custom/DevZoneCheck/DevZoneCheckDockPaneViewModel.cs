using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;

namespace XIAOFUTools.Features.Custom.DevZoneCheck
{
    /// <summary>
    /// 核查结果数据类
    /// </summary>
    public class CheckResultData
    {
        public string ParkName { get; set; } = "";
        public double ParkArea { get; set; }
        
        // 底线类
        public double AreaInUrbanBoundary { get; set; }
        public double AreaOutUrbanBoundary { get; set; }
        public double AreaOnPermanentFarmland { get; set; }
        public double AreaOnEcoRedline { get; set; }
        
        // 节约集约类 - 现状用地
        public double CurrentConstructionLandArea { get; set; }
        public double CurrentIndustrialLandArea { get; set; }
        public double CurrentMiningLandArea { get; set; }
        public double CurrentSaltFieldArea { get; set; }
        public double CurrentWarehouseLandArea { get; set; }
        public double CurrentIndustrialRate { get; set; }
        
        // 节约集约类 - 批而未供、闲置
        public double ApprovedLandArea { get; set; }
        public double ApprovedNotSuppliedArea { get; set; }
        public double ApprovedNotSuppliedRate { get; set; }
        public double SuppliedLandArea { get; set; }
        public double IdleLandArea { get; set; }
        public double IdleLandRate { get; set; }
        
        // 发展空间类
        public double PlannedConstructionLandArea { get; set; }
        public double PlannedIndustrialLandArea { get; set; }
        public double PlannedIndustrialRate { get; set; }
        public double EvidenceArea { get; set; }
        public double AvailableSupplyYears { get; set; }
    }

    /// <summary>
    /// 开发区整合优化核查DockPane视图模型
    /// </summary>
    internal partial class DevZoneCheckDockPaneViewModel : PropertyChangedBase
    {
        
        // 建设用地代码: 05、06、07、08、09、10（1006除外）、1109
        private static readonly string[] ConstructionLandCodes = new[]
        {
            "05", "0501", "0502", "0503", "0504", "0505", "0506", "0507", "0508",
            "06", "0601", "0602", "0603",
            "07", "0701", "0702",
            "08", "0801", "0802", "0803", "0804", "0805", "0806", "0807", "0808", "0809", "0810",
            "09", "0901", "0902", "0903", "0904", "0905", "0906", "0907", "0908", "0909",
            "10", "1001", "1002", "1003", "1004", "1005", "1007", "1008", "1009",
            "1109",
            "05H1", "08H1", "08H2"
        };
        
        // 工业用地代码: 0601
        private static readonly string[] IndustrialLandCodes = new[] { "0601" };
        
        // 采矿用地代码: 0602
        private static readonly string[] MiningLandCodes = new[] { "0602" };
        
        // 盐田代码: 0603
        private static readonly string[] SaltFieldCodes = new[] { "0603" };
        
        // 仓储用地代码: 0508
        private static readonly string[] WarehouseLandCodes = new[] { "0508" };
        
        // 国土空间规划 - 工矿仓储用地代码 (10工矿用地, 11仓储用地)
        private static readonly string[] PlannedIndustrialCodes = new[]
        {
            "10", "1001", "1002", "1003",
            "11", "1101", "1102", "1103"
        };
        
        // 国土空间规划 - 建设用地代码
        private static readonly string[] PlannedConstructionCodes = new[]
        {
            "07", "0701", "0702", "0703", "0704", "0705",
            "08", "0801", "0802", "0803", "0804", "0805", "0806", "0807",
            "09", "0901", "0902", "0903", "0904", "0905", "0906",
            "10", "1001", "1002", "1003",
            "11", "1101", "1102", "1103",
            "12", "1201", "1202", "1203", "1204", "1205", "1206", "1207",
            "13", "1301", "1302", "1303", "1304", "1305",
            "14", "1401", "1402", "1403", "1404", "1405",
            "15", "1501", "1502", "1503",
            "16", "1601", "1602", "1603", "1604"
        };

        private bool _cancelRequested = false;

        private bool _isProcessing = false;

        private bool _hasResult = false;

        // 面图层列表
        private ObservableCollection<FeatureLayer> _polygonLayers;

        // 园区图层
        private FeatureLayer _selectedParkLayer;

        // 园区分组字段列表
        private ObservableCollection<string> _parkFields;

        private string _selectedParkField;

        private bool _enableUrbanBoundary = false;

        private FeatureLayer _urbanBoundaryLayer;

        private bool _enablePermanentFarmland = false;

        private FeatureLayer _permanentFarmlandLayer;

        private bool _enableEcoRedline = false;

        private FeatureLayer _ecoRedlineLayer;

        private bool _enableLandSurvey = false;

        private FeatureLayer _landSurveyLayer;

        private ObservableCollection<string> _landSurveyFields;

        private string _selectedLandSurveyField = "DLBM";

        private bool _enableApprovedNotSupplied = false;

        private FeatureLayer _approvedNotSuppliedLayer;

        private bool _enableApprovedLand = false;

        private FeatureLayer _approvedLandLayer;

        private bool _enableSuppliedLand = false;

        private FeatureLayer _suppliedLandLayer;

        private bool _enableIdleLand = false;

        private FeatureLayer _idleLandLayer;

        private bool _enableSpatialPlanning = false;

        private FeatureLayer _spatialPlanningLayer;

        private ObservableCollection<string> _spatialPlanningFields;

        private string _selectedSpatialPlanningField = "GHDLBM";

        private bool _enableEvidenceData = false;

        private FeatureLayer _evidenceDataLayer;

        private ObservableCollection<string> _areaUnits;

        private string _selectedAreaUnit = "公顷";

        private int _decimalPlaces = 4;
        private string _selectedAreaCalculationMethod = "椭球面";

        private bool _enableSupplyYearData = false;

        private FeatureLayer _supplyYearDataLayer;

        private int _progress = 0;

        private bool _isProgressIndeterminate = false;

        private string _logContent = "";

        // 核查结果
        private List<CheckResultData> _checkResults = new List<CheckResultData>();

        public DevZoneCheckDockPaneViewModel()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            ParkFields = new ObservableCollection<string>();
            LandSurveyFields = new ObservableCollection<string>();
            SpatialPlanningFields = new ObservableCollection<string>();
            AreaUnits = new ObservableCollection<string> { "平方米", "公顷", "亩", "平方公里" };
            AreaCalculationMethods = new ObservableCollection<string> { "平面", "椭球面" };

            RefreshLayersCommand = new RelayCommand(() => RefreshLayers());
            AutoMatchLayersCommand = new RelayCommand(() => AutoMatchLayers());
            RunCommand = new RelayCommand(async () => await RunCheckAsync());
            CancelCommand = new RelayCommand(() => CancelRequested = true);
            ShowHelpCommand = new RelayCommand(() => ShowHelp());
            ExportReportCommand = new RelayCommand(async () => await ExportReportAsync());
        }
    }
}
