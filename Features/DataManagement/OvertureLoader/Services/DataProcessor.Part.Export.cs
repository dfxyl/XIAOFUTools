using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.DataManagement.OvertureLoader.Core;

namespace XIAOFUTools.Features.DataManagement.OvertureLoader.Services
{
    public partial class DataProcessor
    {

        /// <summary>
        /// Exports the current_table to GeoParquet file(s) using DuckDB's Parquet export.
        /// All exported files are placed in a single output folder per session.
        /// </summary>
        /// <param name="layerNameBase">Base name for the layer.</param>
        /// <param name="releaseVersion">The release version (used to name the output folder).</param>
        /// <param name="progress">Progress reporter.</param>
        /// <param name="parentS3Theme">The S3 parent theme key (e.g., 'buildings').</param>
        /// <param name="actualS3Type">The specific S3 data type (e.g., 'building', 'building_part').</param>
        /// <param name="dataOutputPathRoot">The root directory where data for the current release is stored (e.g., C:\...\ProjectHome\OvertureProAddinData\Data\{ReleaseVersion})</param>
        public async Task CreateFeatureLayerAsync(
            string layerNameBase,
            IProgress<string> progress,
            string parentS3Theme,
            string actualS3Type,
            string dataOutputPathRoot,
            CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine($"CreateFeatureLayerAsync called for: {layerNameBase}, ParentS3Theme: {parentS3Theme}, ActualS3Type: {actualS3Type}, DataOutputPathRoot: {dataOutputPathRoot}");
            progress?.Report($"开始为 {layerNameBase} 创建图层");

            if (string.IsNullOrEmpty(parentS3Theme) || string.IsNullOrEmpty(actualS3Type))
            {
                progress?.Report("错误: 未提供父主题或实际类型。无法创建要素图层。");
                System.Diagnostics.Debug.WriteLine("Error: ParentS3Theme or ActualS3Type is null or empty. Aborting CreateFeatureLayerAsync.");
                return;
            }
            if (string.IsNullOrEmpty(dataOutputPathRoot))
            {
                progress?.Report("错误: 未提供数据输出路径根目录。无法创建要素图层。");
                System.Diagnostics.Debug.WriteLine("Error: dataOutputPathRoot is null or empty. Aborting CreateFeatureLayerAsync.");
                return;
            }

            // Store the theme context for this operation (might be used for layer naming or other non-path logic)
            _currentParentS3Theme = parentS3Theme;
            _currentActualS3Type = actualS3Type;

            // Define the specific output folder for this actualS3Type directly under the dataOutputPathRoot
            // Path structure: dataOutputPathRoot / actualS3Type / *.parquet
            string themeTypeSpecificFolder = Path.Combine(dataOutputPathRoot, actualS3Type);

            System.Diagnostics.Debug.WriteLine($"Theme-specific output folder set to: {themeTypeSpecificFolder}");

            try
            {
                // Now pass this specific folder to ExportByGeometryType
                await ExportByGeometryType(
                    layerNameBase,
                    themeTypeSpecificFolder,
                    progress,
                    cancellationToken);

                progress?.Report($"{layerNameBase} 的要素图层创建过程已完成。");
                System.Diagnostics.Debug.WriteLine($"Feature layer creation process completed for {layerNameBase}");
            }
            catch (Exception ex)
            {
                progress?.Report($"创建要素图层 {layerNameBase} 时出错: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error in CreateFeatureLayerAsync for {layerNameBase}: {ex.Message}");
                // Handle or rethrow the exception as appropriate
                throw;
            }
            finally
            {
                // Clear theme context after operation
                _currentParentS3Theme = null;
                _currentActualS3Type = null;
            }
        }

        private async Task ExportByGeometryType(
            string layerNameBase,
            string themeTypeSpecificOutputFolder,
            IProgress<string> progress = null,
            CancellationToken cancellationToken = default)
        {
            System.Diagnostics.Debug.WriteLine($"ExportByGeometryType for {layerNameBase}, OutputFolder: {themeTypeSpecificOutputFolder}");
            progress?.Report($"正在为 {layerNameBase} 处理几何类型，使用最佳图层堆叠");

            var geometryTypes = (await _duckDbDataSession.GetGeometryTypesAsync(
                cancellationToken)).ToList();

            // Sort geometry types for optimal layer stacking order (bottom to top)
            // Polygons on bottom, lines in middle, points on top
            var geometryTypeOrder = new Dictionary<string, int>
            {
                { "POLYGON", 1 }, { "MULTIPOLYGON", 1 },           // Bottom layer
                { "LINESTRING", 2 }, { "MULTILINESTRING", 2 },     // Middle layer  
                { "POINT", 3 }, { "MULTIPOINT", 3 }                // Top layer
            };

            geometryTypes = geometryTypes
                .OrderBy(geomType => geometryTypeOrder.TryGetValue(geomType, out int order) ? order : 99)
                .ToList();

            if (geometryTypes.Count == 0)
            {
                progress?.Report("在当前数据集中未找到几何图形。跳过图层创建。");
                System.Diagnostics.Debug.WriteLine("No geometry types found. Skipping export.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Processing {geometryTypes.Count} geometry types: {string.Join(", ", geometryTypes)}");

            int typeIndex = 0;
            foreach (var geomType in geometryTypes)
            {
                typeIndex++;
                cancellationToken.ThrowIfCancellationRequested();
                string stackingNote = geometryTypeOrder.TryGetValue(geomType, out int order)
                    ? $" (layer {order} of 3)"
                    : "";
                progress?.Report($"Processing {layerNameBase}: {geomType}{stackingNote} ({typeIndex}/{geometryTypes.Count})");

                string descriptiveGeomType = GetDescriptiveGeometryType(geomType);
                string finalLayerName = geometryTypes.Count > 1 ? $"{layerNameBase} ({descriptiveGeomType})" : layerNameBase;
                string outputFileName = $"{MfcUtility.SanitizeFileName(finalLayerName)}.parquet";

                // Ensure output path uses the theme/type specific folder
                string outputPath = Path.Combine(themeTypeSpecificOutputFolder, outputFileName);

                string query = OvertureDuckDbDataPlan.BuildGeometrySelectSql(geomType);

                try
                {
                    string actualFilePath = await _geoParquetExporter.ExportAsync(
                        query,
                        outputPath,
                        finalLayerName,
                        progress,
                        cancellationToken);
                    var layerInfo = new LayerCreationInfo
                    {
                        FilePath = actualFilePath,
                        LayerName = finalLayerName,
                        GeometryType = geomType,
                        StackingPriority = geometryTypeOrder.TryGetValue(geomType, out int priority) ? priority : 99,
                        ParentTheme = _currentParentS3Theme,
                        ActualType = _currentActualS3Type
                    };
                    _pendingLayers.Add(layerInfo);
                    progress?.Report($"Prepared layer {finalLayerName} for optimal stacking");
                    System.Diagnostics.Debug.WriteLine($"Successfully prepared layer: {finalLayerName} at {actualFilePath}");
                }
                catch (Exception ex)
                {
                    progress?.Report($"Error exporting layer for {geomType}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Error during export for {geomType} of {layerNameBase}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    // Decide if we should continue with other geometry types or rethrow
                    // For now, log and continue to attempt other types
                }
            }
            progress?.Report($"Finished processing all geometry types for {layerNameBase}.");
        }

        /// <summary>
        /// Applies consistent settings to feature layers (cache, feature reduction, etc.)
        /// </summary>
        private static void ApplyLayerSettings(FeatureLayer featureLayer)
        {
            try
            {
                if (featureLayer.GetDefinition() is CIMFeatureLayer layerDef)
                {
                    // Set cache settings for performance
                    layerDef.DisplayCacheType = ArcGIS.Core.CIM.DisplayCacheType.None;
                    layerDef.FeatureCacheType = ArcGIS.Core.CIM.FeatureCacheType.None;

                    // Disable feature binning/reduction for better data visibility
                    if (layerDef.FeatureReduction is CIMBinningFeatureReduction binningReduction && binningReduction.Enabled)
                    {
                        binningReduction.Enabled = false;
                        System.Diagnostics.Debug.WriteLine($"Disabled feature binning for layer: {featureLayer.Name}");
                    }

                    featureLayer.SetDefinition(layerDef);
                    System.Diagnostics.Debug.WriteLine($"Applied settings to layer: {featureLayer.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Failed to apply settings to layer {featureLayer.Name}: {ex.Message}");
            }
        }
    }
}
