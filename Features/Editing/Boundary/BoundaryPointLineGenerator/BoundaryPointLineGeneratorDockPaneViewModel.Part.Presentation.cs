using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.Boundary.BoundaryPointLineGenerator
{
    internal partial class BoundaryPointLineGeneratorDockPaneViewModel
    {

        private void UpdateOutputName()
        {
            if (SelectedPolygonLayer == null)
            {
                OutputFeatureClassName = "输出";
                return;
            }

            var baseName = SelectedPolygonLayer.Name;
            if (SelectedOutputType.Contains("JZD") && SelectedOutputType.Contains("JZX"))
                OutputFeatureClassName = $"{baseName}_JZD / {baseName}_JZX";
            else
                OutputFeatureClassName = SelectedOutputType.Contains("JZD") ? $"{baseName}_JZD" : $"{baseName}_JZX";
        }

        private void UpdateOutputPaths()
        {
            try
            {
                var baseName = SelectedPolygonLayer?.Name;
                var projGdb = PathDialogUtils.GetProjectDefaultGdb();
                if (string.IsNullOrWhiteSpace(OutputPathJZD) && (SelectedOutputType?.Contains("JZD") ?? false) && !string.IsNullOrWhiteSpace(baseName))
                {
                    OutputPathJZD = string.IsNullOrWhiteSpace(projGdb) ? $"{baseName}_JZD" : System.IO.Path.Combine(projGdb, $"{baseName}_JZD");
                }
                if (string.IsNullOrWhiteSpace(OutputPathJZX) && (SelectedOutputType?.Contains("JZX") ?? false) && !string.IsNullOrWhiteSpace(baseName))
                {
                    OutputPathJZX = string.IsNullOrWhiteSpace(projGdb) ? $"{baseName}_JZX" : System.IO.Path.Combine(projGdb, $"{baseName}_JZX");
                }
            }
            catch { }
        }

        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            // 使用通用接口获取选择集信息
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

        private void UpdateProgress(int processed, int total)
        {
            var p = (int)(processed * 100.0 / Math.Max(1, total));
            PresentationServices.UiThread.InvokeOrRun(() => { Progress = p; IsProgressIndeterminate = false; });
        }
        private void ClearLog() { _logBuilder.Clear(); LogContent = string.Empty; }
        private void LogInfo(string msg) { Append($"{msg}"); }
        private void LogWarning(string msg) { Append($"警告: {msg}"); }
        private void LogError(string msg) { Append($"错误: {msg}"); }

        private void ShowHelp()
        {
            var help = "界址点线生成\n\n" +
                "- 选择面图层，选择生成类型（界址点/界址线/同时生成）。\n" +
                "- 可自定义 JZD/JZX 输出位置：支持工程GDB或文件夹（自动识别并创建 GDB 要素类或 Shapefile）。\n" +
                "- 若目标已存在，会弹出覆盖确认提示。\n" +
                "- 可选从源图层选择一个字段写入 ZDZHDM（宗地/宗海代码）。\n" +
                "- 其他标准字段预留，用户可后续按需维护。";
            PresentationServices.Dialogs.Show(help, "帮助");
        }
    }
}
