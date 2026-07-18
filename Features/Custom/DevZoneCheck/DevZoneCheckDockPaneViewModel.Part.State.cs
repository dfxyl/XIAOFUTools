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
    internal partial class DevZoneCheckDockPaneViewModel
    {
        public bool CancelRequested
        {
            get => _cancelRequested;
            set => SetProperty(ref _cancelRequested, value);
        }
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
        public bool HasResult
        {
            get => _hasResult;
            set => SetProperty(ref _hasResult, value);
        }
        public ObservableCollection<FeatureLayer> PolygonLayers
        {
            get => _polygonLayers;
            set => SetProperty(ref _polygonLayers, value);
        }
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
        public ObservableCollection<string> ParkFields
        {
            get => _parkFields;
            set => SetProperty(ref _parkFields, value);
        }
        public string SelectedParkField
        {
            get => _selectedParkField;
            set => SetProperty(ref _selectedParkField, value);
        }
        public bool EnableUrbanBoundary
        {
            get => _enableUrbanBoundary;
            set => SetProperty(ref _enableUrbanBoundary, value);
        }
        public FeatureLayer UrbanBoundaryLayer
        {
            get => _urbanBoundaryLayer;
            set => SetProperty(ref _urbanBoundaryLayer, value);
        }
        public bool EnablePermanentFarmland
        {
            get => _enablePermanentFarmland;
            set => SetProperty(ref _enablePermanentFarmland, value);
        }
        public FeatureLayer PermanentFarmlandLayer
        {
            get => _permanentFarmlandLayer;
            set => SetProperty(ref _permanentFarmlandLayer, value);
        }
        public bool EnableEcoRedline
        {
            get => _enableEcoRedline;
            set => SetProperty(ref _enableEcoRedline, value);
        }
        public FeatureLayer EcoRedlineLayer
        {
            get => _ecoRedlineLayer;
            set => SetProperty(ref _ecoRedlineLayer, value);
        }
        public bool EnableLandSurvey
        {
            get => _enableLandSurvey;
            set
            {
                SetProperty(ref _enableLandSurvey, value);
                if (value) LoadLandSurveyFields();
            }
        }
        public FeatureLayer LandSurveyLayer
        {
            get => _landSurveyLayer;
            set
            {
                SetProperty(ref _landSurveyLayer, value);
                LoadLandSurveyFields();
            }
        }
        public ObservableCollection<string> LandSurveyFields
        {
            get => _landSurveyFields;
            set => SetProperty(ref _landSurveyFields, value);
        }
        public string SelectedLandSurveyField
        {
            get => _selectedLandSurveyField;
            set => SetProperty(ref _selectedLandSurveyField, value);
        }
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
        public FeatureLayer ApprovedNotSuppliedLayer
        {
            get => _approvedNotSuppliedLayer;
            set => SetProperty(ref _approvedNotSuppliedLayer, value);
        }
        public bool EnableApprovedLand
        {
            get => _enableApprovedLand;
            set => SetProperty(ref _enableApprovedLand, value);
        }
        public FeatureLayer ApprovedLandLayer
        {
            get => _approvedLandLayer;
            set => SetProperty(ref _approvedLandLayer, value);
        }
        public bool EnableSuppliedLand
        {
            get => _enableSuppliedLand;
            set
            {
                SetProperty(ref _enableSuppliedLand, value);
                NotifyPropertyChanged(nameof(ShowEvidenceDataOption));
            }
        }
        public FeatureLayer SuppliedLandLayer
        {
            get => _suppliedLandLayer;
            set => SetProperty(ref _suppliedLandLayer, value);
        }
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
        public FeatureLayer IdleLandLayer
        {
            get => _idleLandLayer;
            set => SetProperty(ref _idleLandLayer, value);
        }
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
        public FeatureLayer SpatialPlanningLayer
        {
            get => _spatialPlanningLayer;
            set
            {
                SetProperty(ref _spatialPlanningLayer, value);
                LoadSpatialPlanningFields();
            }
        }
        public ObservableCollection<string> SpatialPlanningFields
        {
            get => _spatialPlanningFields;
            set => SetProperty(ref _spatialPlanningFields, value);
        }
        public string SelectedSpatialPlanningField
        {
            get => _selectedSpatialPlanningField;
            set => SetProperty(ref _selectedSpatialPlanningField, value);
        }
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
        public FeatureLayer EvidenceDataLayer
        {
            get => _evidenceDataLayer;
            set => SetProperty(ref _evidenceDataLayer, value);
        }

        // 举证数据选项是否显示（需要规划和已供应都勾选）
        public bool ShowEvidenceDataOption => EnableSpatialPlanning && EnableSuppliedLand;
        public ObservableCollection<string> AreaUnits
        {
            get => _areaUnits;
            set => SetProperty(ref _areaUnits, value);
        }
        public string SelectedAreaUnit
        {
            get => _selectedAreaUnit;
            set => SetProperty(ref _selectedAreaUnit, value);
        }
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set => SetProperty(ref _decimalPlaces, value);
        }

        public ObservableCollection<string> AreaCalculationMethods { get; }
        public string SelectedAreaCalculationMethod
        {
            get => _selectedAreaCalculationMethod;
            set => SetProperty(ref _selectedAreaCalculationMethod, value);
        }
        public bool EnableSupplyYearData
        {
            get => _enableSupplyYearData;
            set => SetProperty(ref _enableSupplyYearData, value);
        }
        public FeatureLayer SupplyYearDataLayer
        {
            get => _supplyYearDataLayer;
            set => SetProperty(ref _supplyYearDataLayer, value);
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
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        public ICommand RefreshLayersCommand { get; }
        public ICommand AutoMatchLayersCommand { get; }
        public ICommand RunCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand ExportReportCommand { get; }
    }
}
