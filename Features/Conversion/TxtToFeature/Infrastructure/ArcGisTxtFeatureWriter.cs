using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace XIAOFUTools.Features.Conversion.TxtToFeature.Infrastructure
{
    internal sealed class ArcGisTxtFeatureWriter
    {
        private static readonly string[] ShapefileSidecarExtensions =
        {
            ".shp", ".shx", ".dbf", ".prj", ".cpg", ".sbn", ".sbx", ".xml"
        };

        private readonly string _outputFolder;
        private readonly bool _separateFolder;
        private readonly SpatialReference _spatialReference;
        private readonly string[] _fieldNames;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logError;
        private readonly Func<bool> _isCancellationRequested;

        internal ArcGisTxtFeatureWriter(
            string outputFolder,
            bool separateFolder,
            SpatialReference spatialReference,
            string fieldNames,
            Action<string> logInfo,
            Action<string> logError,
            Func<bool> isCancellationRequested)
        {
            _outputFolder = outputFolder;
            _separateFolder = separateFolder;
            _spatialReference = spatialReference;
            _fieldNames = (fieldNames ?? string.Empty)
                .Split(',')
                .Select(field => field.Trim())
                .Where(field => field.Length > 0 && field != "@")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _logInfo = logInfo;
            _logError = logError;
            _isCancellationRequested = isCancellationRequested;
        }

        internal async Task<bool> CreateAsync(
            IReadOnlyList<PlotData> plots,
            string fileName,
            string sourceDirectory = null)
        {
            if (plots == null || plots.Count == 0)
            {
                _logInfo($"跳过创建Shapefile {fileName}：没有地块数据");
                return false;
            }

            try
            {
                ThrowIfCancellationRequested();
                var outputPath = ResolveOutputPath(fileName, sourceDirectory);
                Directory.CreateDirectory(outputPath);
                var shapefilePath = Path.Combine(outputPath, $"{fileName}.shp");
                DeleteExistingShapefile(shapefilePath);

                _logInfo($"正在创建Shapefile: {shapefilePath}，包含 {plots.Count} 个地块");
                if (!await CreateFeatureClassAsync(outputPath, fileName))
                {
                    return false;
                }

                if (!await AddFieldsAsync(shapefilePath))
                {
                    return false;
                }

                var inserted = await QueuedTask.Run(() => InsertFeatures(shapefilePath, plots));
                if (inserted)
                {
                    _logInfo($"Shapefile创建完成: {shapefilePath}");
                }

                return inserted;
            }
            catch (OperationCanceledException)
            {
                _logInfo($"已取消创建Shapefile: {fileName}");
                return false;
            }
            catch (Exception ex)
            {
                _logError($"创建Shapefile {fileName} 时出错: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CreateFeatureClassAsync(string outputPath, string fileName)
        {
            var parameters = Geoprocessing.MakeValueArray(
                outputPath,
                fileName,
                "POLYGON",
                null,
                "DISABLED",
                "DISABLED",
                _spatialReference);
            var result = await Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", parameters);
            if (!result.IsFailed)
            {
                return true;
            }

            LogGeoprocessingErrors("创建Shapefile失败", result.Messages);
            return false;
        }

        private async Task<bool> AddFieldsAsync(string shapefilePath)
        {
            foreach (var fieldName in _fieldNames)
            {
                ThrowIfCancellationRequested();
                var parameters = Geoprocessing.MakeValueArray(
                    shapefilePath,
                    fieldName,
                    "TEXT",
                    null,
                    null,
                    255);
                var result = await Geoprocessing.ExecuteToolAsync("AddField_management", parameters);
                if (result.IsFailed)
                {
                    LogGeoprocessingErrors($"添加字段 {fieldName} 失败", result.Messages);
                    return false;
                }

                _logInfo($"字段 {fieldName} 添加成功");
            }

            return true;
        }

        private bool InsertFeatures(string shapefilePath, IReadOnlyList<PlotData> plots)
        {
            var directory = Path.GetDirectoryName(shapefilePath) ?? throw new InvalidOperationException("输出目录无效");
            var name = Path.GetFileNameWithoutExtension(shapefilePath);
            var connectionPath = new FileSystemConnectionPath(
                new Uri(directory),
                FileSystemDatastoreType.Shapefile);

            using var datastore = new FileSystemDatastore(connectionPath);
            using var featureClass = datastore.OpenDataset<FeatureClass>(name);
            using var definition = featureClass.GetDefinition();
            using var insertCursor = featureClass.CreateInsertCursor();
            var successCount = 0;
            var failedCount = 0;

            foreach (var plot in plots)
            {
                ThrowIfCancellationRequested();
                try
                {
                    var polygon = CreatePolygon(plot);
                    if (polygon == null || polygon.IsEmpty)
                    {
                        failedCount++;
                        continue;
                    }

                    using var rowBuffer = featureClass.CreateRowBuffer();
                    rowBuffer[definition.GetShapeField()] = polygon;
                    foreach (var attribute in plot.Attributes)
                    {
                        var fieldIndex = definition.FindField(attribute.Key);
                        if (fieldIndex >= 0)
                        {
                            rowBuffer[fieldIndex] = attribute.Value?.ToString() ?? string.Empty;
                        }
                    }

                    insertCursor.Insert(rowBuffer);
                    successCount++;
                }
                catch (Exception ex)
                {
                    failedCount++;
                    _logError($"插入单个要素时出错: {ex.Message}");
                }
            }

            _logInfo($"要素插入完成: 成功 {successCount} 个，失败 {failedCount} 个");
            return successCount > 0;
        }

        private Polygon CreatePolygon(PlotData plot)
        {
            if (plot.Rings == null || plot.Rings.Count == 0)
            {
                return null;
            }

            var builder = new PolygonBuilderEx(_spatialReference);
            var rings = plot.Rings
                .Where(ring => ring.Points.Count >= 3)
                .OrderBy(ring => ring.RingNumber)
                .ToList();
            for (var index = 0; index < rings.Count; index++)
            {
                var coordinates = rings[index].Points
                    .Select(point => new Coordinate2D(point.X, point.Y))
                    .ToList();
                if (!AreEqual(coordinates[0], coordinates[^1]))
                {
                    coordinates.Add(coordinates[0]);
                }

                if (index > 0)
                {
                    coordinates.Reverse();
                }

                builder.AddPart(coordinates);
            }

            return rings.Count == 0 ? null : builder.ToGeometry();
        }

        private string ResolveOutputPath(string fileName, string sourceDirectory)
        {
            var baseFolder = sourceDirectory ?? _outputFolder;
            return _separateFolder ? Path.Combine(baseFolder, fileName) : baseFolder;
        }

        private static void DeleteExistingShapefile(string shapefilePath)
        {
            var directory = Path.GetDirectoryName(shapefilePath) ?? string.Empty;
            var baseName = Path.GetFileNameWithoutExtension(shapefilePath);
            foreach (var extension in ShapefileSidecarExtensions)
            {
                var path = Path.Combine(directory, baseName + extension);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private void LogGeoprocessingErrors(string prefix, IEnumerable<IGPMessage> messages)
        {
            var errors = string.Join("; ", messages
                .Where(message => message.Type == GPMessageType.Error)
                .Select(message => message.Text));
            _logError(string.IsNullOrWhiteSpace(errors) ? prefix : $"{prefix}: {errors}");
        }

        private void ThrowIfCancellationRequested()
        {
            if (_isCancellationRequested())
            {
                throw new OperationCanceledException();
            }
        }

        private static bool AreEqual(Coordinate2D left, Coordinate2D right)
        {
            return Math.Abs(left.X - right.X) <= 0.001 && Math.Abs(left.Y - right.Y) <= 0.001;
        }
    }
}
