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

        private void UpdateIsSelectAllCheckedStatus()
        {
            if (Themes == null || !Themes.Any())
            {
                // Use SetProperty to ensure UI is notified if it changes.
                SetProperty(ref _isSelectAllChecked, false, nameof(IsSelectAllChecked));
                return;
            }

            bool allDataTypesSelected = true;
            bool anySelectableLeafExists = false;

            // We need to check all actual data types (leaf nodes)
            List<SelectableThemeItem> allLeafItems = GetAllLeafDataItems();

            if (!allLeafItems.Any())
            {
                SetProperty(ref _isSelectAllChecked, false, nameof(IsSelectAllChecked));
                return;
            }

            foreach (var leafItem in allLeafItems)
            {
                anySelectableLeafExists = true; // We know it exists if allLeafItems is not empty
                if (leafItem.IsSelected != true) // Check for explicitly true
                {
                    allDataTypesSelected = false;
                    break;
                }
            }

            SetProperty(ref _isSelectAllChecked, anySelectableLeafExists && allDataTypesSelected, nameof(IsSelectAllChecked));
        }
    }
}
