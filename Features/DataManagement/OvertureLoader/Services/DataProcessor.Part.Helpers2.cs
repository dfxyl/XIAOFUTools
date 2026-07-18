using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using DuckDB.NET.Data;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Services
{
    public partial class DataProcessor
    {

        private async Task DeleteParquetFileAsync(string parquetPath)
        {
            if (!File.Exists(parquetPath))
            {
                System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: File does not exist, no action needed: {parquetPath}");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: Starting deletion process for: {parquetPath}");
                await _mapMemberCleanupService.RemoveFromProjectMapsUsingFileAsync(
                    parquetPath,
                    CancellationToken.None);

                System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: Layers removed. Waiting for file locks to release...");
                await Task.Delay(100); // Reduced delay

                System.Diagnostics.Debug.WriteLine("DeleteParquetFileAsync: Invoking GC.Collect and WaitForPendingFinalizers.");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(100); // Short additional delay after GC

                bool deleted = false;
                int gpRetryCount = 2; // Try GP delete a couple of times

                for (int i = 0; i < gpRetryCount && !deleted; i++)
                {
                    if (i > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: Retrying GP delete, attempt {i + 1} after delay...");
                        await Task.Delay(200 * i); // Much shorter delays for GP retries
                    }
                    System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: Attempting GP delete (attempt {i + 1}/{gpRetryCount}): {parquetPath}");

                    await QueuedTask.Run(async () =>
                    {
                        try
                        {
                            var env = Geoprocessing.MakeEnvironmentArray(overwriteoutput: true);
                            var parameters = Geoprocessing.MakeValueArray(parquetPath);
                            var result = await Geoprocessing.ExecuteToolAsync(
                                "management.Delete",
                                parameters,
                                env,
                                flags: ArcGIS.Desktop.Core.Geoprocessing.GPExecuteToolFlags.Default);

                            if (!result.IsFailed)
                            {
                                System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: Successfully deleted via geoprocessing: {parquetPath}");
                                deleted = true;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: GP delete failed (attempt {i + 1}). Messages:");
                                foreach (var msg in result.Messages)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  GP Error: {msg.Text}");
                                }
                            }
                        }
                        catch (Exception gpEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: Error in geoprocessing deletion (attempt {i + 1}): {gpEx.Message}");
                        }
                    });
                }

                if (!deleted && File.Exists(parquetPath))
                {
                    System.Diagnostics.Debug.WriteLine("DeleteParquetFileAsync: GP deletion failed or file still exists. Trying direct file operations as fallback...");
                    int fileDeleteRetryCount = 3;
                    Exception lastFileDeleteException = null;

                    for (int i = 0; i < fileDeleteRetryCount && !deleted; i++)
                    {
                        try
                        {
                            File.Delete(parquetPath);
                            deleted = true;
                            System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: Successfully deleted via direct file operation: {parquetPath}");
                        }
                        catch (IOException ex)
                        {
                            lastFileDeleteException = ex;
                            if (i < fileDeleteRetryCount - 1)
                            {
                                int delay = (i + 1) * 100; // Much shorter delays
                                System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: Direct delete attempt {i + 1} failed (file locked), waiting {delay}ms to retry: {parquetPath}");
                                await Task.Delay(delay);
                            }
                        }
                    }

                    if (!deleted && (Exception)null != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: All direct file delete attempts failed. Last error: {((Exception)null).Message}");
                        // No longer throwing here, will fall through to the final message box if still not deleted.
                    }
                }

                if (!deleted && File.Exists(parquetPath))
                {
                    string errorMsg = $"删除文件失败 {parquetPath}: 多次尝试后文件仍被锁定。\n\n请尝试关闭可能正在使用此数据的ArcGIS Pro项目，或手动删除该文件。";
                    System.Diagnostics.Debug.WriteLine(errorMsg);
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(errorMsg, "文件删除错误",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
                else if (!File.Exists(parquetPath))
                {
                    System.Diagnostics.Debug.WriteLine($"DeleteParquetFileAsync: Confirmed file successfully deleted: {parquetPath}");
                }
            }
            catch (Exception ex) // Catch unexpected errors in the overall deletion process
            {
                string errorMsg = $"删除文件 {parquetPath} 时发生意外错误: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(errorMsg);
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(errorMsg, "文件删除错误",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Adds all pending layers to the map in optimal stacking order (polygons → lines → points)
        /// </summary>
        public async Task AddAllLayersToMapAsync(IProgress<string> progress = null)
        {
            if (!_pendingLayers.Any())
            {
                progress?.Report("No layers to add to map.");
                return;
            }

            // Sort all layers by stacking priority (higher priority = higher in Contents panel = drawn on top)
            // We want: Points on top (drawn last), Lines in middle, Polygons on bottom (drawn first)
            var sortedLayers = _pendingLayers
                .OrderByDescending(layer => layer.StackingPriority) // Higher priority first
                .ThenBy(layer => layer.ParentTheme) // Secondary sort by theme for consistency
                .ThenBy(layer => layer.ActualType)  // Tertiary sort by actual type
                .ToList();

            progress?.Report($"Preparing to add {sortedLayers.Count} layers with optimal stacking order...");
            System.Diagnostics.Debug.WriteLine($"Adding {sortedLayers.Count} layers in optimal stacking order (points → lines → polygons):");

            foreach (var layer in sortedLayers)
            {
                System.Diagnostics.Debug.WriteLine($"  {layer.LayerName} ({layer.GeometryType}, priority {layer.StackingPriority})");
            }

            // Group layers by geometry type for progress reporting
            var pointLayers = sortedLayers.Where(l => l.StackingPriority == 3).ToList();
            var lineLayers = sortedLayers.Where(l => l.StackingPriority == 2).ToList();
            var polygonLayers = sortedLayers.Where(l => l.StackingPriority == 1).ToList();

            progress?.Report($"Layer summary: {pointLayers.Count} point layers, {lineLayers.Count} line layers, {polygonLayers.Count} polygon layers");

            // Use individual layer creation for single layers, bulk creation for multiple layers
            if (sortedLayers.Count == 1)
            {
                progress?.Report("Single layer detected, using individual layer creation...");
                System.Diagnostics.Debug.WriteLine("Using individual layer creation for single layer (bulk creation may not work reliably with 1 layer)");
                await FallbackToIndividualLayerCreation(sortedLayers, progress);
            }
            else
            {
                List<LayerCreationInfo> missingLayersForIndividualCreation = null;
                
                try
                {
                    progress?.Report("Starting bulk layer creation process...");

                    await QueuedTask.Run(() =>
                    {
                        var map = MapView.Active?.Map;
                        if (map == null)
                        {
                            progress?.Report("Error: No active map found to add layers.");
                            System.Diagnostics.Debug.WriteLine("AddAllLayersToMapAsync: No active map found.");
                            return;
                        }

                        progress?.Report("Validating layer files...");

                        // Use BulkMapMemberCreationParams for optimal performance
                        var uris = new List<Uri>();
                        var layerNames = new List<string>();
                        int validFiles = 0;

                        foreach (var layerInfo in sortedLayers)
                        {
                            if (File.Exists(layerInfo.FilePath))
                            {
                                uris.Add(new Uri(layerInfo.FilePath));
                                layerNames.Add(layerInfo.LayerName);
                                validFiles++;
                                System.Diagnostics.Debug.WriteLine($"Valid file for {layerInfo.LayerName}: {layerInfo.FilePath}");
                            }
                            else
                            {
                                progress?.Report($"Warning: File not found for {layerInfo.LayerName}");
                                System.Diagnostics.Debug.WriteLine($"Warning: File not found for layer {layerInfo.LayerName}: {layerInfo.FilePath}");
                            }
                        }

                        progress?.Report($"Validated {validFiles} layer files. Creating layers...");

                        if (uris.Any())
                        {
                            progress?.Report("Executing bulk layer creation (this may take a moment)...");

                            // Create layers using LayerFactory with URI enumeration for optimal performance
                            System.Diagnostics.Debug.WriteLine($"Attempting to create {uris.Count} layers via bulk creation...");
                            var layers = LayerFactory.Instance.CreateLayers(uris, map);

                            progress?.Report($"Bulk creation completed. Applying settings to {layers.Count} layers...");
                            System.Diagnostics.Debug.WriteLine($"Bulk creation result: {layers.Count} layers created from {uris.Count} URIs");

                            // Apply custom names and settings to the created layers
                            for (int i = 0; i < layers.Count && i < layerNames.Count; i++)
                            {
                                if (layers[i] != null)
                                {
                                    layers[i].SetName(layerNames[i]);

                                    // Apply cache and feature reduction settings
                                    if (layers[i] is FeatureLayer featureLayer)
                                    {
                                        ApplyLayerSettings(featureLayer);
                                    }

                                    // Report progress every few layers to keep UI responsive
                                    if (i % 3 == 0 || i == layers.Count - 1)
                                    {
                                        progress?.Report($"Configured layer {i + 1} of {layers.Count}: {layerNames[i]}");
                                    }
                                    System.Diagnostics.Debug.WriteLine($"Successfully added layer: {layerNames[i]}");
                                }
                            }

                            progress?.Report($"✅ Successfully added {layers.Count} layers with optimal stacking order!");
                            System.Diagnostics.Debug.WriteLine($"Bulk layer creation completed. Added {layers.Count} layers.");
                            
                            // If bulk creation didn't create all expected layers, fall back to individual creation for missing ones
                            if (layers.Count < uris.Count)
                            {
                                progress?.Report($"Some layers missing from bulk creation ({layers.Count}/{uris.Count}). Creating missing layers individually...");
                                System.Diagnostics.Debug.WriteLine($"Bulk creation incomplete: {layers.Count}/{uris.Count} layers created. Falling back for missing layers...");
                                
                                // Find which layers were not created and create them individually
                                var createdLayerNames = layers.Select(l => l.Name).ToHashSet();
                                var missingLayers = sortedLayers.Where(layerInfo => 
                                    File.Exists(layerInfo.FilePath) && 
                                    !createdLayerNames.Contains(layerInfo.LayerName)).ToList();
                                
                                // Store missing layers for individual creation outside QueuedTask
                                missingLayersForIndividualCreation = missingLayers;
                            }
                        }
                    });
                    
                    // Create missing layers individually outside QueuedTask
                    if (missingLayersForIndividualCreation?.Any() == true)
                    {
                        await FallbackToIndividualLayerCreation(missingLayersForIndividualCreation, progress);
                    }
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error during bulk layer creation: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Error in AddAllLayersToMapAsync: {ex.Message}");

                    // Fallback to individual layer creation if bulk creation fails
                    progress?.Report("Falling back to individual layer creation...");
                    await FallbackToIndividualLayerCreation(sortedLayers, progress);
                }
            }
            
            // Clear pending layers after processing
            _pendingLayers.Clear();
        }

        /// <summary>
        /// Clears any pending layers (useful for cleanup or reset operations)
        /// </summary>
        public void ClearPendingLayers()
        {
            _pendingLayers.Clear();
        }

        public void Dispose()
        {
            _connection?.Dispose();
            _pendingLayers?.Clear();
            GC.SuppressFinalize(this);
        }

        private static async Task AddLayerToMapAsync(string parquetFilePath, string layerName, IProgress<string> progress = null)
        {
            progress?.Report($"Adding layer {layerName} to map from {Path.GetFileName(parquetFilePath)}");
            System.Diagnostics.Debug.WriteLine($"Attempting to add layer: {layerName} from path: {parquetFilePath}");

            await QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map == null)
                {
                    progress?.Report("Error: No active map found to add layer.");
                    System.Diagnostics.Debug.WriteLine("AddLayerToMapAsync: No active map found.");
                    return;
                }

                if (!File.Exists(parquetFilePath))
                {
                    progress?.Report($"Error: Parquet file not found at {parquetFilePath}");
                    System.Diagnostics.Debug.WriteLine($"AddLayerToMapAsync: Parquet file not found at {parquetFilePath}");
                    return;
                }

                try
                {
                    // Create a URI for the Parquet file
                    Uri dataUri = new(parquetFilePath);

                    // Create the layer using LayerFactory
                    Layer newLayer = LayerFactory.Instance.CreateLayer(dataUri, map, layerName: layerName);

                    if (newLayer == null)
                    {
                        progress?.Report($"Error: Could not create layer for {layerName}.");
                        System.Diagnostics.Debug.WriteLine($"AddLayerToMapAsync: LayerFactory returned null for {parquetFilePath}");
                        return;
                    }

                    // Attempt to set cache settings to None for performance with potentially large/dynamic datasets
                    if (newLayer is FeatureLayer featureLayerForCacheSettings)
                    {
                        try
                        {
                            if (featureLayerForCacheSettings.GetDefinition() is CIMFeatureLayer layerDef)
                            {
                                System.Diagnostics.Debug.WriteLine($"Setting cache options for layer: {featureLayerForCacheSettings.Name}");
                                layerDef.DisplayCacheType = ArcGIS.Core.CIM.DisplayCacheType.None;
                                layerDef.FeatureCacheType = ArcGIS.Core.CIM.FeatureCacheType.None;
                                featureLayerForCacheSettings.SetDefinition(layerDef);
                                System.Diagnostics.Debug.WriteLine($"Successfully set DisplayCacheType and FeatureCacheType to None for {featureLayerForCacheSettings.Name}.");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: Failed to set cache options for layer {featureLayerForCacheSettings.Name}: {ex.Message}");
                        }
                    }

                    // Specific handling for address layers to disable feature reduction/binning by default
                    if (newLayer is FeatureLayer featureLayer) // Apply to all FeatureLayers
                    {
                        try
                        {
                            if (featureLayer.GetDefinition() is CIMFeatureLayer layerDef)
                            {
                                // Specifically check for CIMBinningFeatureReduction
                                if (layerDef.FeatureReduction is CIMBinningFeatureReduction binningReduction)
                                {
                                    if (binningReduction.Enabled) // Only disable if it's currently enabled
                                    {
                                        binningReduction.Enabled = false;
                                        featureLayer.SetDefinition(layerDef); // Apply the change
                                        System.Diagnostics.Debug.WriteLine($"Disabled feature binning for layer: {featureLayer.Name}");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Feature binning already disabled for layer: {featureLayer.Name}");
                                    }
                                }
                                else if (layerDef.FeatureReduction != null)
                                {
                                    // Log if other types of feature reduction are present but not modified
                                    System.Diagnostics.Debug.WriteLine($"Layer {featureLayer.Name} has feature reduction of type '{layerDef.FeatureReduction.GetType().Name}', not binning. No changes made to feature reduction.");
                                }
                                // If layerDef.FeatureReduction is null, no feature reduction is configured, so nothing to do.
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log any errors during the process
                            System.Diagnostics.Debug.WriteLine($"Warning: Failed to access or modify feature reduction for layer {featureLayer.Name}: {ex.Message}");
                        }
                    }

                    progress?.Report($"Successfully added layer: {newLayer.Name}");
                    System.Diagnostics.Debug.WriteLine($"AddLayerToMapAsync: Successfully added layer {newLayer.Name} to map {map.Name}");
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error adding layer {layerName} to map: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"AddLayerToMapAsync: Error adding layer {layerName} from {parquetFilePath}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    // Optionally, show a message box to the user if critical
                    // ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"Failed to add layer {layerName}:\n{ex.Message}", "Layer Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            });
        }
    }
}
