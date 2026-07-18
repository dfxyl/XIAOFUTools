using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.AreaCalculator
{
    internal partial class AreaCalculatorDockPaneViewModel
    {

        /// <summary>
        /// 刷新图层列表（供DockPane调用）
        /// </summary>
        public void RefreshLayers()
        {
            LoadPolygonLayers();
        }

        private void TrySelectPreferredLayer()
        {
            if (PolygonLayers == null || PolygonLayers.Count == 0)
            {
                return;
            }

            var preferredLayer = FindPreferredLayer(PolygonLayers);
            if (preferredLayer != null)
            {
                SelectedPolygonLayer = preferredLayer;
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
            var helpContent = "计算面积工具使用说明\n\n" +
                "功能描述：\n" +
                "计算面要素的面积并写入指定字段。\n\n" +
                "参数说明：\n" +
                "• 面图层：选择要计算面积的面要素图层\n" +
                "• 字段：选择用于存储面积值的字段（支持数值字段和文本字段）\n" +
                "• 单位：选择面积计算单位（平方米、公顷、亩、平方公里）\n" +
                "• 保留小数位数：设置面积值的小数位数\n" +
                "• 面积类型：选择计算方式（平面或椭球）\n\n" +
                "操作步骤：\n" +
                "1. 选择要处理的面图层\n" +
                "2. 选择用于存储面积的字段（支持数值类型和文本类型）\n" +
                "3. 设置面积单位和小数位数\n" +
                "4. 选择面积计算类型\n" +
                "5. 点击\"开始\"按钮执行计算\n" +
                "6. 处理过程中可点击\"停止\"按钮取消操作\n\n" +
                "面积类型说明：\n" +
                "• 平面：基于投影坐标系统的平面面积计算，适用于小范围区域\n" +
                "• 椭球：基于地理坐标系统的椭球面积计算，适用于大范围区域的精确计算\n" +
                "  椭球面积计算符合测绘规范要求，提供高精度的面积计算结果\n\n" +
                "单位换算：\n" +
                "• 1公顷 = 10,000平方米\n" +
                "• 1亩 ≈ 666.67平方米\n" +
                "• 1平方公里 = 1,000,000平方米\n\n" +
                "字段类型说明：\n" +
                "• 数值字段：直接存储计算结果，根据小数位数四舍五入\n" +
                "• 整型字段：自动四舍五入为整数\n" +
                "• 文本字段：存储格式化的字符串，保留指定小数位数（包括尾随零）\n\n" +
                "注意事项：\n" +
                "• 支持数值字段和文本字段存储面积值\n" +
                "• 椭球面积计算需要图层具有地理坐标系统\n" +
                "• 大数据量处理时建议先备份数据\n" +
                "• 处理过程中会修改原始数据，请谨慎操作";

            PresentationServices.Dialogs.Show(helpContent, "计算面积工具帮助");
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}";

            if (PresentationServices.UiThread != null)
            {
                PresentationServices.UiThread.Invoke(() =>
                {
                    LogContent += logMessage + Environment.NewLine;
                });
            }
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 警告: {message}";

            if (PresentationServices.UiThread != null)
            {
                PresentationServices.UiThread.Invoke(() =>
                {
                    LogContent += logMessage + Environment.NewLine;
                });
            }
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 错误: {message}";

            if (PresentationServices.UiThread != null)
            {
                PresentationServices.UiThread.Invoke(() =>
                {
                    LogContent += logMessage + Environment.NewLine;
                    StatusMessage = $"错误: {message}";
                });
            }
        }
    }
}
