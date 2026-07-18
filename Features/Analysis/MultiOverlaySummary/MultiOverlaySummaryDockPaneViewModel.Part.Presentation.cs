using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Data.Exceptions;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;

namespace XIAOFUTools.Features.Analysis.MultiOverlaySummary
{
    internal partial class MultiOverlaySummaryDockPaneViewModel
    {

        private void UpdateDefaultDecimalPlaces()
        {
            DecimalPlaces = SelectedAreaUnit switch
            {
                "平方米" => 2,
                "公顷" => 4,
                "亩" => 4,
                _ => 2
            };
        }

        public void RefreshLayers() => LoadPolygonLayers();

        private void UpdateOverlayLayerItems()
        {
            PresentationServices.UiThread.Invoke(() =>
            {
                var previousSelections = OverlayLayerItems?.Where(x => x.IsSelected).Select(x => x.LayerName).ToList() ?? new List<string>();
                OverlayLayerItems?.Clear();

                foreach (var layer in PolygonLayers)
                {
                    if (layer != SelectedMainLayer)
                    {
                        var item = new OverlayLayerItem { Layer = layer, IsSelected = previousSelections.Contains(layer.Name) };
                        item.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(OverlayLayerItem.IsSelected))
                                NotifyPropertyChanged(() => CanProcess);
                        };
                        OverlayLayerItems?.Add(item);
                    }
                }
            });
        }

        private void ShowResultWindow()
        {
            if (ResultTable == null || ResultTable.Rows.Count == 0)
            {
                PresentationServices.Dialogs.Show("没有可显示的数据", "提示");
                return;
            }

            try
            {
                _dialogService.ShowResult(ResultTable, DecimalPlaces, _intersectGeometries, _spatialReference, SelectedAreaUnit);
            }
            catch (Exception ex)
            {
                LogError($"显示结果窗口失败: {ex.Message}");
            }
        }

        private void Cancel()
        {
            CancelRequested = true;
            StatusMessage = "正在取消操作...";
            LogWarning("用户请求取消操作");
        }

        private void ShowHelp()
        {
            var helpContent = "多图层压盖汇总工具使用说明\n\n" +
                "功能描述：\n" +
                "计算主图层与多个压盖图层的交集面积，\n" +
                "输出结果以图层名称作为列名，显示每个项目压占各图层的面积。\n\n" +
                "参数说明：\n" +
                "• 主图层：选择作为基础范围的面图层\n" +
                "• 唯一字段(可选)：选择用于分组的字段，不选则汇总全部\n" +
                "• 压盖图层：勾选需要计算压盖面积的图层（支持多选）\n\n" +
                "输出功能：\n" +
                "• 导出Excel/CSV：导出汇总表格\n" +
                "• 导出SHP：导出压盖部分的交集图形（按图层分别导出）\n\n" +
                "操作步骤：\n" +
                "1. 选择主图层\n" +
                "2. 可选：选择唯一字段进行分组\n" +
                "3. 勾选压盖图层\n" +
                "4. 点击\"开始\"执行计算\n" +
                "5. 查看结果或导出";

            PresentationServices.Dialogs.Show(helpContent, "多图层压盖汇总工具帮助");
        }

        private void UpdateProgress(bool isIndeterminate, int progress)
        {
            PresentationServices.UiThread.Invoke(() =>
            {
                IsProgressIndeterminate = isIndeterminate;
                Progress = progress;
            });
        }

        private void UpdateStatus(string message)
        {
            PresentationServices.UiThread.Invoke(() => StatusMessage = message);
        }

        private void LogInfo(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            PresentationServices.UiThread.Invoke(() => LogContent += logMessage + Environment.NewLine);
        }

        private void LogWarning(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] 警告: {message}";
            PresentationServices.UiThread.Invoke(() => LogContent += logMessage + Environment.NewLine);
        }

        private void LogError(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] 错误: {message}";
            PresentationServices.UiThread.Invoke(() =>
            {
                LogContent += logMessage + Environment.NewLine;
                StatusMessage = $"错误: {message}";
            });
        }
    }
}
