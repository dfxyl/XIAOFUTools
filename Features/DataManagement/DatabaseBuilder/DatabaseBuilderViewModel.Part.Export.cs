using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;
using ExcelDataReader;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.DatabaseBuilder
{
    public partial class DatabaseBuilderViewModel
    {

        /// <summary>
        /// 导出Excel模板
        /// </summary>
        private void ExportTemplate()
        {
            try
            {
                // 获取模板文件路径
                string addinFolder = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var templates = _fileStore.GetAvailableTemplates(addinFolder);
                if (templates.Count == 0)
                {
                    LogError("模板文件不存在");
                    PresentationServices.Dialogs.Show("模板文件不存在", "错误");
                    return;
                }

                // 使用 ArcGIS Pro 自带的文件夹浏览对话框
                var outputFolder = PresentationServices.Files.SelectFolder("选择模板导出位置");
                if (!string.IsNullOrWhiteSpace(outputFolder))
                {
                    _fileStore.CopyTemplates(templates, outputFolder);
                    foreach (var template in templates)
                    {
                        LogInfo($"模板已导出: {template.FileName}");
                    }

                    int exportedCount = templates.Count;

                    LogInfo($"共导出 {exportedCount} 个模板到: {outputFolder}");
                    PresentationServices.Dialogs.Show($"已成功导出 {exportedCount} 个模板到:\n{outputFolder}", "导出成功");
                }
            }
            catch (Exception ex)
            {
                LogError($"导出模板失败: {ex.Message}");
                PresentationServices.Dialogs.Show($"导出模板失败: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 开始建库
        /// </summary>
        private async void StartBuildDatabase()
        {
            IsProcessing = true;
            LogText = "";
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            var startTime = DateTime.Now;
            LogInfo($"开始时间: {startTime:yyyy年MM月dd日 HH:mm:ss}");

            try
            {
                // 验证输入文件
                if (!_fileStore.FileExists(InputExcelPath))
                {
                    LogError("指定的Excel文件不存在。");
                    return;
                }

                // 读取图层表
                LogInfo("正在读取Excel文件...");
                var layersTable = ReadLayersTable(InputExcelPath);
                if (layersTable == null || layersTable.Count == 0)
                {
                    LogError("无法读取图层表或图层表为空。");
                    return;
                }
                LogInfo($"读取到 {layersTable.Count} 个图层定义");

                // 读取要素集表
                var featureDatasets = ReadFeatureDatasetsTable(InputExcelPath);
                if (featureDatasets.Count > 0)
                {
                    LogInfo($"读取到 {featureDatasets.Count} 个要素集定义");
                }

                // 创建数据库
                string gdbPath = System.IO.Path.Combine(OutputFolderPath, DatabaseName + ".gdb");
                var targetSpatialReference = SelectedSpatialReference ?? SpatialReferences.WGS84;
                LogInfo($"目标坐标系: {targetSpatialReference.Name} (WKID: {targetSpatialReference.Wkid})");

                if (!_fileStore.DirectoryExists(gdbPath))
                {
                    LogInfo($"正在创建数据库: {DatabaseName}.gdb");
                    try
                    {
                        var args = ArcGIS.Desktop.Core.Geoprocessing.Geoprocessing.MakeValueArray(OutputFolderPath, DatabaseName);
                        var result = await ArcGIS.Desktop.Core.Geoprocessing.Geoprocessing.ExecuteToolAsync(
                            "CreateFileGDB_management",
                            args,
                            null,
                            _cancellationTokenSource.Token,
                            null,
                            ArcGIS.Desktop.Core.Geoprocessing.GPExecuteToolFlags.GPThread);
                        if (result.IsFailed)
                        {
                            LogError("创建数据库失败: " + string.Join("; ", result.Messages.Select(message => message.Text)));
                            return;
                        }

                        LogInfo($"数据库 {DatabaseName} 创建成功");
                    }
                    catch (Exception ex)
                    {
                        LogError($"创建数据库失败: {ex.Message}");
                        return;
                    }
                }
                else
                {
                    LogInfo($"数据库 {DatabaseName} 已存在");
                }

                await QueuedTask.Run(() =>
                {
                    // 打开数据库
                    using (var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbPath))))
                    {
                        // 创建要素集（如果有定义）
                        var createdDatasets = new HashSet<string>();
                        foreach (var dataset in featureDatasets)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                return;
                            }

                            // 检查要素集是否存在
                            bool datasetExists = false;
                            try
                            {
                                var dsDef = geodatabase.GetDefinition<FeatureDatasetDefinition>(dataset.DatasetName);
                                datasetExists = dsDef != null;
                            }
                            catch
                            {
                                datasetExists = false;
                            }

                            if (!datasetExists)
                            {
                                try
                                {
                                    LogInfo($"正在创建要素集: {dataset.DatasetName}");
                                    
                                    // 使用 SchemaBuilder 创建要素集
                                    var datasetDescription = new FeatureDatasetDescription(dataset.DatasetName, targetSpatialReference);
                                    
                                    var schemaBuilder = new SchemaBuilder(geodatabase);
                                    schemaBuilder.Create(datasetDescription);
                                    
                                    if (schemaBuilder.Build())
                                    {
                                        LogInfo($"要素集 {dataset.DatasetName} 创建成功");
                                        createdDatasets.Add(dataset.DatasetName);
                                    }
                                    else
                                    {
                                        var errorInfo = schemaBuilder.ErrorMessages;
                                        var errorMsg = errorInfo != null && errorInfo.Count > 0 
                                            ? string.Join("; ", errorInfo) 
                                            : "未知错误";
                                        LogError($"创建要素集 {dataset.DatasetName} 失败: {errorMsg}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogError($"创建要素集 {dataset.DatasetName} 失败: {ex.Message}");
                                }
                            }
                            else
                            {
                                LogInfo($"要素集 {dataset.DatasetName} 已存在");
                                createdDatasets.Add(dataset.DatasetName);
                            }
                        }

                        // 遍历图层表创建要素类
                        foreach (var layer in layersTable)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                return;
                            }

                            string layerName = layer.AttributesTable;

                            // 检查要素类是否存在
                            bool featureClassExists = false;
                            try
                            {
                                var fcDef = geodatabase.GetDefinition<FeatureClassDefinition>(layerName);
                                featureClassExists = fcDef != null;
                            }
                            catch
                            {
                                featureClassExists = false;
                            }

                            if (!featureClassExists)
                            {
                                try
                                {
                                    // 读取字段表
                                    var fields = ReadFieldsTable(InputExcelPath, layer.AttributesTable);

                                    // 创建字段描述列表
                                    var fieldDescriptions = new List<FieldDescription>();
                                    
                                    if (fields != null && fields.Count > 0)
                                    {
                                        foreach (var field in fields)
                                        {
                                            if (string.IsNullOrEmpty(field.FieldCode))
                                            {
                                                continue;
                                            }

                                            // 转换字段类型
                                            var fieldType = ConvertToFieldType(field.FieldType);
                                            var fieldDesc = new FieldDescription(field.FieldCode, fieldType);
                                            
                                            // 设置别名
                                            if (!string.IsNullOrEmpty(field.FieldAlias))
                                            {
                                                fieldDesc.AliasName = field.FieldAlias;
                                            }

                                            // 设置字段长度（仅文本类型）
                                            if (fieldType == FieldType.String && field.FieldLength.HasValue && field.FieldLength.Value > 0)
                                            {
                                                fieldDesc.Length = field.FieldLength.Value;
                                            }
                                            else if (fieldType == FieldType.String)
                                            {
                                                fieldDesc.Length = 255; // 默认长度
                                            }

                                            fieldDescriptions.Add(fieldDesc);
                                        }
                                    }

                                    // 创建Shape字段描述
                                    var shapeType = ConvertToGeometryType(layer.GeometryType);
                                    var layerSpatialReference = targetSpatialReference;

                                    // 创建要素类描述
                                    FeatureClassDescription fcDescription;
                                    
                                    // 检查是否需要在要素集内创建
                                    bool hasFeatureDataset = !string.IsNullOrEmpty(layer.FeatureDataset) && createdDatasets.Contains(layer.FeatureDataset);

                                    FeatureDatasetDefinition datasetDef = null;
                                    if (hasFeatureDataset)
                                    {
                                        datasetDef = geodatabase.GetDefinition<FeatureDatasetDefinition>(layer.FeatureDataset);
                                        var datasetSpatialReference = datasetDef?.GetSpatialReference();
                                        if (datasetSpatialReference != null)
                                        {
                                            layerSpatialReference = datasetSpatialReference;
                                            if (datasetSpatialReference.Wkid > 0 && targetSpatialReference.Wkid > 0 && datasetSpatialReference.Wkid != targetSpatialReference.Wkid)
                                            {
                                                LogWarning($"要素集 {layer.FeatureDataset} 的坐标系与当前设置不一致，将使用要素集自身坐标系创建要素类 {layerName}");
                                            }
                                        }
                                    }

                                    var shapeDescription = new ShapeDescription(shapeType, layerSpatialReference);
                                    
                                    // 创建要素类描述
                                    fcDescription = new FeatureClassDescription(layerName, fieldDescriptions, shapeDescription);
                                    if (!string.IsNullOrEmpty(layer.LayerAlias))
                                    {
                                        fcDescription = new FeatureClassDescription(layerName, fieldDescriptions, shapeDescription)
                                        {
                                            AliasName = layer.LayerAlias
                                        };
                                    }

                                    // 使用 SchemaBuilder 创建要素类
                                    var schemaBuilder = new SchemaBuilder(geodatabase);
                                    
                                    if (hasFeatureDataset && datasetDef != null)
                                    {
                                        // 获取要素集的 Token
                                        var datasetToken = new FeatureDatasetDescription(datasetDef);
                                        
                                        // 在要素集内创建要素类
                                        LogInfo($"正在创建要素类: {layerName} (要素集: {layer.FeatureDataset})");
                                        schemaBuilder.Create(datasetToken, fcDescription);
                                    }
                                    else
                                    {
                                        if (hasFeatureDataset && datasetDef == null)
                                        {
                                            LogWarning($"未找到要素集 {layer.FeatureDataset}，将改为在数据库根目录创建要素类: {layerName}");
                                        }
                                        // 在数据库根目录创建要素类
                                        LogInfo($"正在创建要素类: {layerName}");
                                        schemaBuilder.Create(fcDescription);
                                    }
                                    
                                    if (schemaBuilder.Build())
                                    {
                                        LogInfo($"要素类 {layerName} 创建成功");
                                        if (!string.IsNullOrEmpty(layer.LayerAlias))
                                        {
                                            LogInfo($"要素类 {layerName} 别名设置为 {layer.LayerAlias}");
                                        }
                                        if (fieldDescriptions.Count > 0)
                                        {
                                            LogInfo($"已添加 {fieldDescriptions.Count} 个字段");
                                        }
                                    }
                                    else
                                    {
                                        var errorInfo = schemaBuilder.ErrorMessages;
                                        var errorMsg = errorInfo != null && errorInfo.Count > 0 
                                            ? string.Join("; ", errorInfo) 
                                            : "未知错误";
                                        LogError($"创建要素类 {layerName} 失败: {errorMsg}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogError($"创建要素类 {layerName} 失败: {ex.Message}");
                                }
                            }
                            else
                            {
                                LogInfo($"要素类 {layerName} 已存在");
                            }
                        }
                    }
                });

                LogInfo("建库完成！");
            }
            catch (Exception ex)
            {
                LogError($"建库过程中出错: {ex.Message}");
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
        /// 停止建库
        /// </summary>
        private void StopBuildDatabase()
        {
            _cancellationTokenSource?.Cancel();
            LogWarning("正在停止操作...");
        }
    }
}
