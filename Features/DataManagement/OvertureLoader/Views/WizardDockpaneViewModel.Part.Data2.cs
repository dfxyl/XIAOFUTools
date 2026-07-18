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

        private List<SelectableThemeItem> GetSelectedLeafItems()
        {
            var selectedLeaves = new List<SelectableThemeItem>();
            if (Themes == null) return selectedLeaves;

            Action<SelectableThemeItem> collectSelectedLeaves = null;
            collectSelectedLeaves = (item) =>
            {
                if (item.IsSelectable && item.IsSelected == true) // Only add if explicitly true
                {
                    selectedLeaves.Add(item);
                }
                foreach (var subItem in item.SubItems)
                {
                    collectSelectedLeaves(subItem); // Recurse for sub-items (though current structure is one level deep)
                }
            };

            foreach (var parentItem in Themes)
            {
                // If the parent item itself is selectable (a leaf parent like "Places")
                if (parentItem.IsSelectable && parentItem.IsSelected == true)
                {
                    selectedLeaves.Add(parentItem);
                }
                // Otherwise, or in addition if it has sub-items (which it shouldn't if it's IsSelectable=true as a leaf)
                // Check its sub-items (which are always leaves and IsSelectable=true)
                else if (parentItem.SubItems.Count > 0)
                {
                    foreach (var subItem in parentItem.SubItems)
                    {
                        if (subItem.IsSelected == true) // SubItems are leaves
                        {
                            selectedLeaves.Add(subItem);
                        }
                    }
                }
            }
            return selectedLeaves.Distinct().ToList(); // Ensure distinct items if logic paths overlap
        }

        // Helper to get all actual data type items (leaves)
        private List<SelectableThemeItem> GetAllLeafDataItems()
        {
            var leafItems = new List<SelectableThemeItem>();
            if (Themes == null) return leafItems;

            foreach (var themeItem in Themes)
            {
                if (themeItem.IsSelectable) // It's a leaf parent (e.g., Places)
                {
                    leafItems.Add(themeItem);
                }
                // Add all sub-items, as they are always leaves/actual data types
                leafItems.AddRange(themeItem.SubItems);
            }
            return leafItems.Distinct().ToList(); // Ensure distinct if structure could somehow allow duplicates
        }
    }
}
