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
        private async void LoadDatasets()
        {
            try
            {
                LogInfo("正在加载图层/表...");
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        var map = MapView.Active?.Map;
                        if (map == null) return;

                        var flayers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().ToList();
                        var tables = map.GetStandaloneTablesAsFlattenedList().ToList();
                        LogInfo($"图层数: {flayers.Count}，独立表数: {tables.Count}");

                        // 在 MCT 线程过滤，确保仅保留可获取 Table 的数据集（支持 FeatureLayer.GetFeatureClass 兜底）
                        var valid = new List<DatasetInfo>();
                        foreach (var fl in flayers)
                        {
                            var tmp = new DatasetInfo { FeatureLayer = fl, DisplayName = fl?.Name };
                            using (var tbl = TryGetTable(tmp))
                            {
                                if (tbl != null) valid.Add(tmp);
                            }
                        LogInfo($"可用数据源: {valid.Count}");
                        }
                        foreach (var t in tables)
                        {
                            var tmp = new DatasetInfo { StandaloneTable = t, DisplayName = t?.Name };
                            using (var tbl = TryGetTable(tmp))
                            {
                                if (tbl != null) valid.Add(tmp);
                            }
                        }

                        PresentationServices.UiThread.InvokeOrRun(() =>
                        {
                            // 重置选择与字段列表，避免旧选择触发字段加载
                            SelectedPrimary = null;
                            SelectedSecondary = null;
                            PrimaryFieldList.Clear();
                            SecondaryFieldList.Clear();
                            PrimaryList.Clear();
                            SecondaryList.Clear();
                            foreach (var ds in valid)
                            {
                                // 分别为主/从构造独立实例，避免共享同一引用造成潜在绑定混淆
                                PrimaryList.Add(new DatasetInfo { FeatureLayer = ds.FeatureLayer, StandaloneTable = ds.StandaloneTable, DisplayName = ds.DisplayName });
                                SecondaryList.Add(new DatasetInfo { FeatureLayer = ds.FeatureLayer, StandaloneTable = ds.StandaloneTable, DisplayName = ds.DisplayName });
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        LogError($"加载图层/表时出错: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }
        private async void LoadPrimaryFields()
        {
            try
            {
                await LoadFieldsInternal(SelectedPrimary, PrimaryFieldList, sideName: "主");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }
        private async void LoadSecondaryFields()
        {
            try
            {
                await LoadFieldsInternal(SelectedSecondary, SecondaryFieldList, sideName: "从");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
        }
        private async Task LoadFieldsInternal(DatasetInfo ds, ObservableCollection<FieldInfo> target, string sideName)
        {
            if (ds == null)
            {
                target.Clear();
                RaiseAllCanExecutes();
                return;
            }
            await QueuedTask.Run(() =>
            {
                try
                {
                    var tbl = TryGetTable(ds);
                    if (tbl == null)
                    {
                        LogError($"加载{sideName}字段失败：无法获取数据表（{GetDsLabel(ds)}）。");
                        PresentationServices.UiThread.InvokeOrRun(() => { target.Clear(); RaiseAllCanExecutes(); });
                        return;
                    }
                    using (tbl)
                    {
                        var def = tbl.GetDefinition();
                        if (def == null)
                        {
                            LogError($"加载{sideName}字段失败：无法获取表定义。");
                            PresentationServices.UiThread.InvokeOrRun(() => { target.Clear(); RaiseAllCanExecutes(); });
                            return;
                        }
                        var fields = def.GetFields();
                        PresentationServices.UiThread.InvokeOrRun(() =>
                        {
                            target.Clear();
                            foreach (var f in fields)
                            {
                                // 排除不适合映射的字段
                                if (f.FieldType == FieldType.Geometry || f.FieldType == FieldType.OID || f.FieldType == FieldType.GlobalID)
                                    continue;
                                if (IsSystemOrNonWritable(f.Name))
                                    continue;
                                target.Add(new FieldInfo
                                {
                                    Name = f.Name,
                                    Alias = f.AliasName,
                                    FieldType = f.FieldType,
                                    Length = f.Length
                                });
                            }
                            RaiseAllCanExecutes();
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogError($"加载{sideName}字段时出错: {ex.Message}");
                    PresentationServices.UiThread.InvokeOrRun(() => { target.Clear(); RaiseAllCanExecutes(); });
                }
            });
        }
        private static string GetDsLabel(DatasetInfo ds)
        {
            if (ds == null) return "<null>";
            var type = ds.FeatureLayer != null ? "要素图层" : (ds.StandaloneTable != null ? "独立表" : "未知");
            return $"{ds.DisplayName ?? ds.Name} | {type}";
        }
        private static Table TryGetTable(DatasetInfo ds)
        {
            try
            {
                if (ds == null) return null;
                if (ds.FeatureLayer != null)
                {
                    var tbl = ds.FeatureLayer.GetTable();
                    if (tbl != null) return tbl;
                    // 兜底：直接获取 FeatureClass
                    var fc = ds.FeatureLayer.GetFeatureClass();
                    return fc; // FeatureClass 继承自 Table
                }
                if (ds.StandaloneTable != null)
                {
                    return ds.StandaloneTable.GetTable();
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
