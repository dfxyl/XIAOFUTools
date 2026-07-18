using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Conversion.SpecialCoordinateTransform
{
    internal sealed partial class SpecialCoordinateTransformDockPaneViewModel
    {
        private const int InsertCursorFlushInterval = 2000;
        private const int FeatureProgressUpdateInterval = 200;
        private const int FeatureLogInterval = 2000;

        private static DatasetHandle OpenLayerDataset(FeatureLayer featureLayer)
        {
            if (featureLayer == null)
            {
                throw new InvalidOperationException("输入图层无效。");
            }

            var featureClass = featureLayer.GetTable() as FeatureClass;
            if (featureClass == null)
            {
                throw new InvalidOperationException("无法从图层获取要素类。");
            }

            return new DatasetHandle(featureClass, featureClass);
        }

        private static DatasetHandle OpenShapefileDataset(string shapefilePath)
        {
            string folder = Path.GetDirectoryName(shapefilePath);
            string name = Path.GetFileNameWithoutExtension(shapefilePath);
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"无效的 SHP 路径: {shapefilePath}");
            }

            var datastore = new FileSystemDatastore(new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile));
            var featureClass = datastore.OpenDataset<FeatureClass>(name);
            return new DatasetHandle(featureClass, featureClass, datastore);
        }

        private static DatasetHandle OpenGdbDataset(string gdbPath, string relativePathInGdb)
        {
            if (string.IsNullOrWhiteSpace(gdbPath) || string.IsNullOrWhiteSpace(relativePathInGdb))
            {
                throw new InvalidOperationException("GDB 输入路径无效。");
            }

            var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));
            return OpenFeatureClassFromGeodatabase(geodatabase, relativePathInGdb);
        }

        private static DatasetHandle OpenFeatureClassFromGeodatabase(Geodatabase geodatabase, string relativePathInGdb)
        {
            if (geodatabase == null)
            {
                throw new ArgumentNullException(nameof(geodatabase));
            }

            if (string.IsNullOrWhiteSpace(relativePathInGdb))
            {
                throw new InvalidOperationException("GDB 要素类路径为空。");
            }

            if (TrySplitFeatureDatasetPath(relativePathInGdb, out string featureDatasetName, out string featureClassName))
            {
                var featureDataset = geodatabase.OpenDataset<FeatureDataset>(featureDatasetName);
                var featureClassInDataset = featureDataset.OpenDataset<FeatureClass>(featureClassName);
                return new DatasetHandle(featureClassInDataset, featureClassInDataset, featureDataset, geodatabase);
            }

            string normalizedFeatureClassName = NormalizeGdbRelativePath(relativePathInGdb);
            var featureClass = geodatabase.OpenDataset<FeatureClass>(normalizedFeatureClassName);
            return new DatasetHandle(featureClass, featureClass, geodatabase);
        }

        private static bool TrySplitFeatureDatasetPath(string relativePathInGdb, out string featureDatasetName, out string featureClassName)
        {
            featureDatasetName = null;
            featureClassName = null;

            string normalizedPath = NormalizeGdbRelativePath(relativePathInGdb);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return false;
            }

            string[] pathParts = normalizedPath.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length < 2)
            {
                return false;
            }

            featureDatasetName = pathParts[0];
            featureClassName = pathParts[pathParts.Length - 1];
            return !string.IsNullOrWhiteSpace(featureDatasetName) && !string.IsNullOrWhiteSpace(featureClassName);
        }

        private static string NormalizeGdbRelativePath(string relativePathInGdb)
        {
            return string.IsNullOrWhiteSpace(relativePathInGdb)
                ? string.Empty
                : relativePathInGdb.Trim().Replace('/', '\\');
        }

        private async Task<DatasetHandle> CreateOutputDatasetAsync(
            string outputPath,
            string defaultOutputName,
            object templateValue,
            FeatureClassDefinition inputDefinition,
            string featureDatasetName)
        {
            OutputDatasetUtils.OutputPathInfo outputInfo = OutputDatasetUtils.ParseOutputPath(outputPath, defaultOutputName);
            await EnsureOutputWorkspaceReadyAsync(outputInfo, inputDefinition.GetSpatialReference(), featureDatasetName);
            await OutputDatasetUtils.DeleteIfExistsAsync(outputInfo);

            string geometryType = inputDefinition.GetShapeType().ToString().ToUpperInvariant();
            await OutputDatasetUtils.CreateFeatureClassAsync(outputInfo, geometryType, inputDefinition.GetSpatialReference(), false, false, templateValue);

            if (outputInfo.IsGdb)
            {
                var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(outputInfo.GdbRoot)));
                return OpenFeatureClassFromGeodatabase(geodatabase, outputInfo.RelativePathInGdb);
            }

            _fileStore.EnsureDirectory(outputInfo.OutPathWorkspace);
            var datastore = new FileSystemDatastore(new FileSystemConnectionPath(new Uri(outputInfo.OutPathWorkspace), FileSystemDatastoreType.Shapefile));
            var shapefileFeatureClass = datastore.OpenDataset<FeatureClass>(outputInfo.OutNameNoExt);
            return new DatasetHandle(shapefileFeatureClass, shapefileFeatureClass, datastore);
        }

        private async Task EnsureOutputWorkspaceReadyAsync(
            OutputDatasetUtils.OutputPathInfo outputInfo,
            SpatialReference spatialReference,
            string featureDatasetName)
        {
            if (outputInfo.IsGdb)
            {
                await EnsureGdbExistsAsync(outputInfo.GdbRoot);
                if (!string.IsNullOrWhiteSpace(featureDatasetName))
                {
                    EnsureFeatureDatasetExists(outputInfo.GdbRoot, featureDatasetName, spatialReference);
                }
            }
            else
            {
                _fileStore.EnsureDirectory(outputInfo.OutPathWorkspace);
            }
        }

        private async Task EnsureGdbExistsAsync(string gdbPath)
        {
            if (_fileStore.DirectoryExists(gdbPath))
            {
                return;
            }

            string parentFolder = Path.GetDirectoryName(gdbPath);
            string gdbName = Path.GetFileNameWithoutExtension(gdbPath);
            if (string.IsNullOrWhiteSpace(parentFolder) || string.IsNullOrWhiteSpace(gdbName))
            {
                throw new InvalidOperationException($"无效的 GDB 路径: {gdbPath}");
            }

            _fileStore.EnsureDirectory(parentFolder);
            var parameters = Geoprocessing.MakeValueArray(parentFolder, gdbName);
            var result = await Geoprocessing.ExecuteToolAsync("CreateFileGDB_management", parameters);
            if (result.IsFailed)
            {
                throw new InvalidOperationException("创建输出 GDB 失败: " + string.Join("; ", result.Messages.Select(message => message.Text)));
            }
        }

        private async Task RecreateOutputGdbAsync(string outputGdbPath)
        {
            _fileStore.DeleteDirectoryIfExists(outputGdbPath, recursive: true);

            await EnsureGdbExistsAsync(outputGdbPath);
        }

        private static void EnsureFeatureDatasetExists(string gdbPath, string datasetName, SpatialReference spatialReference)
        {
            using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath)));
            try
            {
                var existingDefinition = geodatabase.GetDefinition<FeatureDatasetDefinition>(datasetName);
                if (existingDefinition != null)
                {
                    return;
                }
            }
            catch
            {
                // ignore and create
            }

            var schemaBuilder = new SchemaBuilder(geodatabase);
            schemaBuilder.Create(new FeatureDatasetDescription(datasetName, spatialReference));
            if (!schemaBuilder.Build())
            {
                string errorMessage = schemaBuilder.ErrorMessages != null && schemaBuilder.ErrorMessages.Count > 0
                    ? string.Join("; ", schemaBuilder.ErrorMessages)
                    : "未知错误";
                throw new InvalidOperationException($"创建要素数据集失败: {datasetName}，{errorMessage}");
            }
        }

        private async Task TransformFeaturesAsync(
            FeatureClass inputFeatureClass,
            FeatureClass outputFeatureClass,
            string conversionType,
            CancellationToken cancellationToken,
            bool reportFeatureProgress)
        {
            long totalCount = inputFeatureClass.GetCount();
            AddLog($"要素总数: {totalCount}");

            if (reportFeatureProgress)
            {
                SetProgressIndeterminate(false);
                UpdateProgress(0);
            }

            FeatureClassDefinition inputDefinition = inputFeatureClass.GetDefinition();
            FeatureClassDefinition outputDefinition = outputFeatureClass.GetDefinition();
            Dictionary<string, string> fieldMap = CreateFieldMap(inputDefinition, outputDefinition);
            string outputShapeField = outputDefinition.GetShapeField();

            int processedCount = 0;
            using RowCursor cursor = inputFeatureClass.Search();
            using InsertCursor insertCursor = outputFeatureClass.CreateInsertCursor();
            while (cursor.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();
                Row currentRow = cursor.Current;
                if (currentRow is not Feature feature)
                {
                    currentRow?.Dispose();
                    continue;
                }

                using (feature)
                {
                    Geometry geometry = TransformGeometry(feature.GetShape(), conversionType);
                    using RowBuffer rowBuffer = outputFeatureClass.CreateRowBuffer();
                    rowBuffer[outputShapeField] = geometry;

                    foreach (KeyValuePair<string, string> mapping in fieldMap)
                    {
                        object value = feature[mapping.Key];
                        if (value != null)
                        {
                            rowBuffer[mapping.Value] = value;
                        }
                    }

                    insertCursor.Insert(rowBuffer);
                }

                processedCount++;

                if (processedCount % InsertCursorFlushInterval == 0)
                {
                    insertCursor.Flush();
                }

                if (reportFeatureProgress && totalCount > 0 &&
                    (processedCount % FeatureProgressUpdateInterval == 0 || processedCount == totalCount))
                {
                    UpdateProgress((double)processedCount / totalCount * 100.0);
                }

                if (reportFeatureProgress &&
                    (processedCount % FeatureLogInterval == 0 || processedCount == totalCount))
                {
                    AddLog($"已处理 {processedCount}/{totalCount} 个要素。");
                }
            }

            insertCursor.Flush();
        }

        private static Dictionary<string, string> CreateFieldMap(FeatureClassDefinition inputDefinition, FeatureClassDefinition outputDefinition)
        {
            var fieldMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<Field> outputFields = outputDefinition.GetFields();

            foreach (Field inputField in inputDefinition.GetFields())
            {
                if (inputField.FieldType == FieldType.Geometry ||
                    inputField.FieldType == FieldType.OID ||
                    inputField.FieldType == FieldType.GlobalID)
                {
                    continue;
                }

                Field outputField = outputFields.FirstOrDefault(field =>
                    string.Equals(field.Name, inputField.Name, StringComparison.OrdinalIgnoreCase));

                if (outputField == null)
                {
                    continue;
                }

                if (!outputField.IsEditable ||
                    outputField.FieldType == FieldType.Geometry ||
                    outputField.FieldType == FieldType.OID ||
                    outputField.FieldType == FieldType.GlobalID)
                {
                    continue;
                }

                if (string.Equals(outputField.Name, outputDefinition.GetShapeField(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (outputField.Name.StartsWith("Shape_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                fieldMap[inputField.Name] = outputField.Name;
            }

            return fieldMap;
        }

        private Geometry TransformGeometry(Geometry geometry, string conversionType)
        {
            if (geometry == null)
            {
                return null;
            }

            return geometry.GeometryType switch
            {
                GeometryType.Point => TransformPoint(geometry as MapPoint, conversionType),
                GeometryType.Polyline => TransformPolyline(geometry as Polyline, conversionType),
                GeometryType.Polygon => TransformPolygon(geometry as Polygon, conversionType),
                _ => geometry
            };
        }

        private static MapPoint TransformPoint(MapPoint point, string conversionType)
        {
            if (point == null)
            {
                return null;
            }

            (double newX, double newY) = CoordinateTransformCore.TransformCoordinate(point.X, point.Y, conversionType);
            return MapPointBuilderEx.CreateMapPoint(newX, newY, point.SpatialReference);
        }

        private static Polyline TransformPolyline(Polyline polyline, string conversionType)
        {
            if (polyline == null)
            {
                return null;
            }

            var builder = new PolylineBuilderEx(polyline.SpatialReference);
            foreach (ReadOnlySegmentCollection part in polyline.Parts)
            {
                var points = new List<MapPoint>();
                foreach (Segment segment in part)
                {
                    MapPoint startPoint = segment.StartPoint;
                    (double newX, double newY) = CoordinateTransformCore.TransformCoordinate(startPoint.X, startPoint.Y, conversionType);
                    points.Add(MapPointBuilderEx.CreateMapPoint(newX, newY, polyline.SpatialReference));
                }

                if (part.Count > 0)
                {
                    Segment lastSegment = part.Last();
                    MapPoint endPoint = lastSegment.EndPoint;
                    (double newX, double newY) = CoordinateTransformCore.TransformCoordinate(endPoint.X, endPoint.Y, conversionType);
                    points.Add(MapPointBuilderEx.CreateMapPoint(newX, newY, polyline.SpatialReference));
                }

                if (points.Count > 1)
                {
                    builder.AddPart(points);
                }
            }

            return builder.ToGeometry();
        }

        private static Polygon TransformPolygon(Polygon polygon, string conversionType)
        {
            if (polygon == null)
            {
                return null;
            }

            var builder = new PolygonBuilderEx(polygon.SpatialReference);
            foreach (ReadOnlySegmentCollection part in polygon.Parts)
            {
                var points = new List<MapPoint>();
                foreach (Segment segment in part)
                {
                    MapPoint startPoint = segment.StartPoint;
                    (double newX, double newY) = CoordinateTransformCore.TransformCoordinate(startPoint.X, startPoint.Y, conversionType);
                    points.Add(MapPointBuilderEx.CreateMapPoint(newX, newY, polygon.SpatialReference));
                }

                if (points.Count > 2)
                {
                    builder.AddPart(points);
                }
            }

            return builder.ToGeometry();
        }
    }
}
