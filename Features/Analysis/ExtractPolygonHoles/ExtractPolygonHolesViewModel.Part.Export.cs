using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Analysis.ExtractPolygonHoles
{
    internal partial class ExtractPolygonHolesViewModel
    {

        private ExtractionStats ExtractAndWriteHoles(OutputDatasetUtils.OutputPathInfo outputInfo, CancellationToken token)
        {
            using var sourceFeatureClass = SelectedPolygonLayer.GetFeatureClass();

            if (outputInfo.IsGdb)
            {
                using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(outputInfo.GdbRoot)));
                using var outputFeatureClass = geodatabase.OpenDataset<FeatureClass>(outputInfo.RelativePathInGdb);
                return ExtractAndWriteCore(sourceFeatureClass, outputFeatureClass, token);
            }

            var outputFolder = Path.GetDirectoryName(outputInfo.CatalogPath);
            var outputName = Path.GetFileName(outputInfo.CatalogPath);
            using var datastore = new FileSystemDatastore(new FileSystemConnectionPath(new Uri(outputFolder), FileSystemDatastoreType.Shapefile));
            using var outputShp = datastore.OpenDataset<FeatureClass>(outputName);
            return ExtractAndWriteCore(sourceFeatureClass, outputShp, token);
        }

        private ExtractionStats ExtractAndWriteCore(FeatureClass sourceFeatureClass, FeatureClass outputFeatureClass, CancellationToken token)
        {
            var stats = new ExtractionStats();
            var sourceDefinition = sourceFeatureClass.GetDefinition();
            var outputDefinition = outputFeatureClass.GetDefinition();

            var fieldMappings = BuildFieldMappings(sourceDefinition, outputDefinition);
            var outputShapeField = outputDefinition.GetShapeField();

            using var sourceCursor = sourceFeatureClass.Search(new QueryFilter(), false);
            using var insertCursor = outputFeatureClass.CreateInsertCursor();

            while (sourceCursor.MoveNext())
            {
                token.ThrowIfCancellationRequested();

                var current = sourceCursor.Current;
                if (current is not Feature sourceFeature)
                {
                    current?.Dispose();
                    continue;
                }

                using (sourceFeature)
                {
                    stats.SourceFeatureCount++;

                    var polygon = sourceFeature.GetShape() as Polygon;
                    if (polygon == null || polygon.IsEmpty)
                        continue;

                    var holePolygons = ExtractHolePolygons(polygon);
                    if (holePolygons.Count == 0)
                        continue;

                    stats.HoleRingCount += holePolygons.Count;

                    if (CreateMultipartOutput)
                    {
                        var multipartHole = BuildMultipartPolygon(holePolygons, polygon.SpatialReference);
                        InsertPolygon(sourceFeature, multipartHole, outputShapeField, fieldMappings, outputFeatureClass, insertCursor);
                        stats.OutputFeatureCount++;
                        continue;
                    }

                    foreach (var holePolygon in holePolygons)
                    {
                        InsertPolygon(sourceFeature, holePolygon, outputShapeField, fieldMappings, outputFeatureClass, insertCursor);
                        stats.OutputFeatureCount++;
                    }
                }
            }

            insertCursor.Flush();
            return stats;
        }

        private static List<FieldMapping> BuildFieldMappings(FeatureClassDefinition sourceDefinition, FeatureClassDefinition outputDefinition)
        {
            var sourceShapeField = sourceDefinition.GetShapeField();
            var sourceObjectIdField = sourceDefinition.GetObjectIDField();
            var outputShapeField = outputDefinition.GetShapeField();
            var outputObjectIdField = outputDefinition.GetObjectIDField();

            var sourceFields = sourceDefinition.GetFields()
                .Select(field => field.Name)
                .Where(name =>
                    !name.Equals(sourceShapeField, StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals(sourceObjectIdField, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var outputFields = outputDefinition.GetFields()
                .Select(field => field.Name)
                .Where(name =>
                    !name.Equals(outputShapeField, StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals(outputObjectIdField, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var mappings = new List<FieldMapping>();

            if (sourceFields.Count == outputFields.Count)
            {
                for (var i = 0; i < sourceFields.Count; i++)
                    mappings.Add(new FieldMapping(sourceFields[i], outputFields[i]));

                return mappings;
            }

            var outputSet = new HashSet<string>(outputFields, StringComparer.OrdinalIgnoreCase);
            foreach (var sourceField in sourceFields)
            {
                if (outputSet.Contains(sourceField))
                    mappings.Add(new FieldMapping(sourceField, sourceField));
            }

            return mappings;
        }

        private static Polygon BuildMultipartPolygon(IReadOnlyList<Polygon> polygons, SpatialReference spatialReference)
        {
            var builder = new PolygonBuilderEx(spatialReference);
            foreach (var polygon in polygons)
            {
                foreach (var part in polygon.Parts)
                {
                    var points = GetPartPoints(part);
                    if (points.Count >= 4)
                        builder.AddPart(points);
                }
            }

            var multipart = builder.ToGeometry();
            return GeometryEngine.Instance.SimplifyAsFeature(multipart) as Polygon ?? multipart;
        }
    }
}
