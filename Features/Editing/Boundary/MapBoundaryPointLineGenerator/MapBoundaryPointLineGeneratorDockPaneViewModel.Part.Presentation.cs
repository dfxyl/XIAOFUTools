using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Editing.Boundary.MapBoundaryPointLineGenerator
{
    internal partial class MapBoundaryPointLineGeneratorDockPaneViewModel
    {

        private void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            UpdateSelectionInfo();
        }

        private void UpdateSelectionInfo()
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                var info = await SelectionUtils.GetSelectionInfoAsync(SelectedPolygonLayer);
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    UseSelection = SelectionUtils.RecommendUseSelection(UseSelection, info.HasSelection);
                    HasSelection = info.HasSelection;
                    SelectedCount = info.Count;
                });
            });
        }

        private void RefreshAll()
        {
            LoadPolygonLayers();
            LoadLayouts();
        }

        /// <summary>
        /// 刷新图层列表（供View的Loaded调用）
        /// </summary>
        public void RefreshLayers()
        {
            LoadPolygonLayers();
            LoadLayouts();
        }

        private void UpdateProgress(int current, int total)
        {
            if (total <= 0) return;
            int pct = (int)(current * 100.0 / total);
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                Progress = pct;
                IsProgressIndeterminate = false;
            });
        }
        private void LogInfo(string msg)
        {
            _logBuilder.AppendLine($"[信息] {msg}");
            PresentationServices.UiThread.InvokeOrRun(() => LogContent = _logBuilder.ToString());
        }

        private void LogError(string msg)
        {
            _logBuilder.AppendLine($"[错误] {msg}");
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent = _logBuilder.ToString();
                StatusMessage = msg;
            });
        }

        private void ClearLog()
        {
            _logBuilder.Clear();
            LogContent = string.Empty;
        }

        private void ShowHelp()
        {
            PresentationServices.Dialogs.Show(
                "地图生成界址点线\n\n" +
                "功能说明：\n" +
                "在布局的图形图层上生成界址点、界址线、点号、边长标注。\n\n" +
                "使用步骤：\n" +
                "1. 选择面要素图层\n" +
                "2. 选择唯一字段（可选，用于标识）\n" +
                "3. 选择目标布局和地图框\n" +
                "4. 配置界址点、界址线、点号、边长设置\n" +
                "5. 点击\"生成模板\"创建符号模板（可选，用于自定义样式）\n" +
                "6. 点击\"开始\"生成\n\n" +
                "模板说明：\n" +
                "- XF_JZD: 界址点符号模板\n" +
                "- XF_JZX: 界址线符号模板\n" +
                "- XF_DH: 点号文字模板\n" +
                "- XF_BC: 边长文字模板",
                "帮助");
        }
    }
}
