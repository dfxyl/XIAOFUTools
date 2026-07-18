using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
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
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ProtocolLineExtract
{
    internal partial class ProtocolLineExtractViewModel
    {

        private async Task RunAsync()
        {
            if (SelectedPolygonLayer == null)
            {
                AddLog("请选择输入面图层");
                return;
            }
            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                AddLog("请指定输出路径");
                return;
            }

            IsProcessing = true;
            IsProgressIndeterminate = true;
            StatusMessage = "正在提取协议线...";
            Progress = 0;
            _cts = new CancellationTokenSource();

            try
            {
                await QueuedTask.Run(async () =>
                {
                    await ProcessCoreAsync(_cts.Token);
                });

                if (!_cts.IsCancellationRequested)
                {
                    StatusMessage = "提取完成";
                    Progress = 100;
                    AddLog("协议线提取完成");
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
                AddLog("操作已被取消");
            }
            catch (System.Exception ex)
            {
                StatusMessage = "处理失败";
                AddLog($"处理失败: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                IsProgressIndeterminate = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task ProcessCoreAsync(CancellationToken token)
        {
            string tempWS = null;
            string polygonToLinePath = null;
            string selectedLinesPath = null;
            string selectedLinesSelPath = null;
            string mergedLinesPath = null;
            try
            {
                // 1. 准备临时目录
                tempWS = _temporaryWorkspaceStore.CreateWorkspace();
                polygonToLinePath = Path.Combine(tempWS, "poly2line.shp");
                selectedLinesPath = Path.Combine(tempWS, "proto_lines.shp");
                selectedLinesSelPath = Path.Combine(tempWS, "proto_lines_sel.shp");
                mergedLinesPath = Path.Combine(tempWS, "proto_merged.shp");

                // 环境设置：不添加到地图，允许覆盖
                var env = Geoprocessing.MakeEnvironmentArray("addOutputsToMap", "False", "overwriteoutput", "True");

                // 2. PolygonToLine(识别邻接)
                AddLog("正在将面转线并识别邻接...");
                var p2lParams = Geoprocessing.MakeValueArray(
                    SelectedPolygonLayer,
                    polygonToLinePath,
                    "", // cluster_tolerance
                    "IDENTIFY_NEIGHBORS" // neighbor_option
                );
                var p2lResult = await Geoprocessing.ExecuteToolAsync("PolygonToLine_management", p2lParams, env, token);
                if (p2lResult.IsFailed)
                {
                    AddLog("PolygonToLine 失败");
                    foreach (var m in p2lResult.Messages) AddLog(" - " + m.Text);
                    return;
                }

                // 3. 选择左右邻接均存在的线段（内边界）
                AddLog("正在筛选左右均有邻接的线段...");
                var where = "LEFT_FID <> -1 AND RIGHT_FID <> -1";
                var selectParams = Geoprocessing.MakeValueArray(
                    polygonToLinePath,
                    selectedLinesPath,
                    where
                );
                var selectResult = await Geoprocessing.ExecuteToolAsync("Select_analysis", selectParams, env, token);
                if (selectResult.IsFailed)
                {
                    AddLog("筛选失败");
                    foreach (var m in selectResult.Messages) AddLog(" - " + m.Text);
                    return;
                }

                // 如果启用选择集，则基于 LEFT_FID/RIGHT_FID 进一步过滤
                string workingLinesPath = selectedLinesPath;
                if (UseSelection && SelectedPolygonLayer != null && SelectedPolygonLayer.SelectionCount > 0)
                {
                    AddLog($"按选择集过滤协议线（所选 {SelectedPolygonLayer.SelectionCount} 个要素）...");
                    var selection = SelectedPolygonLayer.GetSelection();
                    if (selection == null)
                    {
                        AddLog("无法获取当前选择集，跳过选择过滤");
                    }
                    else
                    {
                        var oids = new List<long>();
                        using (var cur = selection.Search(new QueryFilter(), false))
                        {
                            while (cur != null && cur.MoveNext())
                            {
                                using var row = cur.Current;
                                if (row != null)
                                    oids.Add(row.GetObjectID());
                            }
                        }
                        if (oids.Count > 0)
                        {
                            string oidList = string.Join(",", oids);
                            string whereSel = $"LEFT_FID IN ({oidList}) OR RIGHT_FID IN ({oidList})";
                            var selectParams2 = Geoprocessing.MakeValueArray(
                                selectedLinesPath,
                                selectedLinesSelPath,
                                whereSel
                            );
                            var selectResult2 = await Geoprocessing.ExecuteToolAsync("Select_analysis", selectParams2, env, token);
                            if (!selectResult2.IsFailed)
                                workingLinesPath = selectedLinesSelPath;
                            else
                                AddLog("按选择集过滤失败，继续使用未过滤结果");
                        }
                    }
                }

                // 检查是否有要素
                var countParams = Geoprocessing.MakeValueArray(workingLinesPath);
                var countResult = await Geoprocessing.ExecuteToolAsync("GetCount_management", countParams, env, token);
                if (countResult.ReturnValue == "0")
                {
                    AddLog("未提取到协议线，输出为空");
                    // 创建空输出（按模板）
                    var infoEmpty = OutputDatasetUtils.ParseOutputPath(OutputPath, "协议线");
                    await OutputDatasetUtils.DeleteIfExistsAsync(infoEmpty);
                    var createParams = Geoprocessing.MakeValueArray(
                        infoEmpty.OutPathWorkspace,
                        infoEmpty.IsGdb ? infoEmpty.OutNameNoExt : infoEmpty.OutNameNoExt + ".shp",
                        "POLYLINE",
                        workingLinesPath // 模板（空也可作为模板）
                    );
                    await Geoprocessing.ExecuteToolAsync("CreateFeatureclass_management", createParams, env, token);
                    return;
                }

                // 添加保留字段（TEXT, 长度255），并填充值：左/右用"/"组合
                if (SelectedFields != null && SelectedFields.Count > 0)
                {
                    AddLog("正在新增并填充保留字段...");
                    foreach (var fld in SelectedFields)
                    {
                        try
                        {
                            var addFieldParams = Geoprocessing.MakeValueArray(
                                workingLinesPath,
                                fld,
                                "TEXT",
                                null, null, 255
                            );
                            await Geoprocessing.ExecuteToolAsync("AddField_management", addFieldParams, env, token);
                        }
                        catch { /* 字段可能已存在，忽略错误 */ }
                    }

                    // 使用 Core.Data 写入值
                    try
                    {
                        using var polyTable = SelectedPolygonLayer.GetTable();
                        if (polyTable != null)
                        {
                            var polyVals = new Dictionary<long, Dictionary<string, string>>();
                            using (var pc = polyTable.Search())
                            {
                                while (pc.MoveNext())
                                {
                                    using var prow = pc.Current;
                                    long oid = prow.GetObjectID();
                                    var dict = new Dictionary<string, string>();
                                    foreach (var f in SelectedFields)
                                    {
                                        object v = null;
                                        try { v = prow[f]; } catch { }
                                        dict[f] = v?.ToString() ?? string.Empty;
                                    }
                                    polyVals[oid] = dict;
                                }
                            }

                            var folder = Path.GetDirectoryName(workingLinesPath);
                            var name = Path.GetFileName(workingLinesPath);
                            var fsConn = new FileSystemConnectionPath(new Uri(folder), FileSystemDatastoreType.Shapefile);
                            using var fsds = new FileSystemDatastore(fsConn);
                            using var lineFc = fsds.OpenDataset<FeatureClass>(name);

                            using var lc = lineFc.Search();
                            while (lc.MoveNext())
                            {
                                using var lrow = lc.Current as Row;
                                if (lrow == null) continue;
                                long leftId = 0, rightId = 0;
                                try { leftId = Convert.ToInt64(lrow["LEFT_FID"]); } catch { }
                                try { rightId = Convert.ToInt64(lrow["RIGHT_FID"]); } catch { }

                                foreach (var f in SelectedFields)
                                {
                                    string lv = polyVals.TryGetValue(leftId, out var ldict) && ldict.TryGetValue(f, out var lv0) ? lv0 : string.Empty;
                                    string rv = polyVals.TryGetValue(rightId, out var rdict) && rdict.TryGetValue(f, out var rv0) ? rv0 : string.Empty;
                                    string combo = string.Concat(lv ?? string.Empty, "/", rv ?? string.Empty);
                                    try { lrow[f] = combo; } catch { }
                                }
                                try { lrow.Store(); } catch { }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        AddLog($"填充保留字段失败: {ex.Message}");
                    }
                }

                string finalPath = workingLinesPath;

                // 4. 可选合并端点相连的线段（UnsplitLine）
                if (MergeLines)
                {
                    // 若有保留字段，优先按保留字段 Dissolve 单一部分，以避免属性不一致造成的错误继承
                    if (SelectedFields != null && SelectedFields.Count > 0)
                    {
                        AddLog("正在按保留字段合并线段...");
                        string dissolveFields = string.Join(";", SelectedFields);
                        var dissolveParams = Geoprocessing.MakeValueArray(
                            finalPath,
                            mergedLinesPath,
                            dissolveFields,
                            "",
                            "SINGLE_PART"
                        );
                        var dissolveResult = await Geoprocessing.ExecuteToolAsync("Dissolve_management", dissolveParams, env, token);
                        if (!dissolveResult.IsFailed)
                            finalPath = mergedLinesPath;
                        else
                            AddLog("按保留字段合并失败，将尝试拓扑合并");
                    }

                    if (finalPath != mergedLinesPath)
                    {
                        AddLog("正在拓扑合并端点相连的线段...");
                        var unsplitParams = Geoprocessing.MakeValueArray(
                            finalPath,
                            mergedLinesPath,
                            "" // 不按字段，仅按拓扑端点合并
                        );
                        var unsplitResult = await Geoprocessing.ExecuteToolAsync("UnsplitLine_management", unsplitParams, env, token);
                        if (!unsplitResult.IsFailed)
                            finalPath = mergedLinesPath;
                        else
                            AddLog("拓扑合并失败，使用未合并结果继续");
                    }
                }

                // 5. 复制到输出
                var info = OutputDatasetUtils.ParseOutputPath(OutputPath, "协议线");
                await OutputDatasetUtils.DeleteIfExistsAsync(info);

                AddLog("正在写出到目标要素类...");
                var copyParams = Geoprocessing.MakeValueArray(
                    finalPath,
                    info.CatalogPath
                );
                var copyResult = await Geoprocessing.ExecuteToolAsync("CopyFeatures_management", copyParams, env, token);
                if (copyResult.IsFailed)
                {
                    AddLog("写出失败");
                    foreach (var m in copyResult.Messages) AddLog(" - " + m.Text);
                    return;
                }

                AddLog("协议线已写出");
            }
            finally
            {
                // 先等待GP释放文件句柄，降低地图刷新中的竞争
                try { await Task.Delay(800); } catch { }

                // 从地图移除可能被自动添加的过程图层（按需开启）
                if (RemoveIntermediatesFromMap)
                {
                    try
                    {
                        await RemoveIntermediateLayersAsync(tempWS, polygonToLinePath, selectedLinesPath, selectedLinesSelPath, mergedLinesPath);
                    }
                    catch { /* 忽略清理异常 */ }
                }

                // 清理临时目录
                try
                {
                    _temporaryWorkspaceStore.DeleteIfExists(tempWS);
                }
                catch { }
            }
        }
    }
}
