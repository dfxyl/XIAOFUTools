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

        private void ResetState()
        {
            // Clear theme selections
            foreach (var themeItem in Themes)
            {
                // Temporarily unsubscribe to avoid multiple event triggers
                if (themeItem.IsSelectable) themeItem.SelectionChanged -= OnLeafThemeSelectionChanged; // Only if it's a leaf
                else foreach (var subItem in themeItem.SubItems) subItem.SelectionChanged -= OnLeafThemeSelectionChanged;

                themeItem.IsSelected = false;
                foreach (var subItem in themeItem.SubItems) subItem.IsSelected = false;

                if (themeItem.IsSelectable) themeItem.SelectionChanged += OnLeafThemeSelectionChanged; // Only if it's a leaf
                else foreach (var subItem in themeItem.SubItems) subItem.SelectionChanged += OnLeafThemeSelectionChanged;
            }

            // Clear selected themes list - no longer needed
            // _selectedThemes.Clear();
            // NotifyPropertyChanged(nameof(SelectedThemes));

            // Reset other properties
            SelectedTheme = null;
            SelectedTabIndex = 0; // Switch back to the first tab
            _isSelectAllChecked = false; // Explicitly reset, though UpdateIsSelectAllCheckedStatus will also do it.
            NotifyPropertyChanged(nameof(IsSelectAllChecked));

            // Reset extent options
            UseCurrentMapExtent = true;
            UseCustomExtent = false;
            CustomExtent = null;

            // Reset data and MFC options
            var defaultBasePath = DeterminedDefaultMfcBasePath;

            // Reset data options
            DataOutputPath = Path.Combine(
                defaultBasePath,
                "Data",
                LatestRelease ?? "latest"
            );

            // Reset MFC options
            IsSharedMfc = true;
            MfcOutputPath = Path.Combine(
                defaultBasePath,
                "Connections"
            );

            // Reset data source options for MFC
            UsePreviouslyLoadedData = true;
            UseCustomDataFolder = false;
            CustomDataFolderPath = null;
            _lastLoadedDataPath = null;

            // Reset progress and status
            ProgressValue = 0;
            StatusText = "准备加载 Overture Maps 数据";

            // Clear log but keep initialization messages
            LogOutput = new();
            LogOutput.AppendLine("初始化完成。准备进行新查询。");
            LogOutputText = LogOutput.ToString();
            NotifyPropertyChanged(nameof(LogOutputText));

            // Raise can execute changed on commands
            (LoadDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ShowThemeInfoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SetCustomExtentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SelectAllCommand as RelayCommand)?.RaiseCanExecuteChanged();

            UpdateIsSelectAllCheckedStatus(); // Ensure Select All checkbox is correctly updated
            System.Diagnostics.Debug.WriteLine("Add-in state has been reset");
        }

        private static string MakeFriendlyName(string s3TypeName) // CA1822 Made static
        {
            if (string.IsNullOrEmpty(s3TypeName)) return s3TypeName;
            // Replace underscores with spaces and capitalize words
            var parts = s3TypeName.Split(['_'], StringSplitOptions.RemoveEmptyEntries); // IDE0300 / CA1861 Simplified array
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpper(parts[i][0]) + (parts[i].Length > 1 ? parts[i][1..] : ""); // IDE0057 Substring simplified
            }
            return string.Join(" ", parts);
        }

        private void InitializeThemes()
        {
            var themesCollection = new ObservableCollection<SelectableThemeItem>();
            foreach (var kvp in _overtureS3ThemeTypes)
            {
                string s3ParentThemeKey = kvp.Key; // e.g., "base", "buildings"
                string s3SubTypesString = kvp.Value;
                string[] s3SubTypes = s3SubTypesString.Split(',');

                string parentDisplayName = _parentThemeDisplayNames.TryGetValue(s3ParentThemeKey, out var dn) ? dn : MakeFriendlyName(s3ParentThemeKey);

                // Parent item: DisplayName, ActualType (itself, for grouping), ParentS3Theme (itself)
                // A parent is a leaf (and thus selectable) if it has no distinct sub-types.
                bool parentIsLeaf = s3SubTypes.Length == 1 && s3SubTypes[0] == s3ParentThemeKey;
                // or s3SubTypes.Length == 0 (though current data always has types)

                var parentItem = new SelectableThemeItem(parentDisplayName, s3ParentThemeKey, s3ParentThemeKey, parentIsLeaf);

                if (!parentIsLeaf && s3SubTypes.Length > 0)
                {
                    foreach (var s3SubType in s3SubTypes)
                    {
                        string subTypeTrimmed = s3SubType.Trim();
                        string subItemDisplayName = MakeFriendlyName(subTypeTrimmed);
                        // Sub-item: DisplayName, ActualType=s3SubType, ParentS3Theme=s3ParentThemeKey. Sub-items are always leaves.
                        var subItem = new SelectableThemeItem(subItemDisplayName, subTypeTrimmed, s3ParentThemeKey, true);
                        subItem.Parent = parentItem; // Set the parent property for the sub-item
                        subItem.SelectionChanged += OnLeafThemeSelectionChanged; // ViewModel listens to leaves
                        parentItem.SubItems.Add(subItem);
                    }
                }
                else // Parent is a leaf node
                {
                    // Ensure its ActualType is correctly set if it was determined to be a leaf
                    if (s3SubTypes.Any()) parentItem.ActualType = s3SubTypes[0].Trim();
                    parentItem.SelectionChanged += OnLeafThemeSelectionChanged; // ViewModel listens to leaves
                }
                themesCollection.Add(parentItem);
            }
            Themes = themesCollection; // Assign to the public property
            NotifyPropertyChanged(nameof(Themes));
            UpdateIsSelectAllCheckedStatus(); // Set initial state of SelectAll checkbox
            (SelectAllCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Update command state
        }
    }
}
