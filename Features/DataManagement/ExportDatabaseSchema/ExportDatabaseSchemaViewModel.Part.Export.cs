using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Features.DataManagement.ExportDatabaseSchema.Core;
using XIAOFUTools.Features.DataManagement.ExportDatabaseSchema.Infrastructure;

namespace XIAOFUTools.Features.DataManagement.ExportDatabaseSchema
{
    public partial class ExportDatabaseSchemaViewModel
    {
        private readonly IDatabaseSchemaExcelExporter _excelExporter = new DatabaseSchemaExcelExporter();

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
                if (!_pathStore.DirectoryExists(InputGdbPath)) { LogError("指定的数据库不存在。"); return; }
                LogInfo("正在读取数据库结构...");
                var classes = new List<FeatureClassInfo>();
                var datasets = new List<FeatureDatasetInfo>();
                await QueuedTask.Run(() =>
                {
                    using var geodatabase = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(InputGdbPath)));
                    var datasetDefinitions = geodatabase.GetDefinitions<FeatureDatasetDefinition>();
                    var processed = new HashSet<string>();
                    foreach (var definition in datasetDefinitions)
                    {
                        if (token.IsCancellationRequested) return;
                        var name = definition.GetName();
                        datasets.Add(new FeatureDatasetInfo { Index = datasets.Count + 1, DatasetName = name, DatasetAlias = name, Notes = string.Empty });
                        LogInfo($"正在处理要素集: {name}");
                    }
                    foreach (var definition in datasetDefinitions)
                    {
                        if (token.IsCancellationRequested) return;
                        var name = definition.GetName();
                        using var dataset = geodatabase.OpenDataset<FeatureDataset>(name);
                        foreach (var featureClassDefinition in dataset.GetDefinitions<FeatureClassDefinition>())
                        {
                            if (token.IsCancellationRequested) return;
                            var item = ReadFeatureClassInfo(featureClassDefinition, name);
                            classes.Add(item); processed.Add(featureClassDefinition.GetName()); LogInfo($"  读取要素类: {item.Name}");
                        }
                    }
                    foreach (var definition in geodatabase.GetDefinitions<FeatureClassDefinition>())
                    {
                        if (token.IsCancellationRequested) return;
                        if (processed.Contains(definition.GetName())) continue;
                        var item = ReadFeatureClassInfo(definition, null); classes.Add(item); LogInfo($"读取要素类: {item.Name}");
                    }
                    foreach (var definition in geodatabase.GetDefinitions<TableDefinition>())
                    {
                        if (token.IsCancellationRequested) return;
                        var name = definition.GetName();
                        if (processed.Contains(name) || classes.Any(item => item.Name == name)) continue;
                        var item = ReadTableInfo(definition); classes.Add(item); LogInfo($"读取独立表: {item.Name}");
                    }
                });
                token.ThrowIfCancellationRequested();
                LogInfo($"共读取 {datasets.Count} 个要素集，{classes.Count} 个数据集");
                LogInfo("正在导出到Excel...");
                await _excelExporter.ExportAsync(datasets, classes, OutputExcelPath, token);
                LogInfo($"导出完成！文件保存至: {OutputExcelPath}");
            }
            catch (OperationCanceledException) { LogWarning("操作已被用户取消"); }
            catch (Exception exception) { LogError($"导出过程中出错: {exception.Message}"); }
            finally
            {
                var endTime = DateTime.Now;
                LogInfo($"结束时间: {endTime:yyyy年MM月dd日 HH:mm:ss}");
                LogInfo($"历时: {endTime - startTime}");
                IsProcessing = false;
                _cancellationTokenSource?.Dispose(); _cancellationTokenSource = null;
            }
        }

        private void StopExport()
        {
            _cancellationTokenSource?.Cancel();
            LogWarning("正在停止操作...");
        }
    }
}
