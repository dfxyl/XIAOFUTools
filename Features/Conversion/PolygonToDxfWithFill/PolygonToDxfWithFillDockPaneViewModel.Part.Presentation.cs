using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using ArcGIS.Core.Data;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.Conversion.PolygonToDxfWithFill.Core;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Conversion.PolygonToDxfWithFill
{
    internal partial class PolygonToDxfWithFillDockPaneViewModel
    {
        public void RefreshLayers()
        {
            StatusMessage = "正在刷新图层列表...";
            LogInfo("开始刷新图层列表");

            Task.Run(async () =>
            {
                try
                {
                    var temp = new List<FeatureLayer>();

                    await QueuedTask.Run(() =>
                    {
                        var map = MapView.Active?.Map;
                        if (map == null)
                            return;

                        foreach (var fl in map.Layers.OfType<FeatureLayer>())
                        {
                            if (fl.ShapeType == esriGeometryType.esriGeometryPolygon)
                                temp.Add(fl);
                        }
                    });

                    await PresentationServices.UiThread.InvokeAsync(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var fl in temp)
                            PolygonLayers.Add(fl);

                        StatusMessage = $"已加载 {PolygonLayers.Count} 个面图层";
                        LogInfo(StatusMessage);

                        if (SelectedPolygonLayer == null && PolygonLayers.Any())
                            SelectedPolygonLayer = PolygonLayers.First();
                    });
                }
                catch (Exception ex)
                {
                    await PresentationServices.UiThread.InvokeAsync(() =>
                    {
                        StatusMessage = $"刷新图层失败: {ex.Message}";
                        LogError(StatusMessage);
                    });
                }
            });
        }

        private void BrowseOutputPath()
        {
            var desktop = _outputPathResolver.GetDesktopDirectoryOrNull();
            var selectedPath = PresentationServices.Files.SaveFile(
                "AutoCAD DXF (*.dxf)|*.dxf",
                SelectedPolygonLayer != null ? SanitizeFileName(SelectedPolygonLayer.Name) + ".dxf" : "output.dxf",
                desktop,
                defaultExtension: ".dxf");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                OutputPath = selectedPath;
                StatusMessage = $"输出路径: {OutputPath}";
            }
        }

        private void ShowHelp()
        {
            LogInfo("使用说明：选择面图层，设置输出DXF路径，点击生成DXF。");
        }

        private void RequestCancel()
        {
            if (!IsProcessing) return;
            CancelRequested = true;
            StatusMessage = "已请求取消...";
            LogInfo(StatusMessage);
        }

        private void LogInfo(string message)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            PresentationServices.UiThread.InvokeOrRun(() =>
                LogContent += $"[{ts}] {message}" + Environment.NewLine);
        }

        private void LogError(string message)
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            PresentationServices.UiThread.InvokeOrRun(() =>
                LogContent += $"[{ts}] 错误: {message}" + Environment.NewLine);
        }
    }
}
