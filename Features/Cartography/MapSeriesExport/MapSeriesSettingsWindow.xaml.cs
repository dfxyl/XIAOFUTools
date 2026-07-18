using System;
using System.Collections.Generic;
using System.Windows;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Cartography.MapSeriesExport.Core;
using XIAOFUTools.Features.Cartography.MapSeriesExport.Infrastructure;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    public partial class MapSeriesSettingsWindow : Window
    {
        private readonly MapSeriesSettingsStore _settingsStore = new();
        private Layout _currentLayout;
        private readonly Dictionary<string, FeatureLayer> _intersectLayersByName =
            new(StringComparer.OrdinalIgnoreCase);
        private string _pendingIntersectLayerName;
        private string _pendingIntersectFieldName;

        public MapSeriesSettingsWindow(CoordinateTableSettings existingSettings = null)
        {
            InitializeComponent();
            InitializeAreaUnits();
            LoadMapFramesAsync();
            LoadIntersectLayersAsync();

            var settings = existingSettings ?? _settingsStore.Load();
            if (settings != null)
            {
                LoadSettings(settings);
            }

            LoadIntersectLayersAsync();
        }
    }
}
