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

        public void RefreshDatasets()
        {
            LoadInputDatasets();
        }

        private static PivotFieldOption CloneField(PivotFieldOption source)
        {
            return new PivotFieldOption
            {
                FieldName = source.FieldName,
                Alias = source.Alias,
                FieldType = source.FieldType
            };
        }

        private void ClearFieldCollections()
        {
            foreach (var field in RegionFields)
                field.PropertyChanged -= RegionFieldOnPropertyChanged;

            RegionFields.Clear();
            PivotFields.Clear();
            ValueFields.Clear();
            SelectedPivotField = null;
            SelectedValueField = null;
        }

        private void RegionFieldOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PivotFieldOption.IsSelected))
                NotifyPropertyChanged(() => CanProcess);
        }

        private void BrowseOutputGdb()
        {
            try
            {
                var selectedPath = PresentationServices.Files.SelectGeodatabase(
                    "选择输出地理数据库",
                    string.IsNullOrWhiteSpace(OutputGdbPath) ? Project.Current?.HomeFolderPath : OutputGdbPath);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    OutputGdbPath = selectedPath;
                    LogInfo($"输出数据库: {OutputGdbPath}");
                }
            }
            catch (Exception ex)
            {
                LogError($"选择输出数据库失败: {ex.Message}");
            }
        }

        private void Cancel()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
                StatusMessage = "正在取消，请稍候...";
            }
        }

        private void ShowHelp()
        {
            var help =
                "数据透视工具使用说明\n\n" +
                "功能：\n" +
                "将输入表按区域字段分组后，对透视字段展开列，并对数值字段进行汇总统计。\n\n" +
                "参数说明：\n" +
                "1. 输入表：选择地图中的要素图层或独立表。\n" +
                "2. 区域字段：分组字段，可多选。\n" +
                "3. 透视字段：用于展开列。空值会自动归并为“空值”。\n" +
                "4. 数值字段：用于汇总计算，非数值内容会按 0 处理。\n" +
                "5. 汇总方式：支持求和、计数、平均值、最大值、最小值、中位数、极差、标准差、方差。\n" +
                "6. 输出设置：选择输出 GDB 和输出表名。\n\n" +
                "操作步骤：\n" +
                "1) 选择输入表；\n" +
                "2) 勾选区域字段并设置透视字段/数值字段；\n" +
                "3) 选择汇总方式和输出位置；\n" +
                "4) 点击“开始”执行。";

            PresentationServices.Dialogs.Show(help, "数据透视帮助");
        }

        private void LogInfo(string message)
        {
            AppendLog(message);
        }

        private void LogWarning(string message)
        {
            AppendLog($"警告: {message}");
        }

        private void LogError(string message)
        {
            AppendLog($"错误: {message}");
            StatusMessage = message;
        }

        private void AppendLog(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent += line + Environment.NewLine;
            });
        }
    }
}
