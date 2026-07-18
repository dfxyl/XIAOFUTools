using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using ArcGIS.Desktop.Editing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.RotateGeometry
{
    internal partial class RotateGeometryDockPaneViewModel
    {
        public void RefreshLayers()
        {
            Task.Run(async () =>
            {
                try
                {
                    var tempLayers = new List<FeatureLayer>();
                    await QueuedTask.Run(() =>
                    {
                        var map = MapView.Active?.Map;
                        if (map == null) return;
                        var layers = map.GetLayersAsFlattenedList().OfType<FeatureLayer>();
                        foreach (var fl in layers)
                        {
                            try
                            {
                                var def = fl.GetFeatureClass()?.GetDefinition();
                                var gtype = def?.GetShapeType();
                                if (gtype == GeometryType.Polygon || gtype == GeometryType.Polyline)
                                {
                                    tempLayers.Add(fl);
                                }
                            }
                            catch { }
                        }
                    });

                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        FeatureLayers.Clear();
                        foreach (var l in tempLayers) FeatureLayers.Add(l);
                        if (FeatureLayers.Count > 0 && SelectedLayer == null)
                            SelectedLayer = FeatureLayers[0];
                        StatusMessage = $"已加载 {FeatureLayers.Count} 个线/面图层";
                        UpdateSelectionInfo();
                        UpdateGeometryTypeFlags();
                        AppendLog($"已刷新图层列表：{FeatureLayers.Count} 个线/面图层");
                    });
                }
                catch (Exception ex)
                {
                    PresentationServices.UiThread.InvokeOrRun(() =>
                    {
                        StatusMessage = $"加载图层出错: {ex.Message}";
                        AppendLog($"错误: {ex.Message}");
                    });
                }
            });
        }

        private void Cancel()
        {
            CancelRequested = true;
            StatusMessage = "正在取消...";
        }

        private void AppendLog(string message)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{ts}] {message}\n";
        }

        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            // 仅在当前视图和当前图层相关时更新，简单起见总是刷新文本
            UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            try
            {
                int count = 0;
                if (SelectedLayer != null)
                {
                    count = SelectedLayer.SelectionCount;
                }
                SelectionInfo = count > 0 ? $"已选择 {count} 个要素，将处理选择集" : "未选择要素，将处理全部";
                if (count != _lastSelectionCount)
                {
                    AppendLog(count > 0 ? $"选择集更新：已选择 {count} 个要素，将处理选择集" : "选择集更新：未选择要素，将处理全部");
                    _lastSelectionCount = count;
                }
            }
            catch
            {
                SelectionInfo = "未选择要素，将处理全部";
            }
        }

        private void UpdateGeometryTypeFlags()
        {
            if (SelectedLayer == null)
            {
                IsLineLayer = false;
                IsPolygonLayer = false;
                return;
            }
            Task.Run(async () =>
            {
                GeometryType? gtype = null;
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        gtype = SelectedLayer.GetFeatureClass()?.GetDefinition()?.GetShapeType();
                    }
                    catch { gtype = null; }
                });
                bool isLine = gtype == GeometryType.Polyline;
                bool isPolygon = gtype == GeometryType.Polygon;
                PresentationServices.UiThread.Post(() =>
                {
                    bool beforeLine = IsLineLayer;
                    bool beforePolygon = IsPolygonLayer;
                    IsLineLayer = isLine;
                    IsPolygonLayer = isPolygon;
                    if (beforeLine != isLine || beforePolygon != isPolygon)
                    {
                        var typeName = isLine ? "线" : isPolygon ? "面" : "其他";
                        AppendLog($"当前图层类型：{typeName}；已根据类型显示相应锚点选项");
                    }
                });
            });
        }

        private void ShowHelp()
        {
            var help = "旋转图形[线/面] 使用说明\n\n" +
                       "功能：\n" +
                       "- 对选定图层的线或面几何做旋转；\n" +
                       "- 支持统一角度或按字段角度（度）；\n" +
                       "- 方向可选逆时针/顺时针（统一设置）；\n" +
                       "- 线锚点：起点/中点/终点；\n" +
                       "- 面锚点：质心/标签点/包络中心/左下角/左上角/右下角/右上角/起点。\n\n" +
                       "注意：\n" +
                       "- 建议在编辑会话中操作；\n" +
                       "- 角度单位为度；逆时针为正；顺时针为负。";
            PresentationServices.Dialogs.Show(help, "帮助");
        }
    }
}
