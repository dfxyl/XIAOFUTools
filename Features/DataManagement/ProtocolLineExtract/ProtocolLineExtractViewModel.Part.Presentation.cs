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

        private void AddLog(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return;
            LogContent += $"[{DateTime.Now:HH:mm:ss}] {msg}\r\n";
        }

        public async void RefreshLayers()
        {
            try
            {
                var layers = await QueuedTask.Run(() =>
                {
                    var map = MapView.Active?.Map;
                    if (map == null) return new List<FeatureLayer>();
                    return map.GetLayersAsFlattenedList()
                        ?.OfType<FeatureLayer>()
                        .Where(fl => fl.ShapeType == esriGeometryType.esriGeometryPolygon)
                        .ToList() ?? new List<FeatureLayer>();
                });

                PolygonLayers.Clear();

                if (layers.Count == 0)
                {
                    AddLog("当前没有活动地图");
                    return;
                }

                foreach (var l in layers) PolygonLayers.Add(l);
                if (PolygonLayers.Count > 0 && SelectedPolygonLayer == null)
                    SelectedPolygonLayer = PolygonLayers[0];

                AddLog($"已加载 {PolygonLayers.Count} 个面要素图层");
                UpdateSelectionInfo();
            }
            catch (System.Exception ex)
            {
                AddLog($"刷新图层失败: {ex.Message}");
            }
        }

        private void UpdateSelectionInfo()
        {
            Task.Run(async () =>
            {
                var info = await SelectionUtils.GetSelectionInfoAsync(SelectedPolygonLayer);
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    UseSelection = SelectionUtils.RecommendUseSelection(UseSelection, info.HasSelection);
                    HasSelection = info.HasSelection;
                    SelectedCount = info.Count;
                    SelectionInfoText = info.InfoText;
                });
            });
        }

        /// <summary>
        /// 选择保留字段
        /// </summary>
        private async void SelectFields()
        {
            if (SelectedPolygonLayer == null) return;

            try
            {
                // 先在MCT线程上获取字段列表
                var fields = await QueuedTask.Run(() =>
                {
                    try
                    {
                        using (var table = SelectedPolygonLayer.GetTable())
                        {
                            if (table != null)
                            {
                                var definition = table.GetDefinition();
                                return definition.GetFields().ToList();
                            }
                            return null;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        AddLog($"获取字段列表失败: {ex.Message}");
                        return null;
                    }
                });

                if (fields == null)
                {
                    AddLog("无法获取图层表格");
                    return;
                }

                // 回到UI线程显示对话框
                var selectedFields = Shared.Presentation.Dialogs.FieldSelectionDialog.Select(fields, SelectedFields);
                if (selectedFields is not null)
                {
                    SelectedFields = selectedFields.ToList();
                    AddLog($"已选择 {SelectedFields.Count} 个保留字段");
                }
            }
            catch (System.Exception ex)
            {
                AddLog($"获取字段列表失败: {ex.Message}");
            }
        }

        private void UpdateDefaultOutputPath()
        {
            var proj = Project.Current;
            var basePath = proj?.DefaultGeodatabasePath;
            var name = SelectedPolygonLayer != null ? $"{SelectedPolygonLayer.Name}_协议线" : "协议线";
            if (!string.IsNullOrEmpty(basePath))
                OutputPath = Path.Combine(basePath, name);
            else
                OutputPath = name;
        }

        private void BrowseOutput()
        {
            try
            {
                var init = Project.Current?.DefaultGeodatabasePath;
                var selectedPath = PathDialogUtils.PickSaveFeatureClassPath("选择输出位置", init);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    OutputPath = selectedPath;
                }
            }
            catch (System.Exception ex)
            {
                AddLog($"选择输出位置失败: {ex.Message}");
            }
        }

        private void ShowHelp()
        {
            AddLog("帮助: 根据相邻面共享边提取协议线。使用 PolygonToLine(识别邻接) + 选择左右邻接均存在的线段，可选合并端点相连的线段。");
        }
    }
}
