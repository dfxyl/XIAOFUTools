using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ExcelDataReader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ShapefileBuilder
{
    public partial class ShapefileBuilderViewModel
    {

        /// <summary>
        /// 导出Excel模板
        /// </summary>
        private void ExportTemplate()
        {
            try
            {
                string addinFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string templatePath = Path.Combine(addinFolder, "Data", "Excel模板", "建SHP模板.xls");

                if (!_fileStore.FileExists(templatePath))
                {
                    LogError("建SHP模板文件不存在: 建SHP模板.xls");
                    PresentationServices.Dialogs.Show("建SHP模板文件不存在: 建SHP模板.xls", "错误");
                    return;
                }

                var outputFolder = PresentationServices.Files.SelectFolder("选择模板导出位置");
                if (!string.IsNullOrWhiteSpace(outputFolder))
                {
                    string destPath = Path.Combine(outputFolder, "建SHP模板.xls");
                    _fileStore.CopyFile(templatePath, destPath);

                    LogInfo($"模板已导出: {destPath}");
                    PresentationServices.Dialogs.Show($"已成功导出模板到:\n{destPath}", "导出成功");
                }
            }
            catch (Exception ex)
            {
                LogError($"导出模板失败: {ex.Message}");
                PresentationServices.Dialogs.Show($"导出模板失败: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 开始建SHP
        /// </summary>
        private async void StartBuildShapefile()
        {
            IsProcessing = true;
            LogText = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            var startTime = DateTime.Now;
            LogInfo($"开始时间: {startTime:yyyy年MM月dd日 HH:mm:ss}");

            try
            {
                if (!_fileStore.FileExists(InputExcelPath))
                {
                    LogError("指定的Excel文件不存在。");
                    return;
                }

                if (!_fileStore.DirectoryExists(OutputFolderPath))
                {
                    _fileStore.EnsureDirectory(OutputFolderPath);
                    LogInfo($"输出目录不存在，已自动创建: {OutputFolderPath}");
                }

                LogInfo("正在读取Excel文件...");
                var dataSet = ReadExcelToDataSet(InputExcelPath);
                if (dataSet == null)
                {
                    LogError("读取Excel文件失败。");
                    return;
                }

                var layers = ReadLayersTable(dataSet);
                if (layers == null || layers.Count == 0)
                {
                    LogError("无法读取图层表或图层表为空。");
                    return;
                }

                LogInfo($"读取到 {layers.Count} 个图层定义");

                var featureDatasets = ReadFeatureDatasetsTable(dataSet);
                if (featureDatasets.Count > 0)
                {
                    LogWarning($"检测到 {featureDatasets.Count} 个要素集定义，SHP不支持要素集，已自动忽略。");
                }

                var targetSpatialReference = SelectedSpatialReference ?? SpatialReferences.WGS84;
                LogInfo($"目标坐标系: {targetSpatialReference.Name} (WKID: {targetSpatialReference.Wkid})");

                int createdCount = 0;
                int skippedCount = 0;
                int failedCount = 0;

                foreach (var layer in layers)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        LogWarning("操作已被用户取消");
                        break;
                    }

                    string originalName = layer.AttributesTable;
                    string shpName = NormalizeShapefileDatasetName(originalName, layer.Index);
                    if (!string.Equals(originalName, shpName, StringComparison.OrdinalIgnoreCase))
                    {
                        LogWarning($"图层名 '{originalName}' 不符合SHP命名要求，已规范为 '{shpName}'。");
                    }

                    string shpPath = Path.Combine(OutputFolderPath, shpName + ".shp");
                    if (_fileStore.FileExists(shpPath))
                    {
                        LogWarning($"SHP已存在，跳过: {shpName}.shp");
                        skippedCount++;
                        continue;
                    }

                    try
                    {
                        string geometryType = ConvertToGeometryType(layer.GeometryType);
                        var createParams = Geoprocessing.MakeValueArray(
                            OutputFolderPath,
                            shpName + ".shp",
                            geometryType,
                            null,
                            "DISABLED",
                            "DISABLED",
                            targetSpatialReference);

                        LogInfo($"正在创建SHP: {shpName}.shp ({geometryType})");
                        var createResult = await Geoprocessing.ExecuteToolAsync(
                            "CreateFeatureclass_management",
                            createParams,
                            null,
                            _cancellationTokenSource.Token);

                        if (createResult.IsFailed)
                        {
                            failedCount++;
                            LogError($"创建SHP失败: {shpName}.shp，{GetGpErrorMessage(createResult)}");
                            continue;
                        }

                        var fields = ReadFieldsTable(dataSet, originalName);
                        var usedFieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "FID",
                            "OID",
                            "OBJECTID",
                            "SHAPE",
                            "SHAPE_LEN",
                            "SHAPE_LENG",
                            "SHAPE_AREA"
                        };

                        int addFieldCount = 0;
                        foreach (var field in fields)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(field.FieldCode))
                            {
                                continue;
                            }

                            string shpFieldType = ConvertToShapefileFieldType(field.FieldType);
                            if (string.IsNullOrWhiteSpace(shpFieldType))
                            {
                                LogWarning($"字段 {field.FieldCode} 类型 {field.FieldType} 不支持SHP，已跳过。");
                                continue;
                            }

                            string normalizedFieldName = NormalizeShapefileFieldName(field.FieldCode, usedFieldNames);
                            if (!string.Equals(field.FieldCode, normalizedFieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                LogWarning($"字段名 '{field.FieldCode}' 已规范为 '{normalizedFieldName}'。");
                            }

                            object precision = null;
                            object scale = null;
                            object length = null;

                            if (string.Equals(shpFieldType, "TEXT", StringComparison.OrdinalIgnoreCase))
                            {
                                length = NormalizeTextLength(field.FieldLength);
                            }
                            else if ((string.Equals(shpFieldType, "FLOAT", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(shpFieldType, "DOUBLE", StringComparison.OrdinalIgnoreCase)))
                            {
                                if (field.FieldLength.HasValue && field.FieldLength.Value > 0)
                                {
                                    precision = field.FieldLength.Value;
                                }

                                if (field.DecimalPlaces.HasValue && field.DecimalPlaces.Value >= 0)
                                {
                                    scale = field.DecimalPlaces.Value;
                                }
                            }

                            var addFieldParams = Geoprocessing.MakeValueArray(
                                shpPath,
                                normalizedFieldName,
                                shpFieldType,
                                precision,
                                scale,
                                length,
                                field.FieldAlias);

                            var addFieldResult = await Geoprocessing.ExecuteToolAsync(
                                "AddField_management",
                                addFieldParams,
                                null,
                                _cancellationTokenSource.Token);

                            if (addFieldResult.IsFailed)
                            {
                                LogError($"字段创建失败: {shpName}.{normalizedFieldName}，{GetGpErrorMessage(addFieldResult)}");
                                continue;
                            }

                            addFieldCount++;
                        }

                        createdCount++;
                        LogInfo($"SHP创建成功: {shpName}.shp，已添加 {addFieldCount} 个字段");
                    }
                    catch (OperationCanceledException)
                    {
                        LogWarning("操作已被用户取消");
                        break;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        LogError($"创建SHP失败: {shpName}.shp，{ex.Message}");
                    }
                }

                LogInfo($"建SHP完成：成功 {createdCount}，跳过 {skippedCount}，失败 {failedCount}");
            }
            catch (Exception ex)
            {
                LogError($"建SHP过程中出错: {ex.Message}");
            }
            finally
            {
                var endTime = DateTime.Now;
                LogInfo($"结束时间: {endTime:yyyy年MM月dd日 HH:mm:ss}");
                LogInfo($"历时: {endTime - startTime}");
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 停止建SHP
        /// </summary>
        private void StopBuildShapefile()
        {
            _cancellationTokenSource?.Cancel();
            LogWarning("正在停止操作...");
        }
    }
}
