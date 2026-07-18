using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Analysis.DataPivot
{
    internal partial class DataPivotDockPaneViewModel
    {

        private async void LoadInputDatasets()
        {
            try
            {
                var datasets = await QueuedTask.Run(() =>
                {
                    var items = new List<PivotInputDataset>();
                    var map = MapView.Active?.Map;
                    if (map == null)
                        return items;

                    foreach (var layer in map.GetLayersAsFlattenedList().OfType<FeatureLayer>())
                    {
                        try
                        {
                            using var table = layer.GetTable();
                            if (table != null)
                            {
                                items.Add(new PivotInputDataset
                                {
                                    FeatureLayer = layer,
                                    DisplayName = $"{layer.Name}（要素图层）"
                                });
                            }
                        }
                        catch
                        {
                            // 忽略不可访问图层
                        }
                    }

                    foreach (var standaloneTable in map.GetStandaloneTablesAsFlattenedList())
                    {
                        try
                        {
                            using var table = standaloneTable.GetTable();
                            if (table != null)
                            {
                                items.Add(new PivotInputDataset
                                {
                                    StandaloneTable = standaloneTable,
                                    DisplayName = $"{standaloneTable.Name}（独立表）"
                                });
                            }
                        }
                        catch
                        {
                            // 忽略不可访问独立表
                        }
                    }

                    return items;
                });

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    InputDatasets.Clear();
                    foreach (var item in datasets)
                        InputDatasets.Add(item);

                    if (InputDatasets.Count > 0)
                    {
                        SelectedInputDataset = InputDatasets[0];
                        LogInfo($"已加载 {InputDatasets.Count} 个可用输入表。");
                    }
                    else
                    {
                        SelectedInputDataset = null;
                        ClearFieldCollections();
                        LogWarning("未在当前地图中找到可用图层或独立表。");
                    }

                    StatusMessage = "请选择输入表和透视参数。";
                });
            }
            catch (Exception ex)
            {
                LogError($"加载输入表失败: {ex.Message}");
            }
        }

        private async void LoadFieldOptions()
        {
            try
            {
                if (SelectedInputDataset == null)
                {
                    ClearFieldCollections();
                    return;
                }

                var fields = await QueuedTask.Run(() =>
                {
                    var list = new List<PivotFieldOption>();
                    using var table = SelectedInputDataset.OpenTable();
                    if (table == null)
                        return list;

                    var definition = table.GetDefinition();
                    foreach (var field in definition.GetFields())
                    {
                        if (IsUnsupportedField(field.FieldType))
                            continue;

                        list.Add(new PivotFieldOption
                        {
                            FieldName = field.Name,
                            Alias = field.AliasName,
                            FieldType = field.FieldType
                        });
                    }

                    return list;
                });

                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    ClearFieldCollections();

                    foreach (var field in fields)
                    {
                        var regionField = CloneField(field);
                        regionField.PropertyChanged += RegionFieldOnPropertyChanged;
                        RegionFields.Add(regionField);

                        PivotFields.Add(CloneField(field));

                        if (IsValueFieldSupported(field.FieldType))
                            ValueFields.Add(CloneField(field));
                    }

                    if (RegionFields.Count > 0)
                        RegionFields[0].IsSelected = true;

                    SelectedPivotField = PivotFields.FirstOrDefault();
                    SelectedValueField = ValueFields.FirstOrDefault(IsNumericField) ?? ValueFields.FirstOrDefault();

                    NotifyPropertyChanged(() => CanProcess);
                });
            }
            catch (Exception ex)
            {
                LogError($"加载字段失败: {ex.Message}");
            }
        }

        private static string GetProjectDefaultGdbPath()
        {
            return Project.Current?.DefaultGeodatabasePath ?? string.Empty;
        }

        private string ResolveTemporaryWorkspacePath()
        {
            return _workspaceResolver.ResolveTemporaryWorkspacePath(
                OutputGdbPath,
                GetProjectDefaultGdbPath());
        }

        private static string GetStatisticsType(string aggregationType)
        {
            return aggregationType switch
            {
                "求和" => "SUM",
                "计数" => "COUNT",
                "平均值" => "MEAN",
                "最大值" => "MAX",
                "最小值" => "MIN",
                "中位数" => "MEDIAN",
                "极差" => "RANGE",
                "标准差" => "STD",
                "方差" => "VARIANCE",
                _ => "SUM"
            };
        }
    }
}
