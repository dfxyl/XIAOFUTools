using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ExcelDataReader;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.ShapefileBuilder
{
    public partial class ShapefileBuilderViewModel
    {

        /// <summary>
        /// 选择坐标系
        /// </summary>
        private void SelectCoordinateSystem()
        {
            try
            {
                var selectedSpatialRef = CoordinateSystemSelector.ShowCoordinateSystemDialog();
                if (selectedSpatialRef != null)
                {
                    SelectedSpatialReference = selectedSpatialRef;
                    LogInfo($"已选择图层坐标系: {SelectedCoordinateSystemName}");
                }
            }
            catch (Exception ex)
            {
                LogError($"选择坐标系失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 浏览输入Excel文件
        /// </summary>
        private void BrowseInputExcel()
        {
            var selectedPath = PresentationServices.Files.OpenFile(
                "Excel文件 (*.xls;*.xlsx)|*.xls;*.xlsx|所有文件 (*.*)|*.*",
                title: "选择属性结构表Excel文件");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                InputExcelPath = selectedPath;
                LogInfo($"已选择输入文件: {InputExcelPath}");
            }
        }

        /// <summary>
        /// 浏览输出目录
        /// </summary>
        private void BrowseOutputFolder()
        {
            var selectedPath = PresentationServices.Files.SelectFolder("选择输出SHP目录");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                OutputFolderPath = selectedPath;
                LogInfo($"已选择输出目录: {OutputFolderPath}");
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "属性表建SHP工具使用说明\n\n"
                               + "功能描述：\n"
                               + "根据Excel属性结构表自动批量创建Shapefile图层。\n\n"
                               + "参数说明：\n"
                               + "1. 属性结构表 (Excel)：包含图层和字段定义的Excel文件\n"
                               + "2. 输出SHP目录：Shapefile输出文件夹\n"
                               + "3. 图层坐标系：设置新建SHP图层坐标系（默认WGS84）\n\n"
                               + "Excel模板格式：\n"
                               + "- 必须包含名为'图层'的工作表\n"
                               + "- 可选包含名为'要素集'的工作表（SHP模式会忽略）\n"
                               + "- 图层表列：序号、图层别名、几何类型、属性表名、要素集、约束、备注\n"
                               + "- 每个图层对应一个同名工作表定义字段\n"
                               + "- 字段表列：序号、字段别名、字段代码、字段类型、字段长度、小数位数、值域、约束\n\n"
                               + "几何类型：\n"
                               + "- POINT：点\n"
                               + "- POLYLINE：线\n"
                               + "- POLYGON：面\n"
                               + "- MULTIPOINT：多点\n\n"
                               + "字段类型（SHP支持）：\n"
                               + "- TEXT：文本\n"
                               + "- SHORT：短整型\n"
                               + "- LONG：长整型\n"
                               + "- FLOAT：单精度浮点\n"
                               + "- DOUBLE：双精度浮点\n"
                               + "- DATE：日期\n\n"
                               + "注意事项：\n"
                               + "- 模板文件名为 建SHP模板.xls\n"
                               + "- SHP字段名最大10字符，超长会自动截断并去重\n"
                               + "- SHP不支持BLOB、GUID等字段类型，将自动跳过\n"
                               + "- SHP不支持要素集结构\n\n"
                               + "操作步骤：\n"
                               + "1. 点击'导出模板'获取模板文件\n"
                               + "2. 按模板格式填写图层和字段定义\n"
                               + "3. 选择填写好的Excel文件\n"
                               + "4. 选择输出目录并设置坐标系\n"
                               + "5. 点击'开始建SHP'执行创建";

            PresentationServices.Dialogs.Show(helpContent, "属性表建SHP工具使用说明");
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogText += $"[{timestamp}] {message}\n";
            });
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogText += $"[{timestamp}] 警告: {message}\n";
            });
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogText += $"[{timestamp}] 错误: {message}\n";
            });
        }
    }
}
