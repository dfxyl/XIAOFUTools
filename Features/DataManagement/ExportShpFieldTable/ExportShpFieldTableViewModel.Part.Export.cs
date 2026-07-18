using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Core;
using XIAOFUTools.Features.DataManagement.ExportShpFieldTable.Infrastructure;

namespace XIAOFUTools.Features.DataManagement.ExportShpFieldTable
{
    public partial class ExportShpFieldTableViewModel
    {
        private readonly IShpSchemaExcelExporter _excelExporter = new ShpSchemaExcelExporter();

        private async void StartExport()
        {
            IsProcessing = true;
            LogText = string.Empty;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;
            var startTime = DateTime.Now;
            LogInfo($"开始时间: {startTime:yyyy年MM月dd日 HH:mm:ss}");
            try
            {
                if (!_fileStore.DirectoryExists(InputFolderPath)) { LogError("输入目录不存在。"); return; }
                var shpFiles = _fileStore.GetTopLevelShapefiles(InputFolderPath);
                if (shpFiles.Count == 0) { LogWarning("输入目录中未找到SHP文件。"); return; }
                _fileStore.EnsureOutputDirectory(OutputExcelPath);

                var layers = new List<ShpLayerSchemaInfo>();
                var failures = 0;
                LogInfo("正在读取SHP字段结构...");
                await QueuedTask.Run(() =>
                {
                    var connection = new FileSystemConnectionPath(new Uri(InputFolderPath), FileSystemDatastoreType.Shapefile);
                    using var datastore = new FileSystemDatastore(connection);
                    foreach (var path in shpFiles)
                    {
                        if (token.IsCancellationRequested) return;
                        var name = Path.GetFileNameWithoutExtension(path);
                        try
                        {
                            using var featureClass = datastore.OpenDataset<FeatureClass>(name);
                            var definition = featureClass.GetDefinition();
                            var layer = new ShpLayerSchemaInfo { Name = name, AliasName = name, GeometryType = ConvertGeometryType(definition.GetShapeType()) };
                            foreach (var field in definition.GetFields())
                            {
                                if (IsSystemField(field.Name)) continue;
                                layer.Fields.Add(new ShpFieldSchemaInfo { FieldName = field.Name, AliasName = field.AliasName, FieldType = ConvertFieldType(field.FieldType), Length = field.FieldType == FieldType.String ? field.Length : null, Scale = field.Scale > 0 ? field.Scale : null });
                            }
                            layers.Add(layer);
                            LogInfo($"读取完成: {name}，字段数 {layer.Fields.Count}");
                        }
                        catch (Exception exception) { failures++; LogError($"读取失败: {name}，{exception.Message}"); }
                    }
                });
                token.ThrowIfCancellationRequested();
                if (layers.Count == 0) { LogError("没有可导出的SHP结构。"); return; }
                LogInfo("正在写出字段表...");
                await _excelExporter.ExportAsync(layers, OutputExcelPath, token);
                LogInfo($"导出完成！成功读取 {layers.Count} 个SHP，读取失败 {failures} 个");
                LogInfo($"输出文件: {OutputExcelPath}");
            }
            catch (OperationCanceledException) { LogWarning("操作已被用户取消。"); }
            catch (Exception exception) { LogError($"导出过程中出错: {exception.Message}"); }
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

        private void StopExport()
        {
            _cancellationTokenSource?.Cancel();
            LogWarning("正在停止操作...");
        }
    }
}
