using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;

namespace XIAOFUTools.Tools.DataProcessing.MdbBatchToGdb
{
    internal sealed class GdalMdbToGdbConverter
    {
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
                throw new DirectoryNotFoundException("无法识别输出目录。请检查输出路径。");
            }

            Directory.CreateDirectory(outputFolder);

            cancellationToken.ThrowIfCancellationRequested();
            GdalRuntimeBootstrapper.EnsureInitialized();

            DataSource sourceDataSource = null;
            DataSource targetDataSource = null;
            var featureDatasetSrs = new Dictionary<string, SpatialReference>(StringComparer.OrdinalIgnoreCase);
            var createdFeatureDatasetSrs = new Dictionary<string, SpatialReference>(StringComparer.OrdinalIgnoreCase);

            try
            {
                sourceDataSource = Ogr.Open(inputMdb, 0);
                if (sourceDataSource == null)
                {
                    throw new InvalidOperationException(
                        "打开 MDB 失败。请确认 GDAL 启用了 PGeo 驱动，且系统已安装 Access Database Engine。");
                }

                OSGeo.OGR.Driver outputDriver = GetOutputDriver();
                targetDataSource = outputDriver.CreateDataSource(outputGdb, null);
                if (targetDataSource == null)
                {
                    throw new InvalidOperationException("创建 GDB 失败。请检查输出路径是否可写。");
                }

                List<SourceLayerInfo> layerInfos = GdalMdbLayerInspector.GetSourceLayerInfos(sourceDataSource);
                log?.Invoke($"检测到图层/表数量: {layerInfos.Count}");

                featureDatasetSrs = GdalMdbLayerInspector.BuildFeatureDatasetSpatialReferences(sourceDataSource, layerInfos);
                var createdFeatureDatasetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (SourceLayerInfo layerInfo in layerInfos)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Layer sourceLayer = sourceDataSource.GetLayerByIndex(layerInfo.Index);
                    if (sourceLayer == null)
                    {
                        continue;
                    }

                    string displayName = string.IsNullOrWhiteSpace(layerInfo.FeatureDatasetName)
                        ? layerInfo.OutputName
                        : $"{layerInfo.FeatureDatasetName}\\{layerInfo.OutputName}";
                    log?.Invoke($"转换: {displayName}");

                    bool isFeatureDatasetLayer = !layerInfo.IsTable && !string.IsNullOrWhiteSpace(layerInfo.FeatureDatasetName);
                    bool featureDatasetAlreadyCreated = isFeatureDatasetLayer
                                                      && createdFeatureDatasetNames.Contains(layerInfo.FeatureDatasetName);

                    SpatialReference targetSpatialReference = featureDatasetAlreadyCreated
                        ? null
                        : GdalMdbLayerInspector.ResolveTargetLayerSpatialReference(
                            sourceLayer,
                            layerInfo,
                            featureDatasetSrs,
                            createdFeatureDatasetSrs,
                            log);

                    SpatialReference createdLayerSpatialReference = null;
                    try
                    {
                        createdLayerSpatialReference = CopyLayer(
                            sourceLayer,
                            targetDataSource,
                            layerInfo.OutputName,
                            layerInfo.FeatureDatasetName,
                            targetSpatialReference,
                            log,
                            cancellationToken);

                        if (isFeatureDatasetLayer)
                        {
                            createdFeatureDatasetNames.Add(layerInfo.FeatureDatasetName);

                            if (createdLayerSpatialReference != null
                                && !createdFeatureDatasetSrs.ContainsKey(layerInfo.FeatureDatasetName))
                            {
                                createdFeatureDatasetSrs[layerInfo.FeatureDatasetName] = createdLayerSpatialReference;
                                createdLayerSpatialReference = null;
                            }
                        }
                    }
                    finally
                    {
                        createdLayerSpatialReference?.Dispose();
                    }
                }
            }
            finally
            {
                DisposeSpatialReferences(featureDatasetSrs);
                DisposeSpatialReferences(createdFeatureDatasetSrs);

                targetDataSource?.Dispose();
                sourceDataSource?.Dispose();
            }
        }

        private static OSGeo.OGR.Driver GetOutputDriver()
        {
            OSGeo.OGR.Driver driver = Ogr.GetDriverByName("FileGDB");
            if (driver != null)
            {
                return driver;
            }

            driver = Ogr.GetDriverByName("OpenFileGDB");
            if (driver != null)
            {
                return driver;
            }

            throw new InvalidOperationException("未找到 FileGDB/OpenFileGDB 驱动，无法创建 GDB。");
        }

        private static SpatialReference CopyLayer(
            Layer sourceLayer,
            DataSource targetDataSource,
            string layerName,
            string featureDatasetName,
            SpatialReference targetSpatialReference,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            FeatureDefn sourceDefinition = sourceLayer.GetLayerDefn();
            string[] creationOptions = BuildLayerCreationOptions(sourceDefinition.GetGeomType(), featureDatasetName);

            Layer targetLayer = null;
            bool retriedWithoutCrs = false;
            try
            {
                targetLayer = OgrUtf8Interop.CreateLayer(
                    targetDataSource,
                    layerName,
                    targetSpatialReference,
                    sourceDefinition.GetGeomType(),
                    creationOptions);
            }
            catch (ApplicationException ex) when (ShouldRetryCreateLayerWithoutCrs(ex, featureDatasetName))
            {
                retriedWithoutCrs = true;
                log?.Invoke($"图层 {featureDatasetName}\\{layerName} 坐标系不匹配，已按数据集坐标系重试。");

                try
                {
                    targetLayer = OgrUtf8Interop.CreateLayer(
                        targetDataSource,
                        layerName,
                        null,
                        sourceDefinition.GetGeomType(),
                        creationOptions);
                }
                catch (Exception retryException)
                {
                    throw new InvalidOperationException(
                        $"图层创建重试失败: {featureDatasetName}\\{layerName}，{retryException.Message}",
                        retryException);
                }
            }

            if (targetLayer == null)
            {
                string gdalError = Gdal.GetLastErrorMsg();
                if (string.IsNullOrWhiteSpace(gdalError))
                {
                    throw new InvalidOperationException(
                        retriedWithoutCrs
                            ? $"创建图层失败: {layerName}（已按要素数据集坐标系重试）。"
                            : $"创建图层失败: {layerName}。");
                }

                throw new InvalidOperationException(
                    retriedWithoutCrs
                        ? $"创建图层失败: {layerName}（已按要素数据集坐标系重试），{gdalError}"
                        : $"创建图层失败: {layerName}，{gdalError}");
            }

            try
            {
                int fieldCount = sourceDefinition.GetFieldCount();
                for (int i = 0; i < fieldCount; i++)
                {
                    FieldDefn fieldDefinition = sourceDefinition.GetFieldDefn(i);
                    if (targetLayer.CreateField(fieldDefinition, 1) != 0)
                    {
                        throw new InvalidOperationException($"字段创建失败: {layerName}.{fieldDefinition.GetNameRef()}");
                    }
                }

                sourceLayer.ResetReading();
                FeatureDefn targetDefinition = targetLayer.GetLayerDefn();

                Feature sourceFeature;
                while ((sourceFeature = sourceLayer.GetNextFeature()) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using (sourceFeature)
                    using (var targetFeature = new Feature(targetDefinition))
                    {
                        if (targetFeature.SetFrom(sourceFeature, 1) != 0)
                        {
                            throw new InvalidOperationException($"要素复制失败（SetFrom）: {layerName}");
                        }

                        ApplyStringFieldOverrides(sourceFeature, targetFeature, sourceDefinition);

                        if (targetLayer.CreateFeature(targetFeature) != 0)
                        {
                            throw new InvalidOperationException($"要素写入失败: {layerName}");
                        }
                    }
                }

                return GdalMdbLayerInspector.CloneSpatialReference(targetLayer.GetSpatialRef())
                    ?? GdalMdbLayerInspector.CloneSpatialReference(targetSpatialReference);
            }
            finally
            {
                targetLayer.Dispose();
            }
        }

        private static bool ShouldRetryCreateLayerWithoutCrs(ApplicationException ex, string featureDatasetName)
        {
            if (string.IsNullOrWhiteSpace(featureDatasetName) || ex == null)
            {
                return false;
            }

            string message = ex.Message ?? string.Empty;
            return message.IndexOf("Layer CRS does not match feature dataset CRS", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string[] BuildLayerCreationOptions(wkbGeometryType geometryType, string featureDatasetName)
        {
            if (string.IsNullOrWhiteSpace(featureDatasetName))
            {
                return null;
            }

            if (geometryType == wkbGeometryType.wkbNone)
            {
                return null;
            }

            return new[] { $"FEATURE_DATASET={featureDatasetName}" };
        }

        private static void DisposeSpatialReferences(IDictionary<string, SpatialReference> spatialReferences)
        {
            if (spatialReferences == null)
            {
                return;
            }

            foreach (SpatialReference spatialReference in spatialReferences.Values)
            {
                spatialReference?.Dispose();
            }

            spatialReferences.Clear();
        }

        private static void ApplyStringFieldOverrides(Feature sourceFeature, Feature targetFeature, FeatureDefn sourceDefinition)
        {
            int fieldCount = sourceDefinition.GetFieldCount();
            for (int i = 0; i < fieldCount; i++)
            {
                FieldDefn fieldDefinition = sourceDefinition.GetFieldDefn(i);
                FieldType fieldType = fieldDefinition.GetFieldType();
                bool isStringField = fieldType == FieldType.OFTString || fieldType == FieldType.OFTWideString;
                if (!isStringField)
                {
                    continue;
                }

                if (!sourceFeature.IsFieldSet(i))
                {
                    targetFeature.UnsetField(i);
                    continue;
                }

                if (!sourceFeature.IsFieldSetAndNotNull(i))
                {
                    targetFeature.SetFieldNull(i);
                    continue;
                }

                string value = sourceFeature.GetFieldAsString(i);
                if (!LooksReadableString(value))
                {
                    value = OgrUtf8Interop.GetFieldAsString(sourceFeature, i);
                }

                targetFeature.SetField(i, value ?? string.Empty);
            }
        }

        private static bool LooksReadableString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            if (value.IndexOf('\uFFFD') >= 0)
            {
                return false;
            }

            int suspiciousCount = 0;
            foreach (char ch in value)
            {
                if (char.IsControl(ch))
                {
                    return false;
                }

                if (ch == '?' || (ch >= '\u0080' && ch <= '\u00BF'))
                {
                    suspiciousCount++;
                }
            }

            return suspiciousCount * 4 < value.Length;
        }
    }
}
