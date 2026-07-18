using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace XIAOFUTools.Features.DataManagement.MirrorDatabase
{
    public partial class MirrorDatabaseViewModel
    {

        private bool CanStart()
        {
            return !IsProcessing &&
                   !string.IsNullOrWhiteSpace(SourceDatabasePath) &&
                   !string.IsNullOrWhiteSpace(OutputFolderPath) &&
                   !string.IsNullOrWhiteSpace(DatabaseName);
        }

        private async void StartMirrorDatabase()
        {
            IsProcessing = true;
            LogText = "";
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            var startTime = DateTime.Now;
            LogInfo("开始时间: " + startTime.ToString("yyyy年MM月dd日 HH:mm:ss"));

            try
            {
                if (!_pathStore.DirectoryExists(SourceDatabasePath))
                {
                    LogError("指定的源数据库不存在。");
                    return;
                }

                string targetGdbPath = Path.Combine(OutputFolderPath, DatabaseName + ".gdb");

                if (!_pathStore.DirectoryExists(targetGdbPath))
                {
                    LogInfo("正在创建目标数据库: " + DatabaseName + ".gdb");
                    try
                    {
                        var args = Geoprocessing.MakeValueArray(OutputFolderPath, DatabaseName);
                        var result = await Geoprocessing.ExecuteToolAsync(
                            "CreateFileGDB_management",
                            args,
                            null,
                            _cancellationTokenSource.Token,
                            null,
                            GPExecuteToolFlags.GPThread);
                        if (result.IsFailed)
                        {
                            LogError("创建目标数据库失败: " + string.Join("; ", result.Messages.Select(message => message.Text)));
                            return;
                        }

                        LogInfo("目标数据库 " + DatabaseName + ".gdb 创建成功");
                    }
                    catch (Exception ex)
                    {
                        LogError("创建目标数据库失败: " + ex.Message);
                        return;
                    }
                }
                else
                {
                    LogInfo("目标数据库 " + DatabaseName + ".gdb 已存在，将在其中创建结构");
                }

                await QueuedTask.Run(() =>
                {
                    using (var sourceGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(SourceDatabasePath))))
                    using (var targetGdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(targetGdbPath))))
                    {
                        var processedFCs = new HashSet<string>();

                        // 镜像要素集
                        var featureDatasetDefs = sourceGdb.GetDefinitions<FeatureDatasetDefinition>();
                        LogInfo("源数据库包含 " + featureDatasetDefs.Count() + " 个要素集");

                        foreach (var datasetDef in featureDatasetDefs)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                return;
                            }

                            string datasetName = datasetDef.GetName();
                            LogInfo("正在镜像要素集: " + datasetName);

                            try
                            {
                                bool exists = false;
                                try
                                {
                                    var existingDef = targetGdb.GetDefinition<FeatureDatasetDefinition>(datasetName);
                                    exists = existingDef != null;
                                }
                                catch { exists = false; }

                                if (!exists)
                                {
                                    var spatialRef = datasetDef.GetSpatialReference();
                                    var newDatasetDesc = new FeatureDatasetDescription(datasetName, spatialRef);
                                    var schemaBuilder = new SchemaBuilder(targetGdb);
                                    schemaBuilder.Create(newDatasetDesc);
                                    if (schemaBuilder.Build())
                                    {
                                        LogInfo("  要素集 " + datasetName + " 创建成功");
                                    }
                                    else
                                    {
                                        LogError("  创建要素集 " + datasetName + " 失败");
                                    }
                                }
                                else
                                {
                                    LogInfo("  要素集 " + datasetName + " 已存在");
                                }

                                // 镜像要素集内的要素类
                                using (var featureDataset = sourceGdb.OpenDataset<FeatureDataset>(datasetName))
                                {
                                    var fcDefs = featureDataset.GetDefinitions<FeatureClassDefinition>();
                                    foreach (var fcDef in fcDefs)
                                    {
                                        if (_cancellationTokenSource.Token.IsCancellationRequested)
                                        {
                                            LogWarning("操作已被用户取消");
                                            return;
                                        }

                                        string fcName = fcDef.GetName();
                                        processedFCs.Add(fcName);
                                        LogInfo("  正在镜像要素类: " + fcName);

                                        try
                                        {
                                            bool fcExists = false;
                                            try
                                            {
                                                var existingDef = targetGdb.GetDefinition<FeatureClassDefinition>(fcName);
                                                fcExists = existingDef != null;
                                            }
                                            catch { fcExists = false; }

                                            if (!fcExists)
                                            {
                                                MirrorFeatureClassInDataset(targetGdb, fcDef, datasetName);
                                            }
                                            else
                                            {
                                                LogInfo("    要素类 " + fcName + " 已存在");
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogError("    镜像要素类 " + fcName + " 失败: " + ex.Message);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError("  镜像要素集 " + datasetName + " 失败: " + ex.Message);
                            }
                        }

                        // 镜像根目录下的要素类
                        var featureClassDefs = sourceGdb.GetDefinitions<FeatureClassDefinition>();
                        int rootFcCount = 0;
                        foreach (var fcDef in featureClassDefs)
                        {
                            string fcName = fcDef.GetName();
                            if (!processedFCs.Contains(fcName))
                            {
                                rootFcCount++;
                            }
                        }
                        LogInfo("源数据库根目录包含 " + rootFcCount + " 个要素类");

                        foreach (var fcDef in featureClassDefs)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                return;
                            }

                            string fcName = fcDef.GetName();
                            if (!processedFCs.Contains(fcName))
                            {
                                LogInfo("正在镜像要素类: " + fcName);

                                try
                                {
                                    bool exists = false;
                                    try
                                    {
                                        var existingDef = targetGdb.GetDefinition<FeatureClassDefinition>(fcName);
                                        exists = existingDef != null;
                                    }
                                    catch { exists = false; }

                                    if (!exists)
                                    {
                                        MirrorFeatureClass(targetGdb, fcDef);
                                    }
                                    else
                                    {
                                        LogInfo("  要素类 " + fcName + " 已存在");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogError("  镜像要素类 " + fcName + " 失败: " + ex.Message);
                                }
                            }
                        }

                        // 镜像独立表
                        var tableDefs = sourceGdb.GetDefinitions<TableDefinition>();
                        var standaloneTables = tableDefs.Where(t => !(t is FeatureClassDefinition)).ToList();
                        LogInfo("源数据库包含 " + standaloneTables.Count + " 个独立表");

                        foreach (var tableDef in standaloneTables)
                        {
                            if (_cancellationTokenSource.Token.IsCancellationRequested)
                            {
                                LogWarning("操作已被用户取消");
                                return;
                            }

                            string tableName = tableDef.GetName();
                            LogInfo("正在镜像表: " + tableName);

                            try
                            {
                                bool exists = false;
                                try
                                {
                                    var existingDef = targetGdb.GetDefinition<TableDefinition>(tableName);
                                    exists = existingDef != null;
                                }
                                catch { exists = false; }

                                if (!exists)
                                {
                                    MirrorTable(targetGdb, tableDef);
                                }
                                else
                                {
                                    LogInfo("  表 " + tableName + " 已存在");
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError("  镜像表 " + tableName + " 失败: " + ex.Message);
                            }
                        }
                    }
                });

                LogInfo("镜像完成！");
            }
            catch (Exception ex)
            {
                LogError("镜像过程中出错: " + ex.Message);
            }
            finally
            {
                var endTime = DateTime.Now;
                LogInfo("结束时间: " + endTime.ToString("yyyy年MM月dd日 HH:mm:ss"));
                LogInfo("历时: " + (endTime - startTime).ToString());
                IsProcessing = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }
    }
}
