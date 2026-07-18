using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    internal sealed partial class LandClassTableDockPaneViewModel
    {

        public ObservableCollection<FeatureLayer> PolygonLayers { get; }

        public ObservableCollection<LandClassFieldItem> RedlineFields { get; }

        public ObservableCollection<LandClassFieldItem> RedlineOptionalFields { get; }

        public ObservableCollection<LandClassFieldItem> RedlineNumericFields { get; }

        public ObservableCollection<LandClassFieldItem> ClassFields { get; }

        public ObservableCollection<LandClassReportProfile> ReportProfiles { get; }

        public ObservableCollection<string> AreaUnits { get; }


        public string OutputFolder
        {
            get => _outputFolder;
            set
            {
                if (SetProperty(ref _outputFolder, value))
                {
                    NotifyPropertyChanged(() => OutputPath);
                    NotifyCanProcessChanged();
                }
            }
        }


        public string OutputPath
        {
            get => OutputFolder;
            set => OutputFolder = value;
        }


        public string ReportYear
        {
            get => _reportYear;
            set
            {
                SetProperty(ref _reportYear, value);
                UpdateSelectedProfile(x => x.ReportYear = value);
            }
        }


        public string LocationName
        {
            get => _locationName;
            set
            {
                SetProperty(ref _locationName, value);
                UpdateSelectedProfile(x => x.LocationName = value);
            }
        }


        public string ReportCompany
        {
            get => _reportCompany;
            set
            {
                SetProperty(ref _reportCompany, value);
                UpdateSelectedProfile(x => x.ReportCompany = value);
            }
        }


        public string PreparedBy
        {
            get => _preparedBy;
            set
            {
                SetProperty(ref _preparedBy, value);
                UpdateSelectedProfile(x => x.PreparedBy = value);
            }
        }


        public string ReviewedBy
        {
            get => _reviewedBy;
            set
            {
                SetProperty(ref _reviewedBy, value);
                UpdateSelectedProfile(x => x.ReviewedBy = value);
            }
        }


        public string ReportDate
        {
            get => _reportDate;
            set => SetProperty(ref _reportDate, value);
        }


        public LandClassReportProfile SelectedReportProfile
        {
            get => _selectedReportProfile;
            set
            {
                if (SetProperty(ref _selectedReportProfile, value) && value != null)
                {
                    ApplyReportProfile(value);
                }
            }
        }


        public FeatureLayer SelectedRedlineLayer
        {
            get => _selectedRedlineLayer;
            set
            {
                SetProperty(ref _selectedRedlineLayer, value);
                LoadRedlineFields();
                NotifyCanProcessChanged();
            }
        }


        public FeatureLayer SelectedClassLayer
        {
            get => _selectedClassLayer;
            set
            {
                SetProperty(ref _selectedClassLayer, value);
                LoadClassFields();
                NotifyCanProcessChanged();
            }
        }


        public LandClassFieldItem SelectedAreaField
        {
            get => _selectedAreaField;
            set
            {
                SetProperty(ref _selectedAreaField, value);
                NotifyCanProcessChanged();
            }
        }


        public LandClassFieldItem SelectedProjectNameField
        {
            get => _selectedProjectNameField;
            set => SetProperty(ref _selectedProjectNameField, value);
        }


        public LandClassFieldItem SelectedGroupField
        {
            get => _selectedGroupField;
            set
            {
                if (SetProperty(ref _selectedGroupField, value))
                {
                    NotifyCanProcessChanged();
                }
            }
        }


        public LandClassFieldItem SelectedPlotNameField
        {
            get => _selectedPlotNameField;
            set
            {
                SetProperty(ref _selectedPlotNameField, value);
                NotifyPropertyChanged(() => UsePlotNameColumn);
            }
        }


        public bool UsePlotNameColumn => SelectedPlotNameField != null && !SelectedPlotNameField.IsEmptyOption;


        public LandClassFieldItem SelectedClassNameField
        {
            get => _selectedClassNameField;
            set
            {
                SetProperty(ref _selectedClassNameField, value);
                NotifyCanProcessChanged();
            }
        }


        public LandClassFieldItem SelectedOwnerUnitField
        {
            get => _selectedOwnerUnitField;
            set => SetProperty(ref _selectedOwnerUnitField, value);
        }


        public LandClassFieldItem SelectedOwnerNatureField
        {
            get => _selectedOwnerNatureField;
            set => SetProperty(ref _selectedOwnerNatureField, value);
        }


        public bool UseAutoOwnerMapping
        {
            get => _useAutoOwnerMapping;
            set
            {
                if (SetProperty(ref _useAutoOwnerMapping, value))
                {
                    NotifyPropertyChanged(() => IsAutoOwnerMapping);
                    NotifyPropertyChanged(() => UseManualOwnerMapping);
                    NotifyPropertyChanged(() => IsManualOwnerMapping);
                    NotifyCanProcessChanged();
                }
            }
        }


        public bool IsAutoOwnerMapping => UseAutoOwnerMapping;


        public bool UseManualOwnerMapping
        {
            get => !UseAutoOwnerMapping;
            set
            {
                if (UseManualOwnerMapping == value)
                {
                    return;
                }

                UseAutoOwnerMapping = !value;
            }
        }


        public bool IsManualOwnerMapping => !UseAutoOwnerMapping;


        public bool UseGroupedOutput
        {
            get => _useGroupedOutput;
            set
            {
                if (SetProperty(ref _useGroupedOutput, value))
                {
                    NotifyPropertyChanged(() => IsGroupedOutputVisible);
                    NotifyCanProcessChanged();
                }
            }
        }


        public bool IsGroupedOutputVisible => UseGroupedOutput;


        public string ManualOwnerUnitName
        {
            get => _manualOwnerUnitName;
            set => SetProperty(ref _manualOwnerUnitName, value);
        }


        public string ManualOwnerNatureName
        {
            get => _manualOwnerNatureName;
            set => SetProperty(ref _manualOwnerNatureName, value);
        }


        public string SelectedAreaUnit
        {
            get => _selectedAreaUnit;
            set
            {
                SetProperty(ref _selectedAreaUnit, value);
                DecimalPlaces = value == "平方米" ? 2 : 4;
            }
        }


        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set => SetProperty(ref _decimalPlaces, Math.Max(0, Math.Min(6, value)));
        }


        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                SetProperty(ref _isProcessing, value);
                NotifyCanProcessChanged();
            }
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


        public bool CanProcess =>
            !IsProcessing &&
            SelectedRedlineLayer != null &&
            SelectedClassLayer != null &&
            SelectedClassNameField != null &&
            !string.IsNullOrWhiteSpace(OutputFolder) &&
            (!UseGroupedOutput || (SelectedGroupField != null && !SelectedGroupField.IsEmptyOption));


        public ICommand RefreshLayersCommand => _refreshLayersCommand;

        public ICommand BrowseOutputCommand => _browseOutputCommand;

        public ICommand ManageReportProfilesCommand => _manageReportProfilesCommand;

        public ICommand RunCommand => _runCommand;

        public ICommand CancelCommand => _cancelCommand;

        public ICommand ShowHelpCommand => _showHelpCommand;

    }
}
