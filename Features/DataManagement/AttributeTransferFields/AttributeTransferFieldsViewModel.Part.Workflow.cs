using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.DataManagement.AttributeTransferFields
{
    public partial class AttributeTransferFieldsViewModel
    {
        private void RaiseAllCanExecutes()
        {
            RefreshDatasetsCommand?.RaiseCanExecuteChanged();
            AutoMapCommand?.RaiseCanExecuteChanged();
            AddMappingCommand?.RaiseCanExecuteChanged();
            RemoveSelectedMappingsCommand?.RaiseCanExecuteChanged();
            ClearMappingsCommand?.RaiseCanExecuteChanged();
            StartCommand?.RaiseCanExecuteChanged();
            StopCommand?.RaiseCanExecuteChanged();
        }
        private bool CanStart()
        {
            return !IsProcessing && SelectedPrimary != null && SelectedSecondary != null &&
                   SelectedPrimaryKeyField != null && SelectedSecondaryKeyField != null &&
                   FieldMappings.Any(m => !string.IsNullOrWhiteSpace(m.SourceFieldName) && !string.IsNullOrWhiteSpace(m.TargetFieldName));
        }
        private async void StartTransfer()
        {
            LogText = string.Empty;
            LogInfo("开始执行属性传递...");
            IsProcessing = true;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            try
            {
                var dirP2S = IsPrimaryToSecondary;
                var keySrc = dirP2S ? SelectedPrimaryKeyField?.Name : SelectedSecondaryKeyField?.Name;
                var keyTgt = dirP2S ? SelectedSecondaryKeyField?.Name : SelectedPrimaryKeyField?.Name;

                var srcDS = dirP2S ? SelectedPrimary : SelectedSecondary;
                var tgtDS = dirP2S ? SelectedSecondary : SelectedPrimary;

                // 有效映射集合
                var effectiveMappings = FieldMappings
                    .Where(m => !string.IsNullOrWhiteSpace(m.SourceFieldName) && !string.IsNullOrWhiteSpace(m.TargetFieldName))
                    .ToList();

                await QueuedTask.Run(async () =>
                {
                    try
                    {
                        var srcTable = TryGetTable(srcDS);
                        if (srcTable == null)
                        {
                            LogError("源数据表获取失败。");
                            return;
                        }
                        var tgtTable = TryGetTable(tgtDS);
                        if (tgtTable == null)
                        {
                            LogError("目标数据表获取失败。");
                            srcTable.Dispose();
                            return;
                        }
                        using (srcTable)
                        using (tgtTable)
                        {
                            // 读取源：key -> 字段值字典
                            var neededSrcFields = new HashSet<string>(effectiveMappings.Select(m => m.SourceFieldName), StringComparer.OrdinalIgnoreCase);
                            neededSrcFields.Add(keySrc);

                            var srcDict = new Dictionary<string, Dictionary<string, object>>();
                            var dupKeys = new HashSet<string>();
                            using (var cursor = srcTable.Search(new QueryFilter(), false))
                            {
                                while (cursor.MoveNext())
                                {
                                    if (_cts.IsCancellationRequested) return;
                                    using (var row = cursor.Current)
                                    {
                                        var k = NormalizeKey(row[keySrc]);
                                        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                                        foreach (var fname in neededSrcFields)
                                        {
                                            try { values[fname] = row[fname]; } catch { values[fname] = null; }
                                        }
                                        if (srcDict.ContainsKey(k)) dupKeys.Add(k);
                                        else srcDict[k] = values;
                                    }
                                }
                            }
                            if (dupKeys.Count > 0)
                                LogWarning($"源数据中存在 {dupKeys.Count} 个重复键，将使用首次出现的记录。");

                            // 执行编辑
                            var op = new EditOperation { Name = "属性传递[字段]" };
                            int updateCount = 0, missCount = 0;

                            using (var cursor = tgtTable.Search(new QueryFilter(), false))
                            {
                                while (cursor.MoveNext())
                                {
                                    if (_cts.IsCancellationRequested) break;
                                    using (var row = cursor.Current)
                                    {
                                        var k = NormalizeKey(row[keyTgt]);
                                        if (!srcDict.TryGetValue(k, out var srcVals)) { missCount++; continue; }

                                        var updates = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                                        foreach (var map in effectiveMappings)
                                        {
                                            var srcName = map.SourceFieldName;
                                            var tgtName = map.TargetFieldName;

                                            // 按当前方向确定最终源/目标字段
                                            string finalSource = dirP2S ? srcName : tgtName;
                                            string finalTarget = dirP2S ? tgtName : srcName;

                                            object v = null;
                                            srcVals?.TryGetValue(finalSource, out v);

                                            // 简单类型兼容检查，不兼容则跳过
                                            if (!IsTypeCompatible(srcTable, tgtTable, finalSource, finalTarget))
                                            {
                                                LogWarning($"字段类型不兼容，已跳过: {finalSource} -> {finalTarget}");
                                                continue;
                                            }

                                            // 只填空值：当目标已有值时跳过
                                            if (OnlyFillEmpty)
                                            {
                                                object curr = null;
                                                try { curr = row[finalTarget]; } catch { curr = null; }
                                                if (!IsNullOrEmptyValue(curr))
                                                {
                                                    continue;
                                                }
                                            }

                                            updates[finalTarget] = v;
                                        }

                                        if (updates.Count > 0)
                                        {
                                            op.Modify(tgtTable, row.GetObjectID(), updates);
                                            updateCount++;
                                        }
                                    }
                                }
                            }

                            bool ok = op.Execute();
                            if (!ok)
                            {
                                throw new Exception("编辑操作执行失败");
                            }
                            LogInfo($"属性传递完成：更新 {updateCount} 条；未匹配 {missCount} 条。");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"执行属性传递时出错: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogError($"执行时出错: {ex.Message}");
            }
            finally
            {
                IsProcessing = false;
                _cts?.Dispose();
                _cts = null;
            }
        }
    }
}
