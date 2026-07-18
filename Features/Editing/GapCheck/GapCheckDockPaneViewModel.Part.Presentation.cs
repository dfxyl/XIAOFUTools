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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.Editing.GapCheck
{
    internal partial class GapCheckDockPaneViewModel
    {

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            try
            {
                AddLog("正在刷新图层列表...");
                LoadPolygonLayers();
                AddLog("图层列表刷新完成");
            }
            catch (Exception ex)
            {
                AddLog($"刷新图层列表失败: {ex.Message}");
            }
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
                outputName = $"{SelectedPolygonLayer.Name}_缝隙";
            }
            else
            {
                outputName = "缝隙";
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
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;
                    
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                
                // 确保LogContent不为null
                if (LogContent == null)
                    LogContent = "";
                    
                LogContent += $"[{timestamp}] {message}\r\n";
            }
            catch (Exception ex)
            {
                // 如果日志记录失败，至少不要让整个应用崩溃
                System.Diagnostics.Debug.WriteLine($"AddLog failed: {ex.Message}");
            }
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
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            try
            {
                AddLog("显示帮助信息");
                
                string helpMessage = @"缝隙检查工具使用说明：

1. 选择面要素图层：选择需要检查缝隙的面要素图层
2. 设置检查容差值：设置工具执行的距离容差（米），用于控制检测精度
3. 选择输出路径：指定缝隙要素的输出位置
4. 点击开始按钮执行检查

工具原理：
- 融合所有面要素，消除重叠和相邻边界，保留洞
- 使用要素转面工具提取所有面要素（包括洞），应用容差参数
- 从结果中擦除原始融合要素，只保留洞
- 计算洞的面积并输出缝隙要素

输出结果包含以下字段：
- GAP_ID：缝隙编号
- GAP_AREA：缝隙面积（平方米）
- TOLERANCE：使用的容差值（米）

注意：容差值是工具执行精度，较小的值检测更精细的缝隙";

                PresentationServices.Dialogs.Show(helpMessage, "缝隙检查工具帮助", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AddLog($"显示帮助失败: {ex.Message}");
            }
        }
    }
}
