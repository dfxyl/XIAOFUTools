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
    internal sealed class LandClassFieldItem
    {
        public string FieldName { get; set; } = string.Empty;
        public string Alias { get; set; } = string.Empty;
        public string FieldType { get; set; } = string.Empty;
        public bool IsEmptyOption { get; set; }

        public string DisplayText
        {
            get
            {
                if (IsEmptyOption)
                {
                    return Alias;
                }

                if (!string.IsNullOrWhiteSpace(Alias))
                {
                    return Alias;
                }

                return FieldName;
            }
        }

        public override string ToString()
        {
            return DisplayText;
        }
    }

    internal sealed partial class LandClassTableDockPaneViewModel : PropertyChangedBase
    {
        private readonly ILandClassReportProfileDialogService _reportProfileDialogService = new LandClassReportProfileDialogService();
        private bool _cancelRequested;
        private bool _isProcessing;
        private bool _isProgressIndeterminate;
        private int _progress;
        private int _decimalPlaces = 2;
        private string _reportYear = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture);
        private string _locationName = string.Empty;
        private string _reportCompany = string.Empty;
        private string _preparedBy = string.Empty;
        private string _reviewedBy = string.Empty;
        private string _reportDate = DateTime.Now.ToString("yyyy年M月d日", CultureInfo.CurrentCulture);
        private string _selectedAreaUnit = "平方米";
        private string _logContent = string.Empty;
        private string _outputFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "地类表输出");
        private FeatureLayer _selectedRedlineLayer;
        private FeatureLayer _selectedClassLayer;
        private LandClassFieldItem _selectedAreaField;
        private LandClassFieldItem _selectedProjectNameField;
        private LandClassFieldItem _selectedGroupField;
        private LandClassFieldItem _selectedPlotNameField;
        private LandClassFieldItem _selectedClassNameField;
        private LandClassFieldItem _selectedOwnerUnitField;
        private LandClassFieldItem _selectedOwnerNatureField;
        private bool _useAutoOwnerMapping = true;
        private bool _useGroupedOutput;
        private string _manualOwnerUnitName = string.Empty;
        private string _manualOwnerNatureName = string.Empty;
        private LandClassReportProfile _selectedReportProfile;
        private readonly RelayCommand _refreshLayersCommand;
        private readonly RelayCommand _browseOutputCommand;
        private readonly RelayCommand _manageReportProfilesCommand;
        private readonly RelayCommand _runCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _showHelpCommand;

        public LandClassTableDockPaneViewModel()
        {
            PolygonLayers = new ObservableCollection<FeatureLayer>();
            RedlineFields = new ObservableCollection<LandClassFieldItem>();
            RedlineOptionalFields = new ObservableCollection<LandClassFieldItem>();
            RedlineNumericFields = new ObservableCollection<LandClassFieldItem>();
            ClassFields = new ObservableCollection<LandClassFieldItem>();
            ReportProfiles = LandClassReportProfileStore.Load();
            AreaUnits = new ObservableCollection<string> { "平方米", "公顷", "亩" };

            _refreshLayersCommand = new RelayCommand(() => RefreshLayers(), () => !IsProcessing);
            _browseOutputCommand = new RelayCommand(() => BrowseOutput(), () => !IsProcessing);
            _manageReportProfilesCommand = new RelayCommand(() => ManageReportProfiles(), () => !IsProcessing);
            _runCommand = new RelayCommand(async () => await ExecuteAsync(), () => CanProcess);
            _cancelCommand = new RelayCommand(() => _cancelRequested = true, () => IsProcessing);
            _showHelpCommand = new RelayCommand(() => ShowHelp());

            SelectedReportProfile = ReportProfiles.FirstOrDefault();
            OutputFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "地类表输出");
        }
    }
}
