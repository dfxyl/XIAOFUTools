using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ArcFieldType = ArcGIS.Core.Data.FieldType;
using ArcGeometryType = ArcGIS.Core.Geometry.GeometryType;
using GdalFieldSubType = OSGeo.OGR.FieldSubType;
using GdalFieldType = OSGeo.OGR.FieldType;
using GdalGeometry = OSGeo.OGR.Geometry;
using GdalGeometryType = OSGeo.OGR.wkbGeometryType;
using GdalLayer = OSGeo.OGR.Layer;
using GdalSpatialReference = OSGeo.OSR.SpatialReference;
using OgrDataSource = OSGeo.OGR.DataSource;
using OgrFeature = OSGeo.OGR.Feature;
using OgrFeatureDefn = OSGeo.OGR.FeatureDefn;
using OgrFieldDefn = OSGeo.OGR.FieldDefn;
using OSGeo.OGR;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal sealed class GdalMdbToGdbConverter
    {
        private readonly ArcGisGdbWriter _writer = new ArcGisGdbWriter();

        public void Convert(string inputMdb, string outputGdb, Action<string> log, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputMdb) || !File.Exists(inputMdb))
            {
                throw new FileNotFoundException("输入 MDB 不存在，请检查路径。", inputMdb);
            }

            if (string.IsNullOrWhiteSpace(outputGdb))
            {
                throw new ArgumentException("输出 GDB 路径不能为空。", nameof(outputGdb));
            }

            string outputFolder = Path.GetDirectoryName(outputGdb);
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new DirectoryNotFoundException("无法识别输出目录，请检查输出路径。");
            }

            Directory.CreateDirectory(outputFolder);

            cancellationToken.ThrowIfCancellationRequested();
            GdalRuntimeBootstrapper.EnsureInitialized();

            using OgrDataSource sourceDataSource = Ogr.Open(inputMdb, 0);
            if (sourceDataSource == null)
            {
                throw new InvalidOperationException(
                    "打开 MDB 失败。请确认 GDAL 已启用 PGeo 驱动，并已安装 Access Database Engine。");
            }

            _writer.EnsureOutputGeodatabase(outputGdb);

            List<SourceLayerInfo> layerInfos = GdalMdbLayerInspector.GetSourceLayerInfos(sourceDataSource);
            log?.Invoke($"检测到图层/表数量: {layerInfos.Count}");

            foreach (SourceLayerInfo layerInfo in layerInfos)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using GdalLayer sourceLayer = sourceDataSource.GetLayerByIndex(layerInfo.Index);
                if (sourceLayer == null)
                {
                    continue;
                }

                string displayName = string.IsNullOrWhiteSpace(layerInfo.FeatureDatasetName)
                    ? layerInfo.OutputName
                    : $"{layerInfo.FeatureDatasetName}\\{layerInfo.OutputName}";
                log?.Invoke($"转换: {displayName}");

                MdbLayerSchema layerSchema = BuildLayerSchema(sourceLayer, layerInfo, log);
                _writer.EnsureSchema(outputGdb, layerSchema, cancellationToken);
                CopyLayerRows(sourceLayer, layerSchema, outputGdb, cancellationToken);
            }
        }

        private MdbLayerSchema BuildLayerSchema(GdalLayer sourceLayer, SourceLayerInfo layerInfo, Action<string> log)
        {
            OgrFeatureDefn sourceDefinition = sourceLayer.GetLayerDefn()
                ?? throw new InvalidOperationException($"无法读取源图层定义: {layerInfo.SourceName}");

            bool isTable = layerInfo.IsTable || sourceDefinition.GetGeomType() == GdalGeometryType.wkbNone;
            string spatialReferenceWkt = string.Empty;
            ArcGeometryType? geometryType = null;
            bool hasZ = false;
            bool hasM = false;

            if (!isTable)
            {
                GdalGeometryType sourceGeometryType = sourceDefinition.GetGeomType();
                geometryType = MapGeometryType(sourceGeometryType, layerInfo);
                hasZ = HasZ(sourceGeometryType);
                hasM = HasM(sourceGeometryType);
                spatialReferenceWkt = ExportSpatialReferenceWkt(sourceLayer, layerInfo);
            }

            IReadOnlyList<MdbFieldSchema> fields = BuildFieldSchemas(sourceDefinition, isTable, layerInfo, log);
            return new MdbLayerSchema
            {
                LayerInfo = layerInfo,
                IsTable = isTable,
                GeometryType = geometryType,
                HasZ = hasZ,
                HasM = hasM,
                SpatialReferenceWkt = spatialReferenceWkt,
                Fields = fields
            };
        }

        private IReadOnlyList<MdbFieldSchema> BuildFieldSchemas(
            OgrFeatureDefn sourceDefinition,
            bool isTable,
            SourceLayerInfo layerInfo,
            Action<string> log)
        {
            var fields = new List<MdbFieldSchema>();
            int fieldCount = sourceDefinition.GetFieldCount();
            for (int i = 0; i < fieldCount; i++)
            {
                OgrFieldDefn fieldDefinition = sourceDefinition.GetFieldDefn(i);
                string fieldName = fieldDefinition.GetNameRef();
                if (ShouldSkipSourceField(fieldName, isTable))
                {
                    continue;
                }

                bool serializeAsText;
                ArcFieldType targetFieldType = MapFieldType(fieldDefinition.GetFieldType(), fieldDefinition.GetSubType(), out serializeAsText);
                if (serializeAsText)
                {
                    string displayName = string.IsNullOrWhiteSpace(layerInfo.FeatureDatasetName)
                        ? layerInfo.OutputName
                        : $"{layerInfo.FeatureDatasetName}\\{layerInfo.OutputName}";
                    log?.Invoke($"警告: 字段 {displayName}.{fieldName} 为不受 ArcGIS 原生支持的列表类型，已按文本写入。");
                }

                string aliasName = null;
                if (layerInfo.FieldAliases != null &&
                    layerInfo.FieldAliases.TryGetValue(fieldName, out string metadataAlias) &&
                    !string.IsNullOrWhiteSpace(metadataAlias))
                {
                    aliasName = metadataAlias.Trim();
                }

                if (string.IsNullOrWhiteSpace(aliasName))
                {
                    aliasName = fieldDefinition.GetAlternativeNameRef();
                }

                if (string.IsNullOrWhiteSpace(aliasName))
                {
                    aliasName = fieldName;
                }

                int width = fieldDefinition.GetWidth();
                int precision = fieldDefinition.GetPrecision();
                int? length = null;
                int? scaleValue = null;
                int? precisionValue = null;

                if (targetFieldType == ArcFieldType.String)
                {
                    length = width > 0 ? width : 255;
                }

                if (targetFieldType == ArcFieldType.Double || targetFieldType == ArcFieldType.Single)
                {
                    if (precision > 0)
                    {
                        precisionValue = precision;
                    }

                    if (width > 0)
                    {
                        scaleValue = width;
                    }
                }

                fields.Add(new MdbFieldSchema
                {
                    SourceIndex = i,
                    Name = fieldName,
                    AliasName = aliasName,
                    SourceFieldType = fieldDefinition.GetFieldType(),
                    SourceFieldSubType = fieldDefinition.GetSubType(),
                    TargetFieldType = targetFieldType,
                    Length = length,
                    Precision = precisionValue,
                    Scale = scaleValue,
                    IsNullable = fieldDefinition.IsNullable() != 0,
                    SerializeAsText = serializeAsText
                });
            }

            return fields;
        }

        private void CopyLayerRows(
            GdalLayer sourceLayer,
            MdbLayerSchema layerSchema,
            string outputGdb,
            CancellationToken cancellationToken)
        {
            sourceLayer.ResetReading();

            try
            {
                if (sourceLayer.GetFeatureCount(0) == 0)
                {
                    return;
                }
            }
            catch
            {
            }

            _writer.InsertRows(outputGdb, layerSchema, EnumerateRows(sourceLayer, layerSchema, cancellationToken), cancellationToken);
        }

        private IEnumerable<MdbRowData> EnumerateRows(
            GdalLayer sourceLayer,
            MdbLayerSchema layerSchema,
            CancellationToken cancellationToken)
        {
            OgrFeature sourceFeature;
            while ((sourceFeature = sourceLayer.GetNextFeature()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (sourceFeature)
                {
                    yield return ReadRow(sourceFeature, layerSchema);
                }
            }
        }

        private MdbRowData ReadRow(OgrFeature sourceFeature, MdbLayerSchema layerSchema)
        {
            var values = new object[layerSchema.Fields.Count];
            for (int i = 0; i < layerSchema.Fields.Count; i++)
            {
                values[i] = ReadFieldValue(sourceFeature, layerSchema.Fields[i]);
            }

            return new MdbRowData
            {
                GeometryWkb = layerSchema.IsTable ? null : ExportGeometryWkb(sourceFeature),
                Values = values
            };
        }

        private static object ReadFieldValue(OgrFeature sourceFeature, MdbFieldSchema fieldSchema)
        {
            if (!sourceFeature.IsFieldSet(fieldSchema.SourceIndex))
            {
                return null;
            }

            if (!sourceFeature.IsFieldSetAndNotNull(fieldSchema.SourceIndex) || sourceFeature.IsFieldNull(fieldSchema.SourceIndex))
            {
                return null;
            }

            if (fieldSchema.SerializeAsText)
            {
                return OgrUtf8Interop.GetFieldAsString(sourceFeature, fieldSchema.SourceIndex);
            }

            return fieldSchema.TargetFieldType switch
            {
                ArcFieldType.SmallInteger => System.Convert.ToInt16(sourceFeature.GetFieldAsInteger(fieldSchema.SourceIndex)),
                ArcFieldType.Integer => sourceFeature.GetFieldAsInteger(fieldSchema.SourceIndex),
                ArcFieldType.BigInteger => sourceFeature.GetFieldAsInteger64(fieldSchema.SourceIndex),
                ArcFieldType.Single => System.Convert.ToSingle(sourceFeature.GetFieldAsDouble(fieldSchema.SourceIndex)),
                ArcFieldType.Double => sourceFeature.GetFieldAsDouble(fieldSchema.SourceIndex),
                ArcFieldType.String => OgrUtf8Interop.GetFieldAsString(sourceFeature, fieldSchema.SourceIndex),
                ArcFieldType.GUID => ParseGuidValue(OgrUtf8Interop.GetFieldAsString(sourceFeature, fieldSchema.SourceIndex), fieldSchema.Name),
                ArcFieldType.Blob => OgrUtf8Interop.GetFieldAsBinary(sourceFeature, fieldSchema.SourceIndex),
                ArcFieldType.DateOnly => ReadDateOnlyValue(sourceFeature, fieldSchema.SourceIndex),
                ArcFieldType.TimeOnly => ReadTimeOnlyValue(sourceFeature, fieldSchema.SourceIndex),
                ArcFieldType.Date => ReadDateTimeValue(sourceFeature, fieldSchema.SourceIndex),
                ArcFieldType.XML => OgrUtf8Interop.GetFieldAsString(sourceFeature, fieldSchema.SourceIndex),
                _ => OgrUtf8Interop.GetFieldAsString(sourceFeature, fieldSchema.SourceIndex)
            };
        }

        private static Guid ParseGuidValue(string text, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Guid.Empty;
            }

            if (Guid.TryParse(text, out Guid guid))
            {
                return guid;
            }

            throw new InvalidOperationException($"字段 {fieldName} 的 GUID 值无效: {text}");
        }

        private static DateOnly? ReadDateOnlyValue(OgrFeature sourceFeature, int fieldIndex)
        {
            ReadDateTimeParts(sourceFeature, fieldIndex, out int year, out int month, out int day, out _, out _, out _, out _);
            if (year <= 0 || month <= 0 || day <= 0)
            {
                return null;
            }

            return new DateOnly(year, month, day);
        }

        private static TimeOnly? ReadTimeOnlyValue(OgrFeature sourceFeature, int fieldIndex)
        {
            ReadDateTimeParts(sourceFeature, fieldIndex, out _, out _, out _, out int hour, out int minute, out int second, out int millisecond);
            return new TimeOnly(hour, minute, second, millisecond);
        }

        private static DateTime? ReadDateTimeValue(OgrFeature sourceFeature, int fieldIndex)
        {
            ReadDateTimeParts(sourceFeature, fieldIndex, out int year, out int month, out int day, out int hour, out int minute, out int second, out int millisecond);
            if (year <= 0 || month <= 0 || day <= 0)
            {
                return null;
            }

            return new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Unspecified);
        }

        private static void ReadDateTimeParts(
            OgrFeature sourceFeature,
            int fieldIndex,
            out int year,
            out int month,
            out int day,
            out int hour,
            out int minute,
            out int second,
            out int millisecond)
        {
            year = 0;
            month = 0;
            day = 0;
            hour = 0;
            minute = 0;
            second = 0;
            millisecond = 0;

            float secondsWithFraction = 0;
            int timezone = 0;
            sourceFeature.GetFieldAsDateTime(
                fieldIndex,
                out year,
                out month,
                out day,
                out hour,
                out minute,
                out secondsWithFraction,
                out timezone);

            second = (int)Math.Truncate(secondsWithFraction);
            millisecond = (int)Math.Round((secondsWithFraction - second) * 1000f);
            if (millisecond >= 1000)
            {
                second += 1;
                millisecond -= 1000;
            }
        }

        private static byte[] ExportGeometryWkb(OgrFeature sourceFeature)
        {
            GdalGeometry geometry = sourceFeature.GetGeometryRef();
            if (geometry == null || geometry.IsEmpty())
            {
                return null;
            }

            var buffer = new byte[geometry.WkbSize()];
            if (geometry.ExportToWkb(buffer) != 0)
            {
                throw new InvalidOperationException("导出几何 WKB 失败。");
            }

            return buffer;
        }

        private static ArcFieldType MapFieldType(GdalFieldType sourceFieldType, GdalFieldSubType sourceFieldSubType, out bool serializeAsText)
        {
            serializeAsText = false;
            return sourceFieldType switch
            {
                GdalFieldType.OFTInteger when sourceFieldSubType == GdalFieldSubType.OFSTInt16 => ArcFieldType.SmallInteger,
                GdalFieldType.OFTInteger => ArcFieldType.Integer,
                GdalFieldType.OFTInteger64 => ArcFieldType.BigInteger,
                GdalFieldType.OFTReal when sourceFieldSubType == GdalFieldSubType.OFSTFloat32 => ArcFieldType.Single,
                GdalFieldType.OFTReal => ArcFieldType.Double,
                GdalFieldType.OFTString when sourceFieldSubType == GdalFieldSubType.OFSTUUID => ArcFieldType.GUID,
                GdalFieldType.OFTWideString when sourceFieldSubType == GdalFieldSubType.OFSTUUID => ArcFieldType.GUID,
                GdalFieldType.OFTString => ArcFieldType.String,
                GdalFieldType.OFTWideString => ArcFieldType.String,
                GdalFieldType.OFTBinary => ArcFieldType.Blob,
                GdalFieldType.OFTDate => ArcFieldType.DateOnly,
                GdalFieldType.OFTTime => ArcFieldType.TimeOnly,
                GdalFieldType.OFTDateTime => ArcFieldType.Date,
                GdalFieldType.OFTIntegerList => SetTextFallback(out serializeAsText),
                GdalFieldType.OFTInteger64List => SetTextFallback(out serializeAsText),
                GdalFieldType.OFTRealList => SetTextFallback(out serializeAsText),
                GdalFieldType.OFTStringList => SetTextFallback(out serializeAsText),
                GdalFieldType.OFTWideStringList => SetTextFallback(out serializeAsText),
                _ => SetTextFallback(out serializeAsText)
            };
        }

        private static ArcFieldType SetTextFallback(out bool serializeAsText)
        {
            serializeAsText = true;
            return ArcFieldType.String;
        }

        private static bool ShouldSkipSourceField(string fieldName, bool isTable)
        {
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                return true;
            }

            if (fieldName.Equals("OBJECTID", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!isTable &&
                (fieldName.Equals("Shape_Length", StringComparison.OrdinalIgnoreCase) ||
                 fieldName.Equals("Shape_Area", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        private static ArcGeometryType MapGeometryType(GdalGeometryType geometryType, SourceLayerInfo layerInfo)
        {
            return geometryType switch
            {
                GdalGeometryType.wkbPoint or
                GdalGeometryType.wkbPoint25D or
                GdalGeometryType.wkbPointM or
                GdalGeometryType.wkbPointZM => ArcGeometryType.Point,

                GdalGeometryType.wkbMultiPoint or
                GdalGeometryType.wkbMultiPoint25D or
                GdalGeometryType.wkbMultiPointM or
                GdalGeometryType.wkbMultiPointZM => ArcGeometryType.Multipoint,

                GdalGeometryType.wkbLineString or
                GdalGeometryType.wkbLineString25D or
                GdalGeometryType.wkbLineStringM or
                GdalGeometryType.wkbLineStringZM or
                GdalGeometryType.wkbMultiLineString or
                GdalGeometryType.wkbMultiLineString25D or
                GdalGeometryType.wkbMultiLineStringM or
                GdalGeometryType.wkbMultiLineStringZM => ArcGeometryType.Polyline,

                GdalGeometryType.wkbPolygon or
                GdalGeometryType.wkbPolygon25D or
                GdalGeometryType.wkbPolygonM or
                GdalGeometryType.wkbPolygonZM or
                GdalGeometryType.wkbMultiPolygon or
                GdalGeometryType.wkbMultiPolygon25D or
                GdalGeometryType.wkbMultiPolygonM or
                GdalGeometryType.wkbMultiPolygonZM => ArcGeometryType.Polygon,

                _ => throw new NotSupportedException(
                    $"暂不支持几何类型 {geometryType}：{layerInfo.SourceName}")
            };
        }

        private static bool HasZ(GdalGeometryType geometryType)
        {
            string name = geometryType.ToString();
            return name.EndsWith("Z", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("ZM", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("25D", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasM(GdalGeometryType geometryType)
        {
            string name = geometryType.ToString();
            return name.EndsWith("M", StringComparison.OrdinalIgnoreCase) ||
                   name.EndsWith("ZM", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExportSpatialReferenceWkt(GdalLayer sourceLayer, SourceLayerInfo layerInfo)
        {
            using GdalSpatialReference spatialReference =
                GdalMdbLayerInspector.NormalizeSpatialReferenceForFileGeodatabase(sourceLayer.GetSpatialRef())
                ?? GdalMdbLayerInspector.CloneSpatialReference(sourceLayer.GetSpatialRef());

            if (spatialReference == null)
            {
                throw new InvalidOperationException($"图层缺少空间参考: {layerInfo.SourceName}");
            }

            spatialReference.ExportToWkt(out string wkt, new[] { "FORMAT=WKT1_ESRI" });
            if (string.IsNullOrWhiteSpace(wkt))
            {
                throw new InvalidOperationException($"图层空间参考导出失败: {layerInfo.SourceName}");
            }

            return wkt;
        }
    }
}
