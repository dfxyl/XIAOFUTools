using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.IntersectSummary
{
    internal partial class IntersectSummaryDockPaneViewModel
    {

        /// <summary>
        /// 根据单位更新默认小数位数
        /// </summary>
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

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            LoadPolygonLayers();
        }

        /// <summary>
        /// 显示结果窗口
        /// </summary>
        private void ShowResultWindow()
        {
            if (ResultTable == null || ResultTable.Rows.Count == 0)
            {
                PresentationServices.Dialogs.Show("没有可显示的数据", "提示");
                return;
            }

            try
            {
                _resultWindowService.Show(ResultTable, DecimalPlaces, _regionFieldCount);
            }
            catch (Exception ex)
            {
                LogError($"显示结果窗口失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消操作
        /// </summary>
        private void Cancel()
        {
            CancelRequested = true;
            StatusMessage = "正在取消操作...";
            LogWarning("用户请求取消操作");
        }

        /// <summary>
        /// 显示帮助信息
        /// </summary>
        private void ShowHelp()
        {
            var helpContent = "交集汇总表工具使用说明\n\n" +
                "功能描述：\n" +
                "计算区域红线与类要素图层的交集面积，并按区域和类别进行汇总统计。\n" +
                "支持面积调平，确保各子面积之和等于图斑实际面积。\n\n" +
                "参数说明：\n" +
                "• 区域图层：选择作为基础范围的面图层\n" +
                "• 区域字段(可选)：选择用于分组的字段，不选则不分组\n" +
                "• 类要素图层：选择需要计算交集的面图层\n" +
                "• 类字段：选择用于分类汇总的字段（支持多选）\n" +
                "• 输出单位：选择面积输出单位\n" +
                "• 保留位数：设置面积值的小数位数\n\n" +
                "面积调平说明：\n" +
                "工具会自动按比例调整交集面积，使各子面积之和\n" +
                "精确等于对应区域图斑的实际面积，消除计算误差。\n" +
                "保留小数位时使用最大余额法，确保四舍五入后总和不变。\n\n" +
                "操作步骤：\n" +
                "1. 选择区域图层\n" +
                "2. 可选：勾选区域字段进行分组\n" +
                "3. 选择类要素图层\n" +
                "4. 勾选需要的类字段\n" +
                "5. 设置输出单位和小数位数\n" +
                "6. 点击\"开始\"按钮执行计算\n" +
                "7. 计算完成后可查看或导出结果\n\n" +
                "单位默认小数位数：\n" +
                "• 平方米：2位小数\n" +
                "• 公顷：4位小数\n" +
                "• 亩：4位小数";

            PresentationServices.Dialogs.Show(helpContent, "交集汇总表工具帮助");
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
            PresentationServices.UiThread.Invoke(() =>
            {
                StatusMessage = message;
            });
        }

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";

            PresentationServices.UiThread.Invoke(() =>
            {
                LogContent += logMessage + Environment.NewLine;
            });
        }

        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 警告: {message}";

            PresentationServices.UiThread.Invoke(() =>
            {
                LogContent += logMessage + Environment.NewLine;
            });
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 错误: {message}";

            PresentationServices.UiThread.Invoke(() =>
            {
                LogContent += logMessage + Environment.NewLine;
                StatusMessage = $"错误: {message}";
            });
        }
    }
}
