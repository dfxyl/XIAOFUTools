using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcFieldType = ArcGIS.Core.Data.FieldType;
using ArcSpatialReference = ArcGIS.Core.Geometry.SpatialReference;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal sealed class ArcGisGdbWriter
    {
        public void EnsureOutputGeodatabase(string outputGdb)
        {
            if (Directory.Exists(outputGdb))
            {
                return;
            }

            string outputFolder = Path.GetDirectoryName(outputGdb);
            string gdbName = Path.GetFileNameWithoutExtension(outputGdb);
            var parameters = Geoprocessing.MakeValueArray(outputFolder, gdbName);
            var result = Geoprocessing.ExecuteToolAsync("CreateFileGDB_management", parameters).Result;
            if (result == null || result.IsFailed)
            {
                throw new InvalidOperationException("创建 GDB 失败: " + BuildGpMessage(result));
            }
        }

        public void EnsureSchema(string outputGdb, MdbLayerSchema schema, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var geodatabase = OpenGeodatabase(outputGdb);

                if (!schema.IsTable)
                {
                    EnsureFeatureDataset(geodatabase, schema);
                }

                if (DatasetExists(geodatabase, schema))
                {
                    return;
                }

                var schemaBuilder = new SchemaBuilder(geodatabase);
                if (schema.IsTable)
                {
                    var tableDescription = new TableDescription(schema.LayerInfo.OutputName, CreateFieldDescriptions(schema.Fields))
                    {
                        AliasName = string.IsNullOrWhiteSpace(schema.LayerInfo.AliasName)
                            ? schema.LayerInfo.OutputName
                            : schema.LayerInfo.AliasName
                    };
                    schemaBuilder.Create(tableDescription);
                }
                else
                {
                    ArcSpatialReference spatialReference = CreateSpatialReference(schema.SpatialReferenceWkt);
                    var shapeDescription = new ShapeDescription(schema.GeometryType!.Value, spatialReference)
                    {
                        HasM = schema.HasM,
                        HasZ = schema.HasZ
                    };

                    var featureClassDescription = new FeatureClassDescription(
                        schema.LayerInfo.OutputName,
                        CreateFieldDescriptions(schema.Fields),
                        shapeDescription)
                    {
                        AliasName = string.IsNullOrWhiteSpace(schema.LayerInfo.AliasName)
                            ? schema.LayerInfo.OutputName
                            : schema.LayerInfo.AliasName
                    };

                    if (string.IsNullOrWhiteSpace(schema.LayerInfo.FeatureDatasetName))
                    {
                        schemaBuilder.Create(featureClassDescription);
                    }
                    else
                    {
                        var datasetDefinition = geodatabase.GetDefinition<FeatureDatasetDefinition>(schema.LayerInfo.FeatureDatasetName);
                        var datasetDescription = new FeatureDatasetDescription(datasetDefinition);
                        schemaBuilder.Create(datasetDescription, featureClassDescription);
                    }
                }

                if (!schemaBuilder.Build())
                {
                    throw new InvalidOperationException("创建目标数据集失败: " + string.Join("; ", schemaBuilder.ErrorMessages));
                }
            }).GetAwaiter().GetResult();
        }

        public void InsertRows(string outputGdb, MdbLayerSchema schema, IReadOnlyList<MdbRowData> rows, CancellationToken cancellationToken)
        {
            if (rows == null || rows.Count == 0)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            QueuedTask.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var geodatabase = OpenGeodatabase(outputGdb);
                if (schema.IsTable)
                {
                    using var table = geodatabase.OpenDataset<Table>(schema.LayerInfo.OutputName);
                    geodatabase.ApplyEdits(() => InsertTableRows(table, schema, rows, cancellationToken));
                }
                else
                {
                    using var featureClass = geodatabase.OpenDataset<FeatureClass>(schema.LayerInfo.OutputName);
                    FeatureClassDefinition definition = featureClass.GetDefinition();
                    string shapeField = definition.GetShapeField();
                    ArcSpatialReference spatialReference = definition.GetSpatialReference();

                    geodatabase.ApplyEdits(() => InsertFeatureRows(
                        featureClass,
                        schema,
                        rows,
                        shapeField,
                        spatialReference,
                        cancellationToken));
                }
            }).GetAwaiter().GetResult();
        }

        private static Geodatabase OpenGeodatabase(string outputGdb)
        {
            return new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(outputGdb)));
        }

        private static void EnsureFeatureDataset(Geodatabase geodatabase, MdbLayerSchema schema)
        {
            if (string.IsNullOrWhiteSpace(schema.LayerInfo.FeatureDatasetName))
            {
                return;
            }

            try
            {
                var existingDefinition = geodatabase.GetDefinition<FeatureDatasetDefinition>(schema.LayerInfo.FeatureDatasetName);
                if (existingDefinition != null)
                {
                    return;
                }
            }
            catch
            {
            }

            ArcSpatialReference spatialReference = CreateSpatialReference(schema.SpatialReferenceWkt);
            var datasetDescription = new FeatureDatasetDescription(schema.LayerInfo.FeatureDatasetName, spatialReference);
            var schemaBuilder = new SchemaBuilder(geodatabase);
            schemaBuilder.Create(datasetDescription);
            if (!schemaBuilder.Build())
            {
                throw new InvalidOperationException("创建要素集失败: " + string.Join("; ", schemaBuilder.ErrorMessages));
            }
        }

        private static bool DatasetExists(Geodatabase geodatabase, MdbLayerSchema schema)
        {
            try
            {
                if (schema.IsTable)
                {
                    return geodatabase.GetDefinition<TableDefinition>(schema.LayerInfo.OutputName) != null;
                }

                return geodatabase.GetDefinition<FeatureClassDefinition>(schema.LayerInfo.OutputName) != null;
            }
            catch
            {
                return false;
            }
        }

        private static List<FieldDescription> CreateFieldDescriptions(IReadOnlyList<MdbFieldSchema> fields)
        {
            var descriptions = new List<FieldDescription>(fields.Count);
            foreach (MdbFieldSchema field in fields)
            {
                var description = new FieldDescription(field.Name, field.TargetFieldType)
                {
                    AliasName = string.IsNullOrWhiteSpace(field.AliasName) ? field.Name : field.AliasName,
                    IsNullable = field.IsNullable
                };

                if (field.Length.HasValue && field.TargetFieldType == ArcFieldType.String)
                {
                    description.Length = field.Length.Value;
                }

                if (field.Precision.HasValue)
                {
                    description.Precision = field.Precision.Value;
                }

                if (field.Scale.HasValue)
                {
                    description.Scale = field.Scale.Value;
                }

                descriptions.Add(description);
            }

            return descriptions;
        }

        private static ArcSpatialReference CreateSpatialReference(string spatialReferenceWkt)
        {
            if (string.IsNullOrWhiteSpace(spatialReferenceWkt))
            {
                throw new InvalidOperationException("缺少空间参考定义，无法创建目标数据集。");
            }

            return SpatialReferenceBuilder.CreateSpatialReference(spatialReferenceWkt);
        }

        private static void InsertTableRows(
            Table table,
            MdbLayerSchema schema,
            IReadOnlyList<MdbRowData> rows,
            CancellationToken cancellationToken)
        {
            using var insertCursor = table.CreateInsertCursor();
            foreach (MdbRowData row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var rowBuffer = table.CreateRowBuffer();
                ApplyValues(rowBuffer, schema.Fields, row.Values);
                insertCursor.Insert(rowBuffer);
            }

            insertCursor.Flush();
        }

        private static void InsertFeatureRows(
            FeatureClass featureClass,
            MdbLayerSchema schema,
            IReadOnlyList<MdbRowData> rows,
            string shapeField,
            ArcSpatialReference spatialReference,
            CancellationToken cancellationToken)
        {
            using var insertCursor = featureClass.CreateInsertCursor();
            foreach (MdbRowData row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var rowBuffer = featureClass.CreateRowBuffer();
                if (row.GeometryWkb != null && row.GeometryWkb.Length > 0)
                {
                    rowBuffer[shapeField] = GeometryEngine.Instance.ImportFromWKB(
                        WkbImportFlags.WkbImportNonTrusted,
                        row.GeometryWkb,
                        spatialReference);
                }

                ApplyValues(rowBuffer, schema.Fields, row.Values);
                insertCursor.Insert(rowBuffer);
            }

            insertCursor.Flush();
        }

        private static void ApplyValues(RowBuffer rowBuffer, IReadOnlyList<MdbFieldSchema> fields, IReadOnlyList<object> values)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                object value = values[i];
                if (value == null)
                {
                    continue;
                }

                rowBuffer[fields[i].Name] = value;
            }
        }

        private static string BuildGpMessage(IGPResult result)
        {
            if (result?.Messages == null || !result.Messages.Any())
            {
                return "未知错误";
            }

            return string.Join("; ", result.Messages.Select(message => message.Text).Where(text => !string.IsNullOrWhiteSpace(text)));
        }
    }
}
