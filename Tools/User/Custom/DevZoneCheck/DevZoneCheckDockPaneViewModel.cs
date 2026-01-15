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
using Excel = Microsoft.Office.Interop.Excel;

namespace XIAOFUTools.Tools.User.Custom.DevZoneCheck
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
    internal class DevZoneCheckDockPaneViewModel : PropertyChangedBase
    {
        #region 变更调查地类代码常量
        
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
        
        #endregion

        #region 属性

        private bool _cancelRequested = false;
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
        }

        private bool _isProcessing = false;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyPropertyChanged(() => CanProcess);
            }
        }

        public bool CanProcess => !IsProcessing && SelectedParkLayer != null;

        private bool _hasResult = false;
        public bool HasResult
        {
            get => _hasResult;
            set => SetProperty(ref _hasResult, value);
        }

        // 面图层列表
        private ObservableCollection<FeatureLayer> _polygonLayers;
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }

        // 园区图层
        private FeatureLayer _selectedParkLayer;
        public FeatureLayer SelectedParkLayer
        {
            get => _selectedParkLayer;
            set
            {
                SetProperty(ref _selectedParkLayer, value);
                NotifyPropertyChanged(() => CanProcess);
                LoadParkFields();
            }
        }

        // 园区分组字段列表
        private ObservableCollection<string> _parkFields;
        public ObservableCollection<string> ParkFields
        {
            get => _parkFields;
            set => SetProperty(ref _parkFields, value);
        }

        private string _selectedParkField;
        public string SelectedParkField
        {
            get => _selectedParkField;
            set => SetProperty(ref _selectedParkField, value);
        }

        #region 底线类图层

        private bool _enableUrbanBoundary = false;
        public bool EnableUrbanBoundary
        {
            get => _enableUrbanBoundary;
            set => SetProperty(ref _enableUrbanBoundary, value);
        }

        private FeatureLayer _urbanBoundaryLayer;
        public FeatureLayer UrbanBoundaryLayer
        {
            get => _urbanBoundaryLayer;
            set => SetProperty(ref _urbanBoundaryLayer, value);
        }

        private bool _enablePermanentFarmland = false;
        public bool EnablePermanentFarmland
        {
            get => _enablePermanentFarmland;
            set => SetProperty(ref _enablePermanentFarmland, value);
        }

        private FeatureLayer _permanentFarmlandLayer;
        public FeatureLayer PermanentFarmlandLayer
        {
            get => _permanentFarmlandLayer;
            set => SetProperty(ref _permanentFarmlandLayer, value);
        }

        private bool _enableEcoRedline = false;
        public bool EnableEcoRedline
        {
            get => _enableEcoRedline;
            set => SetProperty(ref _enableEcoRedline, value);
        }

        private FeatureLayer _ecoRedlineLayer;
        public FeatureLayer EcoRedlineLayer
        {
            get => _ecoRedlineLayer;
            set => SetProperty(ref _ecoRedlineLayer, value);
        }

        #endregion

        #region 节约集约类图层

        private bool _enableLandSurvey = false;
        public bool EnableLandSurvey
        {
            get => _enableLandSurvey;
            set
            {
                SetProperty(ref _enableLandSurvey, value);
                if (value) LoadLandSurveyFields();
            }
        }

        private FeatureLayer _landSurveyLayer;
        public FeatureLayer LandSurveyLayer
        {
            get => _landSurveyLayer;
            set
            {
                SetProperty(ref _landSurveyLayer, value);
                LoadLandSurveyFields();
            }
        }

        private ObservableCollection<string> _landSurveyFields;
        public ObservableCollection<string> LandSurveyFields
        {
            get => _landSurveyFields;
            set => SetProperty(ref _landSurveyFields, value);
        }

        private string _selectedLandSurveyField = "DLBM";
        public string SelectedLandSurveyField
        {
            get => _selectedLandSurveyField;
            set => SetProperty(ref _selectedLandSurveyField, value);
        }

        private bool _enableApprovedNotSupplied = false;
        public bool EnableApprovedNotSupplied
        {
            get => _enableApprovedNotSupplied;
            set
            {
                SetProperty(ref _enableApprovedNotSupplied, value);
                // 批而未供依赖已批建设用地
                if (value && !EnableApprovedLand)
                {
                    EnableApprovedLand = true;
                }
            }
        }

        private FeatureLayer _approvedNotSuppliedLayer;
        public FeatureLayer ApprovedNotSuppliedLayer
        {
            get => _approvedNotSuppliedLayer;
            set => SetProperty(ref _approvedNotSuppliedLayer, value);
        }

        private bool _enableApprovedLand = false;
        public bool EnableApprovedLand
        {
            get => _enableApprovedLand;
            set => SetProperty(ref _enableApprovedLand, value);
        }

        private FeatureLayer _approvedLandLayer;
        public FeatureLayer ApprovedLandLayer
        {
            get => _approvedLandLayer;
            set => SetProperty(ref _approvedLandLayer, value);
        }

        private bool _enableSuppliedLand = false;
        public bool EnableSuppliedLand
        {
            get => _enableSuppliedLand;
            set
            {
                SetProperty(ref _enableSuppliedLand, value);
                NotifyPropertyChanged(nameof(ShowEvidenceDataOption));
            }
        }

        private FeatureLayer _suppliedLandLayer;
        public FeatureLayer SuppliedLandLayer
        {
            get => _suppliedLandLayer;
            set => SetProperty(ref _suppliedLandLayer, value);
        }

        private bool _enableIdleLand = false;
        public bool EnableIdleLand
        {
            get => _enableIdleLand;
            set
            {
                SetProperty(ref _enableIdleLand, value);
                // 闲置土地依赖已供应数据
                if (value && !EnableSuppliedLand)
                {
                    EnableSuppliedLand = true;
                }
            }
        }

        private FeatureLayer _idleLandLayer;
        public FeatureLayer IdleLandLayer
        {
            get => _idleLandLayer;
            set => SetProperty(ref _idleLandLayer, value);
        }

        #endregion

        #region 发展空间类图层

        private bool _enableSpatialPlanning = false;
        public bool EnableSpatialPlanning
        {
            get => _enableSpatialPlanning;
            set
            {
                SetProperty(ref _enableSpatialPlanning, value);
                NotifyPropertyChanged(nameof(ShowEvidenceDataOption));
                if (value) LoadSpatialPlanningFields();
            }
        }

        private FeatureLayer _spatialPlanningLayer;
        public FeatureLayer SpatialPlanningLayer
        {
            get => _spatialPlanningLayer;
            set
            {
                SetProperty(ref _spatialPlanningLayer, value);
                LoadSpatialPlanningFields();
            }
        }

        private ObservableCollection<string> _spatialPlanningFields;
        public ObservableCollection<string> SpatialPlanningFields
        {
            get => _spatialPlanningFields;
            set => SetProperty(ref _spatialPlanningFields, value);
        }

        private string _selectedSpatialPlanningField = "GHDLBM";
        public string SelectedSpatialPlanningField
        {
            get => _selectedSpatialPlanningField;
            set => SetProperty(ref _selectedSpatialPlanningField, value);
        }

        private bool _enableEvidenceData = false;
        public bool EnableEvidenceData
        {
            get => _enableEvidenceData;
            set
            {
                SetProperty(ref _enableEvidenceData, value);
                // 举证数据（年限计算）依赖规划和已供应
                if (value)
                {
                    if (!EnableSpatialPlanning) EnableSpatialPlanning = true;
                    if (!EnableSuppliedLand) EnableSuppliedLand = true;
                }
            }
        }

        private FeatureLayer _evidenceDataLayer;
        public FeatureLayer EvidenceDataLayer
        {
            get => _evidenceDataLayer;
            set => SetProperty(ref _evidenceDataLayer, value);
        }

        // 举证数据选项是否显示（需要规划和已供应都勾选）
        public bool ShowEvidenceDataOption => EnableSpatialPlanning && EnableSuppliedLand;

        #endregion

        #region 输出设置

        private ObservableCollection<string> _areaUnits;
        public ObservableCollection<string> AreaUnits
        {
            get => _areaUnits;
            set => SetProperty(ref _areaUnits, value);
        }

        private string _selectedAreaUnit = "公顷";
        public string SelectedAreaUnit
        {
            get => _selectedAreaUnit;
            set => SetProperty(ref _selectedAreaUnit, value);
        }

        private int _decimalPlaces = 4;
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set => SetProperty(ref _decimalPlaces, value);
        }

        public ObservableCollection<string> AreaCalculationMethods { get; }
        private string _selectedAreaCalculationMethod = "椭球面";
        public string SelectedAreaCalculationMethod
        {
            get => _selectedAreaCalculationMethod;
            set => SetProperty(ref _selectedAreaCalculationMethod, value);
        }

        private bool _enableSupplyYearData = false;
        public bool EnableSupplyYearData
        {
            get => _enableSupplyYearData;
            set => SetProperty(ref _enableSupplyYearData, value);
        }

        private FeatureLayer _supplyYearDataLayer;
        public FeatureLayer SupplyYearDataLayer
        {
            get => _supplyYearDataLayer;
            set => SetProperty(ref _supplyYearDataLayer, value);
        }

        #endregion

        #region 进度和日志

        private int _progress = 0;
        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private bool _isProgressIndeterminate = false;
        public bool IsProgressIndeterminate
        {
            get => _isProgressIndeterminate;
            set => SetProperty(ref _isProgressIndeterminate, value);
        }

        private string _logContent = "";
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        #endregion

        // 核查结果
        private List<CheckResultData> _checkResults = new List<CheckResultData>();

        #endregion

        #region 命令

        public ICommand RefreshLayersCommand { get; }
        public ICommand AutoMatchLayersCommand { get; }
        public ICommand RunCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand ExportReportCommand { get; }

        #endregion

        #region 构造函数

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

        #endregion

        #region 方法

        public void RefreshLayers()
        {
            QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map == null) return;

                var layers = map.GetLayersAsFlattenedList()
                    .OfType<FeatureLayer>()
                    .Where(l =>
                    {
                        try
                        {
                            var fc = l.GetFeatureClass();
                            if (fc == null) return false;
                            var shapeType = fc.GetDefinition().GetShapeType();
                            return shapeType == GeometryType.Polygon;
                        }
                        catch { return false; }
                    })
                    .ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    PolygonLayers.Clear();
                    foreach (var layer in layers)
                    {
                        PolygonLayers.Add(layer);
                    }
                });
            });
        }

        public void AutoMatchLayers()
        {
            if (PolygonLayers == null || PolygonLayers.Count == 0)
            {
                RefreshLayers();
            }

            var keywords = new Dictionary<string, Action<FeatureLayer>>
            {
                { "园区", layer => SelectedParkLayer = layer },
                { "城镇开发边界", layer => { EnableUrbanBoundary = true; UrbanBoundaryLayer = layer; } },
                { "永久基本农田", layer => { EnablePermanentFarmland = true; PermanentFarmlandLayer = layer; } },
                { "生态保护红线", layer => { EnableEcoRedline = true; EcoRedlineLayer = layer; } },
                { "三调", layer => { EnableLandSurvey = true; LandSurveyLayer = layer; } },
                { "现状", layer => { EnableLandSurvey = true; LandSurveyLayer = layer; } },
                { "批而未供", layer => { EnableApprovedNotSupplied = true; ApprovedNotSuppliedLayer = layer; } },
                { "已批", layer => { EnableApprovedLand = true; ApprovedLandLayer = layer; } },
                { "已供", layer => { EnableSuppliedLand = true; SuppliedLandLayer = layer; } },
                { "供地", layer => { EnableSuppliedLand = true; SuppliedLandLayer = layer; } },
                { "闲置", layer => { EnableIdleLand = true; IdleLandLayer = layer; } },
                { "规划", layer => { EnableSpatialPlanning = true; SpatialPlanningLayer = layer; } },
                { "国土空间", layer => { EnableSpatialPlanning = true; SpatialPlanningLayer = layer; } },
                { "举证", layer => { EnableEvidenceData = true; EvidenceDataLayer = layer; } },
                { "年限", layer => { EnableSupplyYearData = true; SupplyYearDataLayer = layer; } },
                { "19-23", layer => { EnableSupplyYearData = true; SupplyYearDataLayer = layer; } },
                { "2019", layer => { EnableSupplyYearData = true; SupplyYearDataLayer = layer; } }
            };

            int matchCount = 0;
            foreach (var layer in PolygonLayers)
            {
                var layerName = layer.Name;
                foreach (var keyword in keywords)
                {
                    if (layerName.Contains(keyword.Key))
                    {
                        keyword.Value(layer);
                        matchCount++;
                        break;
                    }
                }
            }

            LogInfo($"自动匹配完成，共匹配 {matchCount} 个图层");
        }

        private void LoadParkFields()
        {
            if (SelectedParkLayer == null) return;

            QueuedTask.Run(() =>
            {
                var fc = SelectedParkLayer.GetFeatureClass();
                if (fc == null) return;

                var fields = fc.GetDefinition().GetFields()
                    .Where(f => f.FieldType == FieldType.String || 
                               f.FieldType == FieldType.Integer ||
                               f.FieldType == FieldType.SmallInteger)
                    .Select(f => f.Name)
                    .ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ParkFields.Clear();
                    ParkFields.Add(""); // 允许不选择
                    foreach (var field in fields)
                    {
                        ParkFields.Add(field);
                    }
                });
            });
        }

        private void LoadLandSurveyFields()
        {
            if (LandSurveyLayer == null) return;

            QueuedTask.Run(() =>
            {
                var fc = LandSurveyLayer.GetFeatureClass();
                if (fc == null) return;

                var fields = fc.GetDefinition().GetFields()
                    .Where(f => f.FieldType == FieldType.String)
                    .Select(f => f.Name)
                    .ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    LandSurveyFields.Clear();
                    foreach (var field in fields)
                    {
                        LandSurveyFields.Add(field);
                    }
                    // 自动选择DLBM字段
                    if (fields.Contains("DLBM"))
                        SelectedLandSurveyField = "DLBM";
                    else if (fields.Count > 0)
                        SelectedLandSurveyField = fields[0];
                });
            });
        }

        private void LoadSpatialPlanningFields()
        {
            if (SpatialPlanningLayer == null) return;

            QueuedTask.Run(() =>
            {
                var fc = SpatialPlanningLayer.GetFeatureClass();
                if (fc == null) return;

                var fields = fc.GetDefinition().GetFields()
                    .Where(f => f.FieldType == FieldType.String)
                    .Select(f => f.Name)
                    .ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SpatialPlanningFields.Clear();
                    foreach (var field in fields)
                    {
                        SpatialPlanningFields.Add(field);
                    }

                    if (SpatialPlanningFields.Contains("GHDLBM"))
                    {
                        SelectedSpatialPlanningField = "GHDLBM";
                    }
                    else if (SpatialPlanningFields.Count > 0)
                    {
                        SelectedSpatialPlanningField = SpatialPlanningFields[0];
                    }
                });
            });
        }

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] {message}\n";
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] 错误: {message}\n";
        }

        private void ClearLog()
        {
            LogContent = "";
        }

        /// <summary>
        /// 计算面积（平方米）- 支持平面和椭球面
        /// </summary>
        private double CalculateArea(Geometry geometry)
        {
            if (geometry == null || geometry.IsEmpty) return 0;
            
            try
            {
                if (SelectedAreaCalculationMethod == "椭球面")
                {
                    // 椭球面计算需要有效的空间参考
                    if (geometry.SpatialReference == null)
                    {
                        // 没有空间参考，回退到平面计算
                        return Math.Abs(((Polygon)geometry).Area);
                    }
                    
                    return Math.Abs(GeometryEngine.Instance.GeodesicArea(geometry));
                }
                else
                {
                    return Math.Abs(((Polygon)geometry).Area);
                }
            }
            catch (Exception)
            {
                // 回退到平面计算
                try
                {
                    return Math.Abs(((Polygon)geometry).Area);
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// 转换面积单位
        /// </summary>
        private double ConvertArea(double squareMeters)
        {
            return SelectedAreaUnit switch
            {
                "平方米" => squareMeters,
                "公顷" => squareMeters / 10000.0,
                "亩" => squareMeters / 666.67,
                "平方公里" => squareMeters / 1000000.0,
                _ => squareMeters
            };
        }

        /// <summary>
        /// 格式化面积
        /// </summary>
        private string FormatArea(double area)
        {
            return Math.Round(area, DecimalPlaces).ToString($"F{DecimalPlaces}");
        }

        /// <summary>
        /// 计算两个图层的交集面积（优化版：使用空间过滤）
        /// </summary>
        private async Task<double> CalculateIntersectAreaAsync(FeatureLayer sourceLayer, Geometry clipGeometry, 
            string filterField = null, string[] filterCodes = null)
        {
            double totalArea = 0;

            await QueuedTask.Run(() =>
            {
                var fc = sourceLayer.GetFeatureClass();
                if (fc == null) return;

                // 获取数据层的空间参考
                var layerSpatialRef = fc.GetDefinition().GetSpatialReference();
                var clipSpatialRef = clipGeometry.SpatialReference;
                
                // 确定统一的目标坐标系：优先使用clipGeometry的空间参考（园区图层）
                SpatialReference targetSpatialRef = clipSpatialRef ?? layerSpatialRef;
                
                // 准备clipGeometry - 确保有空间参考
                Geometry clipGeomInTarget = clipGeometry;
                
                // 如果clipGeometry没有空间参考但图层有，为其设置空间参考
                if (clipSpatialRef == null && layerSpatialRef != null)
                {
                    try
                    {
                        var builder = new PolygonBuilderEx(clipGeometry as Polygon);
                        builder.SpatialReference = layerSpatialRef;
                        clipGeomInTarget = builder.ToGeometry();
                        targetSpatialRef = layerSpatialRef;
                    }
                    catch { }
                }

                // 为空间过滤器准备几何（需要与数据层坐标系一致）
                Geometry filterGeometry = clipGeomInTarget;
                if (layerSpatialRef != null && clipGeomInTarget.SpatialReference != null &&
                    !SpatialReference.AreEqual(layerSpatialRef, clipGeomInTarget.SpatialReference, false))
                {
                    try
                    {
                        filterGeometry = GeometryEngine.Instance.Project(clipGeomInTarget, layerSpatialRef);
                    }
                    catch
                    {
                        filterGeometry = clipGeomInTarget;
                    }
                }

                // 使用空间过滤器提高性能
                var spatialFilter = new SpatialQueryFilter
                {
                    FilterGeometry = filterGeometry,
                    SpatialRelationship = SpatialRelationship.Intersects
                };

                // 如果有字段过滤条件，添加到查询
                if (!string.IsNullOrEmpty(filterField) && filterCodes != null && filterCodes.Length > 0)
                {
                    var whereClauses = filterCodes.Select(code => 
                        $"{filterField} = '{code}' OR {filterField} LIKE '{code}%'");
                    spatialFilter.WhereClause = string.Join(" OR ", whereClauses);
                }

                using (var cursor = fc.Search(spatialFilter))
                {
                    while (cursor.MoveNext())
                    {
                        if (CancelRequested) break;

                        using (var feature = cursor.Current as Feature)
                        {
                            var geom = feature.GetShape();
                            if (geom == null || geom.IsEmpty) continue;

                            try
                            {
                                Geometry processGeom = geom;
                                Geometry clipGeomForIntersect = clipGeomInTarget;
                                
                                // 获取要素的空间参考（优先使用要素自身的，否则使用图层的）
                                var geomSpatialRef = geom.SpatialReference ?? layerSpatialRef;
                                var clipGeomSpatialRef = clipGeomForIntersect.SpatialReference;
                                
                                // 情况1：两者都没有空间参考 - 直接计算
                                if (geomSpatialRef == null && clipGeomSpatialRef == null)
                                {
                                    // 无需投影，直接计算
                                }
                                // 情况2：要素没有空间参考，clipGeom有 - 为要素设置clipGeom的空间参考
                                else if (geomSpatialRef == null && clipGeomSpatialRef != null)
                                {
                                    var builder = new PolygonBuilderEx(geom as Polygon);
                                    builder.SpatialReference = clipGeomSpatialRef;
                                    processGeom = builder.ToGeometry();
                                }
                                // 情况3：要素有空间参考，clipGeom没有 - 为clipGeom设置要素的空间参考
                                else if (geomSpatialRef != null && clipGeomSpatialRef == null)
                                {
                                    var builder = new PolygonBuilderEx(clipGeomForIntersect as Polygon);
                                    builder.SpatialReference = geomSpatialRef;
                                    clipGeomForIntersect = builder.ToGeometry();
                                }
                                // 情况4：两者都有空间参考但不同 - 投影要素到clipGeom的坐标系
                                else if (!SpatialReference.AreEqual(geomSpatialRef, clipGeomSpatialRef, false))
                                {
                                    processGeom = GeometryEngine.Instance.Project(geom, clipGeomSpatialRef);
                                }
                                
                                // 计算交集
                                var intersection = GeometryEngine.Instance.Intersection(processGeom, clipGeomForIntersect);
                                if (intersection != null && !intersection.IsEmpty)
                                {
                                    totalArea += CalculateArea(intersection);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Intersection error: {ex.Message}");
                                continue;
                            }
                        }
                    }
                }
            });

            return totalArea;
        }


        /// <summary>
        /// 执行核查
        /// </summary>
        private async Task RunCheckAsync()
        {
            // 验证必要选项
            if (SelectedParkLayer == null)
            {
                MessageBox.Show("请选择园区红线图层！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 验证变更调查数据字段
            if (EnableLandSurvey && LandSurveyLayer != null && string.IsNullOrEmpty(SelectedLandSurveyField))
            {
                MessageBox.Show("已勾选变更调查数据，请选择地类代码字段！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 验证国土空间规划字段
            if (EnableSpatialPlanning && SpatialPlanningLayer != null && string.IsNullOrEmpty(SelectedSpatialPlanningField))
            {
                MessageBox.Show("已勾选国土空间规划，请选择用地代码字段！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 验证依赖关系
            if (EnableApprovedNotSupplied && ApprovedNotSuppliedLayer != null && ApprovedLandLayer == null)
            {
                MessageBox.Show("批而未供率计算需要已批建设用地数据！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (EnableIdleLand && IdleLandLayer != null && SuppliedLandLayer == null)
            {
                MessageBox.Show("闲置土地率计算需要已供应建设用地数据！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (EnableEvidenceData && EvidenceDataLayer != null)
            {
                if (SpatialPlanningLayer == null)
                {
                    MessageBox.Show("尚可供应年限计算需要国土空间规划数据！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (SuppliedLandLayer == null)
                {
                    MessageBox.Show("尚可供应年限计算需要已供应建设用地数据！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            try
            {
                IsProcessing = true;
                CancelRequested = false;
                Progress = 0;
                ClearLog();
                _checkResults.Clear();
                HasResult = false;

                LogInfo("开始开发区整合优化核查...");
                LogInfo($"园区图层: {SelectedParkLayer.Name}");
                LogInfo($"面积单位: {SelectedAreaUnit}，小数位数: {DecimalPlaces}");
                LogInfo($"面积计算方式: {SelectedAreaCalculationMethod}");

                // 获取园区要素并按分组字段合并
                var parkFeatures = new List<(string Name, Geometry Geom)>();
                
                await QueuedTask.Run(() =>
                {
                    var fc = SelectedParkLayer.GetFeatureClass();
                    
                    if (!string.IsNullOrEmpty(SelectedParkField))
                    {
                        // 有分组字段：按字段值分组合并
                        LogInfo($"按字段 [{SelectedParkField}] 分组合并...");
                        var groupDict = new Dictionary<string, List<Geometry>>();
                        
                        using (var cursor = fc.Search())
                        {
                            while (cursor.MoveNext())
                            {
                                using (var feature = cursor.Current as Feature)
                                {
                                    var geom = feature.GetShape();
                                    if (geom == null || geom.IsEmpty) continue;

                                    var groupValue = feature[SelectedParkField]?.ToString() ?? "未分组";
                                    if (!groupDict.ContainsKey(groupValue))
                                    {
                                        groupDict[groupValue] = new List<Geometry>();
                                    }
                                    groupDict[groupValue].Add(geom);
                                }
                            }
                        }

                        // 合并每个分组的几何
                        foreach (var kvp in groupDict)
                        {
                            if (kvp.Value.Count == 1)
                            {
                                parkFeatures.Add((kvp.Key, kvp.Value[0]));
                            }
                            else
                            {
                                try
                                {
                                    var union = GeometryEngine.Instance.Union(kvp.Value);
                                    parkFeatures.Add((kvp.Key, union));
                                }
                                catch
                                {
                                    // 合并失败则逐个添加
                                    for (int i = 0; i < kvp.Value.Count; i++)
                                    {
                                        parkFeatures.Add(($"{kvp.Key}_{i + 1}", kvp.Value[i]));
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // 无分组字段：合并所有要素为一个整体
                        LogInfo("合并所有要素为整体...");
                        var allGeoms = new List<Geometry>();
                        
                        using (var cursor = fc.Search())
                        {
                            while (cursor.MoveNext())
                            {
                                using (var feature = cursor.Current as Feature)
                                {
                                    var geom = feature.GetShape();
                                    if (geom != null && !geom.IsEmpty)
                                    {
                                        allGeoms.Add(geom);
                                    }
                                }
                            }
                        }

                        if (allGeoms.Count > 0)
                        {
                            try
                            {
                                var union = GeometryEngine.Instance.Union(allGeoms);
                                parkFeatures.Add((SelectedParkLayer.Name, union));
                            }
                            catch
                            {
                                // 合并失败则保持原样
                                for (int i = 0; i < allGeoms.Count; i++)
                                {
                                    parkFeatures.Add(($"{SelectedParkLayer.Name}_{i + 1}", allGeoms[i]));
                                }
                            }
                        }
                    }
                });

                LogInfo($"共 {parkFeatures.Count} 个分组");

                int totalSteps = parkFeatures.Count;
                int currentStep = 0;

                foreach (var (parkName, parkGeom) in parkFeatures)
                {
                    if (CancelRequested)
                    {
                        LogInfo("用户取消操作");
                        break;
                    }

                    currentStep++;
                    Progress = (int)((double)currentStep / totalSteps * 100);
                    LogInfo($"正在核查: {parkName} ({currentStep}/{totalSteps})");

                    var result = new CheckResultData
                    {
                        ParkName = parkName,
                        ParkArea = ConvertArea(CalculateArea(parkGeom))
                    };

                    // 底线类核查
                    if (EnableUrbanBoundary && UrbanBoundaryLayer != null)
                    {
                        result.AreaInUrbanBoundary = ConvertArea(
                            await CalculateIntersectAreaAsync(UrbanBoundaryLayer, parkGeom));
                        result.AreaOutUrbanBoundary = result.ParkArea - result.AreaInUrbanBoundary;
                        if (result.AreaOutUrbanBoundary < 0) result.AreaOutUrbanBoundary = 0;
                    }

                    if (EnablePermanentFarmland && PermanentFarmlandLayer != null)
                    {
                        result.AreaOnPermanentFarmland = ConvertArea(
                            await CalculateIntersectAreaAsync(PermanentFarmlandLayer, parkGeom));
                    }

                    if (EnableEcoRedline && EcoRedlineLayer != null)
                    {
                        result.AreaOnEcoRedline = ConvertArea(
                            await CalculateIntersectAreaAsync(EcoRedlineLayer, parkGeom));
                    }

                    // 节约集约类核查
                    if (EnableLandSurvey && LandSurveyLayer != null && !string.IsNullOrEmpty(SelectedLandSurveyField))
                    {
                        result.CurrentConstructionLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(LandSurveyLayer, parkGeom, 
                                SelectedLandSurveyField, ConstructionLandCodes));

                        result.CurrentIndustrialLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(LandSurveyLayer, parkGeom, 
                                SelectedLandSurveyField, IndustrialLandCodes));

                        result.CurrentMiningLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(LandSurveyLayer, parkGeom, 
                                SelectedLandSurveyField, MiningLandCodes));

                        result.CurrentSaltFieldArea = ConvertArea(
                            await CalculateIntersectAreaAsync(LandSurveyLayer, parkGeom, 
                                SelectedLandSurveyField, SaltFieldCodes));

                        result.CurrentWarehouseLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(LandSurveyLayer, parkGeom, 
                                SelectedLandSurveyField, WarehouseLandCodes));

                        // 现状工业用地率 = 现状工矿仓储用地面积/现状建设用地面积*100%
                        double industrialWarehouseArea = result.CurrentIndustrialLandArea + 
                            result.CurrentMiningLandArea + result.CurrentSaltFieldArea + 
                            result.CurrentWarehouseLandArea;
                        if (result.CurrentConstructionLandArea > 0)
                        {
                            result.CurrentIndustrialRate = industrialWarehouseArea / 
                                result.CurrentConstructionLandArea * 100;
                        }
                    }

                    if (EnableApprovedLand && ApprovedLandLayer != null)
                    {
                        LogInfo("  计算已批建设用地...");
                        result.ApprovedLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(ApprovedLandLayer, parkGeom));
                    }

                    if (EnableApprovedNotSupplied && ApprovedNotSuppliedLayer != null && ApprovedLandLayer != null)
                    {
                        LogInfo("  计算批而未供面积...");
                        
                        // 在已批建设用地范围内计算批而未供数据
                        result.ApprovedNotSuppliedArea = await QueuedTask.Run(() =>
                        {
                            var approvedFC = ApprovedLandLayer.GetFeatureClass();
                            var notSuppliedFC = ApprovedNotSuppliedLayer.GetFeatureClass();
                            
                            if (approvedFC == null || notSuppliedFC == null)
                                return 0.0;

                            var targetSpatialRef = parkGeom.SpatialReference;
                            Geometry approvedGeom = null;

                            // 1. 获取已批建设用地与园区的交集
                            var approvedSpatialRef = approvedFC.GetDefinition().GetSpatialReference();
                            var parkGeomForApproved = parkGeom;
                            if (approvedSpatialRef != null && parkGeom.SpatialReference?.Wkid != approvedSpatialRef.Wkid)
                            {
                                try
                                {
                                    parkGeomForApproved = GeometryEngine.Instance.Project(parkGeom, approvedSpatialRef);
                                }
                                catch
                                {
                                    return 0.0;
                                }
                            }

                            var approvedFilter = new SpatialQueryFilter
                            {
                                FilterGeometry = parkGeomForApproved,
                                SpatialRelationship = SpatialRelationship.Intersects
                            };

                            using (var cursor = approvedFC.Search(approvedFilter))
                            {
                                while (cursor.MoveNext())
                                {
                                    if (CancelRequested) break;

                                    using (var feature = cursor.Current as Feature)
                                    {
                                        var geom = feature.GetShape();
                                        if (geom == null || geom.IsEmpty) continue;

                                        if (geom.SpatialReference?.Wkid != targetSpatialRef?.Wkid)
                                        {
                                            geom = GeometryEngine.Instance.Project(geom, targetSpatialRef);
                                        }

                                        var intersect = GeometryEngine.Instance.Intersection(geom, parkGeom);
                                        if (intersect != null && !intersect.IsEmpty)
                                        {
                                            // 确保交集结果有正确的空间参考
                                            if (intersect.SpatialReference == null || intersect.SpatialReference.Wkid != targetSpatialRef?.Wkid)
                                            {
                                                try
                                                {
                                                    intersect = GeometryEngine.Instance.Project(intersect, targetSpatialRef);
                                                }
                                                catch { continue; }
                                            }

                                            if (approvedGeom == null)
                                            {
                                                approvedGeom = intersect;
                                            }
                                            else
                                            {
                                                var unionResult = GeometryEngine.Instance.Union(approvedGeom, intersect);
                                                if (unionResult != null && !unionResult.IsEmpty)
                                                {
                                                    if (unionResult.SpatialReference == null || unionResult.SpatialReference.Wkid != targetSpatialRef?.Wkid)
                                                    {
                                                        try
                                                        {
                                                            unionResult = GeometryEngine.Instance.Project(unionResult, targetSpatialRef);
                                                        }
                                                        catch { }
                                                    }
                                                    approvedGeom = unionResult;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (approvedGeom == null || approvedGeom.IsEmpty)
                                return 0.0;

                            // 2. 在已批建设用地范围内计算批而未供数据面积
                            double totalArea = 0;
                            var notSuppliedSpatialRef = notSuppliedFC.GetDefinition().GetSpatialReference();
                            var approvedGeomForNotSupplied = approvedGeom;
                            if (notSuppliedSpatialRef != null && approvedGeom.SpatialReference?.Wkid != notSuppliedSpatialRef.Wkid)
                            {
                                try
                                {
                                    approvedGeomForNotSupplied = GeometryEngine.Instance.Project(approvedGeom, notSuppliedSpatialRef);
                                }
                                catch
                                {
                                    // 投影失败，继续使用原几何
                                    approvedGeomForNotSupplied = approvedGeom;
                                }
                            }

                            var notSuppliedFilter = new SpatialQueryFilter
                            {
                                FilterGeometry = approvedGeomForNotSupplied,
                                SpatialRelationship = SpatialRelationship.Intersects
                            };

                            using (var cursor = notSuppliedFC.Search(notSuppliedFilter))
                            {
                                while (cursor.MoveNext())
                                {
                                    if (CancelRequested) break;

                                    using (var feature = cursor.Current as Feature)
                                    {
                                        var geom = feature.GetShape();
                                        if (geom == null || geom.IsEmpty) continue;

                                        if (geom.SpatialReference?.Wkid != targetSpatialRef?.Wkid)
                                        {
                                            geom = GeometryEngine.Instance.Project(geom, targetSpatialRef);
                                        }

                                        var intersect = GeometryEngine.Instance.Intersection(geom, approvedGeom);
                                        if (intersect != null && !intersect.IsEmpty)
                                        {
                                            totalArea += CalculateArea(intersect);
                                        }
                                    }
                                }
                            }

                            return totalArea;
                        });
                        
                        result.ApprovedNotSuppliedArea = ConvertArea(result.ApprovedNotSuppliedArea);
                        
                        // 批而未供率 = 批而未供面积/已批准建设用地面积*100%
                        if (result.ApprovedLandArea > 0)
                        {
                            result.ApprovedNotSuppliedRate = result.ApprovedNotSuppliedArea / 
                                result.ApprovedLandArea * 100;
                        }
                    }

                    if (EnableSuppliedLand && SuppliedLandLayer != null)
                    {
                        LogInfo("  计算已供应建设用地...");
                        result.SuppliedLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(SuppliedLandLayer, parkGeom));
                    }

                    if (EnableIdleLand && IdleLandLayer != null)
                    {
                        LogInfo("  计算闲置土地面积...");
                        result.IdleLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(IdleLandLayer, parkGeom));
                        
                        // 闲置土地率 = 闲置土地面积/已供应建设用地面积*100%
                        if (result.SuppliedLandArea > 0)
                        {
                            result.IdleLandRate = result.IdleLandArea / result.SuppliedLandArea * 100;
                        }
                    }

                    // 发展空间类核查
                    if (EnableSpatialPlanning && SpatialPlanningLayer != null && 
                        !string.IsNullOrEmpty(SelectedSpatialPlanningField))
                    {
                        LogInfo("  计算规划建设用地...");
                        result.PlannedConstructionLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(SpatialPlanningLayer, parkGeom, 
                                SelectedSpatialPlanningField, PlannedConstructionCodes));

                        LogInfo("  计算规划工矿仓储用地...");
                        result.PlannedIndustrialLandArea = ConvertArea(
                            await CalculateIntersectAreaAsync(SpatialPlanningLayer, parkGeom, 
                                SelectedSpatialPlanningField, PlannedIndustrialCodes));

                        // 规划工业用地率 = 规划工矿仓储用地面积/规划建设用地面积*100%
                        if (result.PlannedConstructionLandArea > 0)
                        {
                            result.PlannedIndustrialRate = result.PlannedIndustrialLandArea / 
                                result.PlannedConstructionLandArea * 100;
                        }
                    }

                    // 举证面积计算
                    if (EnableEvidenceData && EvidenceDataLayer != null)
                    {
                        result.EvidenceArea = ConvertArea(
                            await CalculateIntersectAreaAsync(EvidenceDataLayer, parkGeom));
                    }

                    // 尚可供应年限计算（使用空间擦除）
                    // 公式: 尚可供应年限 = (规划建设用地 - 已供应) ÷ (园区内19-23年供地面积 ÷ 5)
                    // 举证数据为可选，不强制要求
                    if (EnableSpatialPlanning && EnableSuppliedLand && EnableSupplyYearData &&
                        SpatialPlanningLayer != null && SuppliedLandLayer != null &&
                        SupplyYearDataLayer != null)
                    {
                        LogInfo("  计算尚可供应年限...");
                        // 使用空间擦除计算尚可供应面积
                        double availableArea = await QueuedTask.Run(async () =>
                        {
                            var planningFC = SpatialPlanningLayer.GetFeatureClass();
                            var suppliedFC = SuppliedLandLayer.GetFeatureClass();
                            // 举证数据可选
                            var evidenceFC = (EnableEvidenceData && EvidenceDataLayer != null) 
                                ? EvidenceDataLayer.GetFeatureClass() : null;
                            
                            if (planningFC == null || suppliedFC == null)
                                return 0.0;

                            // 使用园区几何的空间参考作为统一目标坐标系
                            var targetSpatialRef = parkGeom.SpatialReference;
                            if (targetSpatialRef == null)
                            {
                                targetSpatialRef = planningFC.GetDefinition().GetSpatialReference();
                            }
                            
                            Geometry remainingGeom = null;

                            // 1. 获取规划建设用地与园区的交集
                            var planningSpatialRef = planningFC.GetDefinition().GetSpatialReference();
                            var parkGeomForPlanning = parkGeom;
                            if (planningSpatialRef != null && targetSpatialRef != null &&
                                !SpatialReference.AreEqual(parkGeom.SpatialReference, planningSpatialRef, false))
                            {
                                try
                                {
                                    parkGeomForPlanning = GeometryEngine.Instance.Project(parkGeom, planningSpatialRef);
                                }
                                catch
                                {
                                    return 0.0;
                                }
                            }

                            var planningFilter = new SpatialQueryFilter
                            {
                                FilterGeometry = parkGeomForPlanning,
                                SpatialRelationship = SpatialRelationship.Intersects
                            };

                            using (var cursor = planningFC.Search(planningFilter))
                            {
                                while (cursor.MoveNext())
                                {
                                    if (CancelRequested) break;

                                    using (var feature = cursor.Current as Feature)
                                    {
                                        var geom = feature.GetShape();
                                        if (geom == null || geom.IsEmpty) continue;

                                        // 投影到目标坐标系
                                        var geomSpatialRef = geom.SpatialReference ?? planningSpatialRef;
                                        if (geomSpatialRef != null && targetSpatialRef != null &&
                                            !SpatialReference.AreEqual(geomSpatialRef, targetSpatialRef, false))
                                        {
                                            try
                                            {
                                                geom = GeometryEngine.Instance.Project(geom, targetSpatialRef);
                                            }
                                            catch { continue; }
                                        }

                                        try
                                        {
                                            var intersect = GeometryEngine.Instance.Intersection(geom, parkGeom);
                                            if (intersect != null && !intersect.IsEmpty)
                                            {
                                                if (remainingGeom == null)
                                                {
                                                    remainingGeom = intersect;
                                                }
                                                else
                                                {
                                                    var unionResult = GeometryEngine.Instance.Union(remainingGeom, intersect);
                                                    if (unionResult != null && !unionResult.IsEmpty)
                                                    {
                                                        remainingGeom = unionResult;
                                                    }
                                                }
                                            }
                                        }
                                        catch { continue; }
                                    }
                                }
                            }

                            if (remainingGeom == null || remainingGeom.IsEmpty)
                                return 0.0;

                            // 2. 擦除已供应数据
                            var suppliedSpatialRef = suppliedFC.GetDefinition().GetSpatialReference();
                            var remainingGeomForSupplied = remainingGeom;
                            if (suppliedSpatialRef != null && targetSpatialRef != null &&
                                !SpatialReference.AreEqual(remainingGeom.SpatialReference, suppliedSpatialRef, false))
                            {
                                try
                                {
                                    remainingGeomForSupplied = GeometryEngine.Instance.Project(remainingGeom, suppliedSpatialRef);
                                }
                                catch
                                {
                                    remainingGeomForSupplied = remainingGeom;
                                }
                            }

                            var suppliedFilter = new SpatialQueryFilter
                            {
                                FilterGeometry = remainingGeomForSupplied,
                                SpatialRelationship = SpatialRelationship.Intersects
                            };

                            using (var cursor = suppliedFC.Search(suppliedFilter))
                            {
                                while (cursor.MoveNext())
                                {
                                    if (CancelRequested) break;

                                    using (var feature = cursor.Current as Feature)
                                    {
                                        var geom = feature.GetShape();
                                        if (geom == null || geom.IsEmpty) continue;

                                        // 投影到目标坐标系
                                        var geomSpatialRef = geom.SpatialReference ?? suppliedSpatialRef;
                                        if (geomSpatialRef != null && targetSpatialRef != null &&
                                            !SpatialReference.AreEqual(geomSpatialRef, targetSpatialRef, false))
                                        {
                                            try
                                            {
                                                geom = GeometryEngine.Instance.Project(geom, targetSpatialRef);
                                            }
                                            catch { continue; }
                                        }

                                        try
                                        {
                                            var diffResult = GeometryEngine.Instance.Difference(remainingGeom, geom);
                                            if (diffResult == null || diffResult.IsEmpty)
                                            {
                                                remainingGeom = null;
                                                break;
                                            }
                                            remainingGeom = diffResult;
                                        }
                                        catch { continue; }
                                    }
                                }
                            }

                            // 如果已供应擦除后为空，直接返回0
                            if (remainingGeom == null || remainingGeom.IsEmpty)
                                return 0.0;

                            // 3. 擦除举证数据（可选，只有启用举证数据时才执行）
                            if (evidenceFC != null)
                            {
                                var evidenceSpatialRef = evidenceFC.GetDefinition().GetSpatialReference();
                                var remainingGeomForEvidence = remainingGeom;
                                if (evidenceSpatialRef != null && targetSpatialRef != null &&
                                    !SpatialReference.AreEqual(remainingGeom.SpatialReference, evidenceSpatialRef, false))
                                {
                                    try
                                    {
                                        remainingGeomForEvidence = GeometryEngine.Instance.Project(remainingGeom, evidenceSpatialRef);
                                    }
                                    catch
                                    {
                                        remainingGeomForEvidence = remainingGeom;
                                    }
                                }

                                var evidenceFilter = new SpatialQueryFilter
                                {
                                    FilterGeometry = remainingGeomForEvidence,
                                    SpatialRelationship = SpatialRelationship.Intersects
                                };

                                using (var cursor = evidenceFC.Search(evidenceFilter))
                                {
                                    while (cursor.MoveNext())
                                    {
                                        if (CancelRequested) break;

                                        using (var feature = cursor.Current as Feature)
                                        {
                                            var geom = feature.GetShape();
                                            if (geom == null || geom.IsEmpty) continue;

                                            // 投影到目标坐标系
                                            var geomSpatialRef = geom.SpatialReference ?? evidenceSpatialRef;
                                            if (geomSpatialRef != null && targetSpatialRef != null &&
                                                !SpatialReference.AreEqual(geomSpatialRef, targetSpatialRef, false))
                                            {
                                                try
                                                {
                                                    geom = GeometryEngine.Instance.Project(geom, targetSpatialRef);
                                                }
                                                catch { continue; }
                                            }

                                            try
                                            {
                                                var diffResult = GeometryEngine.Instance.Difference(remainingGeom, geom);
                                                if (diffResult == null || diffResult.IsEmpty)
                                                {
                                                    remainingGeom = null;
                                                    break;
                                                }
                                                remainingGeom = diffResult;
                                            }
                                            catch { continue; }
                                        }
                                    }
                                }
                            }

                            // 4. 计算剩余面积（即使举证为空，remainingGeom仍然有效）
                            if (remainingGeom == null || remainingGeom.IsEmpty)
                                return 0.0;
                                
                            return CalculateArea(remainingGeom);
                        });
                        
                        // 转换面积单位
                        availableArea = ConvertArea(availableArea);
                        
                        // 年限数据（19-23年，5年）与园区交集面积
                        double yearDataArea = ConvertArea(
                            await CalculateIntersectAreaAsync(SupplyYearDataLayer, parkGeom));
                        
                        // 年均供应量 = 年限数据面积 / 5
                        double avgAnnualSupply = yearDataArea / 5.0;
                        if (avgAnnualSupply > 0 && availableArea > 0)
                        {
                            result.AvailableSupplyYears = availableArea / avgAnnualSupply;
                        }
                    }

                    _checkResults.Add(result);

                    // 输出简要结果
                    LogInfo($"  园区面积: {FormatArea(result.ParkArea)} {SelectedAreaUnit}");
                    if (EnableUrbanBoundary)
                    {
                        LogInfo($"  开发边界内: {FormatArea(result.AreaInUrbanBoundary)}, 边界外: {FormatArea(result.AreaOutUrbanBoundary)}");
                    }
                    if (EnablePermanentFarmland)
                    {
                        LogInfo($"  压占永久基本农田: {FormatArea(result.AreaOnPermanentFarmland)}");
                    }
                    if (EnableEcoRedline)
                    {
                        LogInfo($"  压占生态保护红线: {FormatArea(result.AreaOnEcoRedline)}");
                    }
                    if (EnableLandSurvey)
                    {
                        LogInfo($"  现状工业用地率: {result.CurrentIndustrialRate:F2}%");
                    }
                    if (EnableApprovedNotSupplied)
                    {
                        LogInfo($"  批而未供率: {result.ApprovedNotSuppliedRate:F2}%");
                    }
                    if (EnableIdleLand)
                    {
                        LogInfo($"  闲置土地率: {result.IdleLandRate:F2}%");
                    }
                    if (EnableSpatialPlanning)
                    {
                        LogInfo($"  规划工业用地率: {result.PlannedIndustrialRate:F2}%");
                    }
                }

                if (!CancelRequested)
                {
                    HasResult = _checkResults.Count > 0;
                    Progress = 100;
                    LogInfo($"核查完成！共处理 {_checkResults.Count} 个园区");
                }
            }
            catch (Exception ex)
            {
                LogError($"核查过程中发生错误: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
            }
        }

        /// <summary>
        /// 导出报告
        /// </summary>
        private async Task ExportReportAsync()
        {
            if (_checkResults.Count == 0)
            {
                MessageBox.Show("没有可导出的结果！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel文件|*.xlsx",
                Title = "导出核查报告",
                FileName = $"开发区整合优化核查报告_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                IsProcessing = true;
                LogInfo("正在导出报告...");

                await Task.Run(() =>
                {
                    Excel.Application excelApp = null;
                    Excel.Workbook workbook = null;

                    try
                    {
                        excelApp = new Excel.Application { Visible = false };
                        workbook = excelApp.Workbooks.Add();
                        var worksheet = (Excel.Worksheet)workbook.Sheets[1];
                        worksheet.Name = "核查报告";

                        // 表头
                        var headers = new List<string>
                        {
                            "园区名称", $"园区面积({SelectedAreaUnit})"
                        };

                        if (EnableUrbanBoundary)
                        {
                            headers.AddRange(new[] { $"开发边界内({SelectedAreaUnit})", $"开发边界外({SelectedAreaUnit})" });
                        }
                        if (EnablePermanentFarmland)
                        {
                            headers.Add($"压占永久基本农田({SelectedAreaUnit})");
                        }
                        if (EnableEcoRedline)
                        {
                            headers.Add($"压占生态保护红线({SelectedAreaUnit})");
                        }
                        if (EnableLandSurvey)
                        {
                            headers.AddRange(new[] 
                            { 
                                $"现状建设用地({SelectedAreaUnit})", 
                                $"现状工业用地({SelectedAreaUnit})",
                                $"现状采矿用地({SelectedAreaUnit})",
                                $"现状盐田({SelectedAreaUnit})",
                                $"现状仓储用地({SelectedAreaUnit})",
                                "现状工业用地率(%)" 
                            });
                        }
                        if (EnableApprovedLand)
                        {
                            headers.Add($"已批建设用地({SelectedAreaUnit})");
                        }
                        if (EnableApprovedNotSupplied)
                        {
                            headers.AddRange(new[] { $"批而未供面积({SelectedAreaUnit})", "批而未供率(%)" });
                        }
                        if (EnableSuppliedLand)
                        {
                            headers.Add($"已供应建设用地({SelectedAreaUnit})");
                        }
                        if (EnableIdleLand)
                        {
                            headers.AddRange(new[] { $"闲置土地面积({SelectedAreaUnit})", "闲置土地率(%)" });
                        }
                        if (EnableSpatialPlanning)
                        {
                            headers.AddRange(new[] 
                            { 
                                $"规划建设用地({SelectedAreaUnit})", 
                                $"规划工矿仓储用地({SelectedAreaUnit})",
                                "规划工业用地率(%)" 
                            });
                        }
                        if (EnableEvidenceData)
                        {
                            headers.Add($"举证面积({SelectedAreaUnit})");
                        }
                        if (EnableSupplyYearData)
                        {
                            headers.Add("尚可供应年限(年)");
                        }

                        // 写入表头
                        for (int i = 0; i < headers.Count; i++)
                        {
                            worksheet.Cells[1, i + 1] = headers[i];
                        }

                        // 写入数据
                        int row = 2;
                        foreach (var result in _checkResults)
                        {
                            int col = 1;
                            worksheet.Cells[row, col++] = result.ParkName;
                            worksheet.Cells[row, col++] = Math.Round(result.ParkArea, DecimalPlaces);

                            if (EnableUrbanBoundary)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.AreaInUrbanBoundary, DecimalPlaces);
                                worksheet.Cells[row, col++] = Math.Round(result.AreaOutUrbanBoundary, DecimalPlaces);
                            }
                            if (EnablePermanentFarmland)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.AreaOnPermanentFarmland, DecimalPlaces);
                            }
                            if (EnableEcoRedline)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.AreaOnEcoRedline, DecimalPlaces);
                            }
                            if (EnableLandSurvey)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.CurrentConstructionLandArea, DecimalPlaces);
                                worksheet.Cells[row, col++] = Math.Round(result.CurrentIndustrialLandArea, DecimalPlaces);
                                worksheet.Cells[row, col++] = Math.Round(result.CurrentMiningLandArea, DecimalPlaces);
                                worksheet.Cells[row, col++] = Math.Round(result.CurrentSaltFieldArea, DecimalPlaces);
                                worksheet.Cells[row, col++] = Math.Round(result.CurrentWarehouseLandArea, DecimalPlaces);
                                worksheet.Cells[row, col++] = Math.Round(result.CurrentIndustrialRate, 2);
                            }
                            if (EnableApprovedLand)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.ApprovedLandArea, DecimalPlaces);
                            }
                            if (EnableApprovedNotSupplied)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.ApprovedNotSuppliedArea, DecimalPlaces);
                                worksheet.Cells[row, col++] = Math.Round(result.ApprovedNotSuppliedRate, 2);
                            }
                            if (EnableSuppliedLand)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.SuppliedLandArea, DecimalPlaces);
                            }
                            if (EnableIdleLand)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.IdleLandArea, DecimalPlaces);
                                worksheet.Cells[row, col++] = Math.Round(result.IdleLandRate, 2);
                            }
                            if (EnableSpatialPlanning)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.PlannedConstructionLandArea, DecimalPlaces);
                                worksheet.Cells[row, col++] = Math.Round(result.PlannedIndustrialLandArea, DecimalPlaces);
                                worksheet.Cells[row, col++] = Math.Round(result.PlannedIndustrialRate, 2);
                            }
                            if (EnableEvidenceData)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.EvidenceArea, DecimalPlaces);
                            }
                            if (EnableSupplyYearData)
                            {
                                worksheet.Cells[row, col++] = Math.Round(result.AvailableSupplyYears, 1);
                            }

                            row++;
                        }

                        // 设置格式
                        var headerRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[1, headers.Count]];
                        headerRange.Font.Bold = true;
                        headerRange.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                        
                        worksheet.Columns.AutoFit();
                        workbook.SaveAs(saveDialog.FileName);
                    }
                    finally
                    {
                        workbook?.Close(false);
                        excelApp?.Quit();
                        
                        if (workbook != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                        if (excelApp != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(excelApp);
                    }
                });

                LogInfo($"报告已导出: {saveDialog.FileName}");
                MessageBox.Show($"报告已导出到:\n{saveDialog.FileName}", "导出成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                LogError($"导出失败: {ex.Message}");
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void ShowHelp()
        {
            var helpText = @"【开发区整合优化核查工具】

功能说明:
基于园区红线，对开发区进行底线类、节约集约类、发展空间类核查分析。
所有面积基于椭球面计算。

参数说明:
1. 园区红线: 开发区/园区范围图层，可选分组字段进行分组统计
2. 底线类核查:
   - 城镇开发边界: 核查园区是否全部位于开发边界内
   - 永久基本农田: 核查园区是否压占永久基本农田
   - 生态保护红线: 核查园区是否压占生态保护红线

3. 节约集约类核查:
   - 变更调查数据: 计算现状建设用地、工业用地等面积及工业用地率
     (建设用地代码: 05/06/07/08/09/10(除1006)/1109)
     (工业用地:0601, 采矿:0602, 盐田:0603, 仓储:0508)
   - 批而未供数据: 计算批而未供率
   - 已批建设用地/已供应数据: 计算供应情况
   - 闲置土地数据: 计算闲置土地率

4. 发展空间类核查:
   - 国土空间规划: 计算规划工业用地率
     (工矿仓储用地代码: 10/11开头)
   - 举证数据: 用于计算尚可供应年限

指标计算公式:
- 现状工业用地率 = 现状工矿仓储用地面积/现状建设用地面积×100%
- 批而未供率 = 批而未供面积/已批准建设用地面积×100%
- 闲置土地率 = 闲置土地面积/已供应建设用地面积×100%
- 规划工业用地率 = 规划工矿仓储用地面积/规划建设用地面积×100%
- 尚可供应年限 = (园区范围-已供应+举证)/(年均供应量)";

            MessageBox.Show(helpText, "帮助", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }
}
