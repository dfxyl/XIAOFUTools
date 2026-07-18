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
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using System.IO;
using System.Linq;

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;
using XIAOFUTools.Shared.Presentation;
using SharedFieldSelectionDialog = XIAOFUTools.Shared.Presentation.Dialogs.FieldSelectionDialog;

namespace XIAOFUTools.Features.Editing.OverlapCheck
{
    internal partial class OverlapCheckDockPaneViewModel
    {

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            AddLog("正在刷新图层列表...");
            LoadPolygonLayers();
            AddLog("图层列表刷新完成");
        }

        /// <summary>
        /// 浏览输出路径
        /// </summary>
        private void BrowseOutput()
        {
            try
            {
                var saveItemDialog = new SaveItemDialog
                {
                    Title = "选择输出位置",
                    OverwritePrompt = true,
                    DefaultExt = "shp",
                    Filter = ItemFilters.FeatureClasses_All
                };

                var initialLocation = GetProjectGDBPath();
                if (!string.IsNullOrEmpty(initialLocation))
                {
                    saveItemDialog.InitialLocation = initialLocation;
                }

                bool? dialogResult = saveItemDialog.ShowDialog();
                if (dialogResult == true)
                {
                    OutputPath = saveItemDialog.FilePath;
                }
            }
            catch (Exception ex)
            {
                AddLog($"选择输出位置出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新输出路径
        /// </summary>
        private void UpdateOutputPath()
        {
            string projectGDB = GetProjectGDBPath();
            string outputName;

            if (SelectedPolygonLayer != null)
            {
                outputName = $"{SelectedPolygonLayer.Name}_重叠区域";
            }
            else
            {
                outputName = "重叠区域";
            }

            if (!string.IsNullOrEmpty(projectGDB))
            {
                OutputPath = Path.Combine(projectGDB, outputName);
            }
            else
            {
                OutputPath = outputName;
            }
        }

        /// <summary>
        /// 添加日志
        /// </summary>
        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] {message}\r\n";
        }

        /// <summary>
        /// 取消处理
        /// </summary>
        private void CancelProcess()
        {
            _cancellationTokenSource?.Cancel();
            AddLog("正在取消操作...");
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
                    using (var table = SelectedPolygonLayer.GetTable())
                    {
                        if (table != null)
                        {
                            var definition = table.GetDefinition();
                            return definition.GetFields().ToList();
                        }
                        return null;
                    }
                });

                if (fields == null)
                {
                    AddLog("无法获取图层字段信息");
                    return;
                }

                IReadOnlyList<string> selectedFields = null;
                PresentationServices.UiThread.InvokeOrRun(() =>
                {
                    selectedFields = SharedFieldSelectionDialog.Select(fields, SelectedFields);
                });
                if (selectedFields is not null)
                {
                    SelectedFields = selectedFields.ToList();
                    AddLog($"已选择 {SelectedFields.Count} 个保留字段");
                }
            }
            catch (Exception ex)
            {
                AddLog($"选择字段时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            var helpText = @"图形重叠检查工具使用说明：

1. 选择面要素图层：选择需要检查重叠的面要素图层
2. 设置容差值：设置重叠检查的容差值（默认0.001米）
3. 选择输出路径：选择重叠区域输出的Shapefile路径
4. 选择保留字段：选择要保留到输出图层的原始字段
5. 点击开始按钮执行重叠检查

工具将检查选中图层中所有面要素之间的重叠情况，
并将重叠区域输出为新的面要素图层。
保留字段的值将以 '值1/值2/值3' 的格式合并。";

            PresentationServices.Dialogs.Show(helpText, "使用说明");
        }
    }
}
