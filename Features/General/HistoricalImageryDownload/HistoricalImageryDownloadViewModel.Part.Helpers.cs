#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;
using XIAOFUTools.Features.General.HistoricalImageryDownload.Services;

namespace XIAOFUTools.Features.General.HistoricalImageryDownload
{
    internal sealed partial class HistoricalImageryDownloadViewModel
    {

        public async Task InitializeAsync()
        {
            if (string.IsNullOrWhiteSpace(OutputFolderPath))
            {
                var projectPath = ArcGIS.Desktop.Core.Project.Current?.HomeFolderPath
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                OutputFolderPath = projectPath;
            }

            await LoadFeatureLayersAsync();
            InitializeDefaultSpatialReference();
            SelectedZoomLevel = GetDefaultZoomLevel();
        }


        private void InitializeDefaultSpatialReference()
        {
            _selectedOutputSpatialReference = MapView.Active?.Map?.SpatialReference;
            OutputSpatialReferenceDisplay = _selectedOutputSpatialReference?.Name ?? "未设置";
        }


        private async Task AddOutputToMapAsync(string outputFilePath)
        {
            await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null)
                {
                    return;
                }

                LayerFactory.Instance.CreateLayer(
                    new Uri(outputFilePath),
                    mapView.Map,
                    layerName: Path.GetFileNameWithoutExtension(outputFilePath));
            });
        }


        private async Task ReleaseOutputFileLocksAsync(string outputFilePath)
        {
            await QueuedTask.Run(() =>
            {
                var mapView = MapView.Active;
                var map = mapView?.Map;
                if (map == null)
                {
                    return;
                }

                var lockedLayers = map.GetLayersAsFlattenedList()
                    .Where(layer => HistoricalLayerUriMatcher.IsOutputFamilyMatch(outputFilePath, layer.URI))
                    .ToList();

                foreach (var layer in lockedLayers)
                {
                    map.RemoveLayer(layer);
                }

                if (lockedLayers.Count > 0)
                {
                    mapView?.Redraw(true);
                }
            });
        }


        private void PickSpatialReference()
        {
            var spatialReference = CoordinateSystemSelector.ShowCoordinateSystemDialog();
            if (spatialReference != null)
            {
                _selectedOutputSpatialReference = spatialReference;
                OutputSpatialReferenceDisplay = spatialReference.Name;
            }
        }


        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

    }
}
