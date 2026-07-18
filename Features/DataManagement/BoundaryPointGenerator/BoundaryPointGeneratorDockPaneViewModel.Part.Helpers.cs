using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Extensions;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BoundaryPointGenerator
{
    internal partial class BoundaryPointGeneratorDockPaneViewModel
    {

        /// <summary>
        /// 插入点要素
        /// </summary>
        private async Task InsertPointFeatures(string outputFeatureClassPath, List<(MapPoint point, Dictionary<string, object> attributes)> pointsToCreate)
        {
            try
            {
                await QueuedTask.Run(() =>
                {
                    // 打开输出要素类（兼容 GDB 根、要素数据集路径与 Shapefile）
                    var catalogPath = outputFeatureClassPath;
                    if (string.IsNullOrEmpty(catalogPath))
                    {
                        LogError("无效的输出要素类路径");
                        return;
                    }

                    FeatureClass featureClass = null;
                    Geodatabase gdb = null;
                    FileSystemDatastore fsds = null;
                    try
                    {
                        if (catalogPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
                        {
                            var folder = Path.GetDirectoryName(catalogPath);
                            var shpName = Path.GetFileName(catalogPath);
                            var conn = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                            fsds = new FileSystemDatastore(conn);
                            featureClass = fsds.OpenDataset<FeatureClass>(shpName);
                        }
                        else
                        {
                            int gdbIdx = catalogPath.IndexOf(".gdb", StringComparison.OrdinalIgnoreCase);
                            if (gdbIdx >= 0)
                            {
                                var gdbRoot = catalogPath.Substring(0, gdbIdx + 4);
                                var relative = catalogPath.Length > gdbIdx + 4 ? catalogPath.Substring(gdbIdx + 4).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : string.Empty;
                                if (string.IsNullOrEmpty(relative))
                                {
                                    LogError("GDB 输出路径缺少要素类名");
                                    return;
                                }
                                gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(gdbRoot)));
                                featureClass = gdb.OpenDataset<FeatureClass>(relative);
                            }
                            else
                            {
                                // 兜底：尝试按目录 + 名称方式打开
                                var workspace = Path.GetDirectoryName(catalogPath);
                                var featureClassName = Path.GetFileNameWithoutExtension(catalogPath);
                                bool isGdb = workspace.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase);
                                if (isGdb)
                                {
                                    gdb = new Geodatabase(new FileGeodatabaseConnectionPath(new Uri(workspace)));
                                    featureClass = gdb.OpenDataset<FeatureClass>(featureClassName);
                                }
                                else
                                {
                                    var conn = new FileSystemConnectionPath(new Uri(workspace), FileSystemDatastoreType.Shapefile);
                                    fsds = new FileSystemDatastore(conn);
                                    featureClass = fsds.OpenDataset<FeatureClass>(featureClassName + ".shp");
                                }
                            }
                        }

                        int insertedCount = 0;
                        
                        foreach (var pointData in pointsToCreate)
                        {
                            if (CancelRequested) break;

                            try
                            {
                                using (var rowBuffer = featureClass.CreateRowBuffer())
                                {
                                    // 设置几何
                                    rowBuffer[featureClass.GetDefinition().GetShapeField()] = pointData.point;

                                    // 设置属性
                                    foreach (var attr in pointData.attributes)
                                    {
                                        try
                                        {
                                            rowBuffer[attr.Key] = attr.Value;
                                        }
                                        catch (Exception ex)
                                        {
                                            LogWarning($"设置字段 {attr.Key} 的值失败: {ex.Message}");
                                        }
                                    }

                                    using (var feature = featureClass.CreateRow(rowBuffer))
                                    {
                                        feature.Store();
                                        insertedCount++;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError($"插入点要素失败: {ex.Message}");
                            }
                        }

                        LogInfo($"成功插入 {insertedCount} 个点要素");
                    }
                    finally
                    {
                        featureClass?.Dispose();
                        gdb?.Dispose();
                        fsds?.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"插入点要素时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理事件订阅
        /// </summary>
        public void Cleanup()
        {
            if (_mapSelectionChangedToken != null)
            {
                MapSelectionChangedEvent.Unsubscribe(_mapSelectionChangedToken);
                _mapSelectionChangedToken = null;
            }
        }
    }
}
