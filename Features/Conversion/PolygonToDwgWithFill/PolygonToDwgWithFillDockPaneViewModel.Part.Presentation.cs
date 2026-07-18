using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill
{
    internal partial class PolygonToDwgWithFillDockPaneViewModel
    {
        public void RefreshLayers()
        {
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                StatusMessage = "正在刷新图层列表...";
                LogInfo("开始刷新图层列表");
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    var layers = await QueuedTask.Run(() => MapView.Active?.Map?
                        .Layers
                        .OfType<FeatureLayer>()
                        .Where(layer => layer.ShapeType == esriGeometryType.esriGeometryPolygon)
                        .ToList() ?? new List<FeatureLayer>());
                    await PresentationServices.UiThread.InvokeAsync(() =>
                    {
                        PolygonLayers.Clear();
                        foreach (var layer in layers)
                        {
                            PolygonLayers.Add(layer);
                        }
                        StatusMessage = $"已加载 {PolygonLayers.Count} 个面图层";
                        LogInfo(StatusMessage);
                        if (SelectedPolygonLayer == null && PolygonLayers.Any())
                        {
                            SelectedPolygonLayer = PolygonLayers.First();
                        }
                    });
                }
                catch (Exception exception)
                {
                    await PresentationServices.UiThread.InvokeAsync(() =>
                    {
                        StatusMessage = $"刷新图层失败: {exception.Message}";
                        LogError(StatusMessage);
                    });
                }
            });
        }

        private void BrowseOutputPath()
        {
            var selectedPath = PresentationServices.Files.SaveFile(
                "AutoCAD DWG (*.dwg)|*.dwg",
                SelectedPolygonLayer != null ? SanitizeFileName(SelectedPolygonLayer.Name) + ".dwg" : "output.dwg",
                _outputPathResolver.GetDesktopDirectoryOrNull(),
                defaultExtension: ".dwg");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                OutputPath = selectedPath;
                StatusMessage = $"输出路径: {OutputPath}";
            }
        }

        private void ShowHelp()
        {
            LogInfo("使用说明：选择面图层，设置输出DWG路径，点击生成DWG。");
        }

        private void RequestCancel()
        {
            if (!IsProcessing)
            {
                return;
            }
            CancelRequested = true;
            StatusMessage = "已请求取消...";
            LogInfo(StatusMessage);
        }

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            PresentationServices.UiThread.InvokeOrRun(() =>
                LogContent += $"[{timestamp}] {message}" + Environment.NewLine);
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            PresentationServices.UiThread.InvokeOrRun(() =>
                LogContent += $"[{timestamp}] 错误: {message}" + Environment.NewLine);
        }
    }
}
