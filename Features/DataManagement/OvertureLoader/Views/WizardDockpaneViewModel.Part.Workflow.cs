using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Geometry;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Data;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Application;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Infrastructure;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Services;
using Microsoft.Win32;
using ArcGIS.Desktop.Catalog;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.IO;
using System.Threading;
using ArcGIS.Desktop.Core; // Added for Project.Current
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Views
{
    internal partial class WizardDockpaneViewModel
    {

        // Protected constructor for ArcGIS Pro framework runtime instantiation
        // Removing this constructor as the public one handles both design-time and runtime.
        /*
        protected WizardDockpaneViewModel() 
        {
            // This is the primary runtime constructor expected by ArcGIS Pro.
            InitializeViewModelForRuntime();
        }
        */

        private void InitializeViewModelForRuntime()
        {
            System.Diagnostics.Debug.WriteLine("InitializeViewModelForRuntime executing...");
            var mapCleanupService = new ArcGisOvertureMapMemberCleanupService();
            _dataProcessor = new DataProcessor(mapCleanupService);
            _releaseClient = OvertureReleaseClient.CreateDefault();
            _dataReplacementService = new OvertureDataReplacementService(
                new FileSystemOvertureDataFolderStore(),
                mapCleanupService);
            _mfcDataFolderInspector = new OvertureMfcDataFolderInspector();

            LoadDataCommand = new RelayCommand(async () => await LoadOvertureDataAsync(), () => GetSelectedLeafItems().Count > 0);
            ShowThemeInfoCommand = new RelayCommand(() => ShowThemeInfo(), () => SelectedItemForPreview != null);
            SetCustomExtentCommand = new RelayCommand(() => SetCustomExtent(), () => UseCustomExtent);
            BrowseMfcLocationCommand = new RelayCommand(() => BrowseMfcLocation());
            BrowseDataLocationCommand = new RelayCommand(() => BrowseDataLocation());
            BrowseCustomDataFolderCommand = new RelayCommand(() => BrowseCustomDataFolder());
            CreateMfcCommand = new RelayCommand(async () => await CreateMfcAsync(), () => (UsePreviouslyLoadedData && !string.IsNullOrEmpty(_lastLoadedDataPath)) || (UseCustomDataFolder && !string.IsNullOrEmpty(CustomDataFolderPath)));
            GoToCreateMfcTabCommand = new RelayCommand(() => ShowCreateMfcTab(), () => true);
            CancelCommand = new RelayCommand(() =>
            {
                if (_cts != null && !_cts.IsCancellationRequested) { _cts.Cancel(); AddToLog("用户取消了操作。"); }
                ResetState(); AddToLog("插件状态已重置。");
                try { this.Hide(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error closing dockpane: {ex.Message}"); }
            });
            SelectAllCommand = new RelayCommand(
                () => IsSelectAllChecked = !IsSelectAllChecked, // Action: Toggle the IsSelectAllChecked property
                () => Themes != null && Themes.Any(t => t.IsSelectable || t.SubItems.Any()) // CanExecute: If there are any selectable themes
            );
            ShowHelpCommand = new RelayCommand(() => ShowHelp());

            CustomExtentTool.ExtentCreatedStatic += OnExtentCreated;

            Themes = new ObservableCollection<SelectableThemeItem>();
            LogOutput = new StringBuilder();
            LogOutput.AppendLine("Initializing WizardDockpaneViewModel...");
            LogOutputText = LogOutput.ToString(); // Initialize LogOutputText

            // Set default paths immediately, they might be updated by InitializeAsync if LatestRelease changes
            var defaultBasePath = DeterminedDefaultMfcBasePath;
            DataOutputPath = Path.Combine(defaultBasePath, "Data", LatestRelease ?? "latest");
            MfcOutputPath = Path.Combine(defaultBasePath, "Connections");
            NotifyPropertyChanged(nameof(DataOutputPath)); // Notify for initial value
            NotifyPropertyChanged(nameof(MfcOutputPath)); // Notify for initial value

            IsLoading = true; // Set IsLoading to true before starting async init
            StatusText = "Initializing..."; // Initial status
            NotifyPropertyChanged(nameof(IsLoading));
            NotifyPropertyChanged(nameof(StatusText));
            _isSelectAllChecked = false; // Initialize Select All state

            // Do not call InitializeAsync() here. The base DockPane class
            // will invoke it automatically after construction. Calling it
            // explicitly would cause double initialization which leads to
            // errors such as attempting to open the DuckDB connection twice.
        }

        /// <summary>
        /// Provides periodic heartbeat feedback during long-running operations
        /// </summary>
        private async Task StartHeartbeatAsync(string itemName, CancellationToken cancellationToken)
        {
            int heartbeatCount = 0;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(10000, cancellationToken); // Every 10 seconds
                    heartbeatCount++;

                    var timeElapsed = heartbeatCount * 10;
                    StatusText = $"Still loading {itemName}... ({timeElapsed}s elapsed)";
                    AddToLog($"⏱️ Still working on {itemName} ({timeElapsed} seconds elapsed)...");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when operation completes
            }
        }

        /// <summary>
        /// 估算处理时间
        /// </summary>
        private string EstimateProcessingTime(int itemCount)
        {
            // 基于经验的时间估算：每个数据类型大约需要1-3分钟
            int estimatedMinutes = Math.Max(1, itemCount * 2);
            if (estimatedMinutes < 60)
            {
                return $"{estimatedMinutes}";
            }
            else
            {
                int hours = estimatedMinutes / 60;
                int minutes = estimatedMinutes % 60;
                return minutes > 0 ? $"{hours}小时{minutes}" : $"{hours}小时";
            }
        }
    }
}
