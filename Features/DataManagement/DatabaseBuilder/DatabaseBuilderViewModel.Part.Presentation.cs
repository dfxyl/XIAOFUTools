using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;
using ExcelDataReader;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.DatabaseBuilder
{
    public partial class DatabaseBuilderViewModel
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
            // Excel文件不是GIS数据，使用Windows标准文件对话框
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
        /// 浏览输出文件夹
        /// </summary>
        private void BrowseOutputFolder()
        {
            // 使用 ArcGIS Pro 自带的文件夹浏览对话框
            var selectedPath = PresentationServices.Files.SelectFolder("选择输出数据库位置");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                OutputFolderPath = selectedPath;
                LogInfo($"已选择输出位置: {OutputFolderPath}");
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "属性表建库工具使用说明\n\n" +
                               "功能描述：\n" +
                               "根据Excel属性结构表自动创建文件地理数据库、要素集及要素类。\n\n" +
                               "参数说明：\n" +
                               "1. 属性结构表 (Excel)：包含图层和字段定义的Excel文件\n" +
                               "2. 输出数据库位置：文件地理数据库的保存位置\n" +
                               "3. 数据库名称：要创建的数据库名称（不含.gdb后缀）\n" +
                               "4. 图层坐标系：设置新建要素集和要素类的坐标系（默认WGS84）\n\n" +
                               "Excel模板格式：\n" +
                               "- 必须包含名为'图层'的工作表\n" +
                               "- 可选包含名为'要素集'的工作表\n" +
                               "- 要素集表列：序号、要素集名称、要素集别名、备注\n" +
                               "- 图层表列：序号、图层别名、几何类型、属性表名、要素集、约束、备注\n" +
                               "- 每个图层对应一个同名工作表定义字段\n" +
                               "- 字段表列：序号、字段别名、字段代码、字段类型、字段长度、小数位数、值域、约束\n\n" +
                               "几何类型：\n" +
                               "- POINT：点\n" +
                               "- POLYLINE：线\n" +
                               "- POLYGON：面\n" +
                               "- MULTIPOINT：多点\n\n" +
                               "字段类型：\n" +
                               "- TEXT：文本\n" +
                               "- SHORT：短整型\n" +
                               "- LONG：长整型\n" +
                               "- FLOAT：单精度浮点\n" +
                               "- DOUBLE：双精度浮点\n" +
                               "- DATE：日期\n\n" +
                               "操作步骤：\n" +
                               "1. 点击'导出Excel模板'获取模板文件\n" +
                               "2. 按模板格式填写要素集、图层和字段定义\n" +
                               "3. 选择填写好的Excel文件\n" +
                               "4. 选择输出位置并输入数据库名称\n" +
                               "5. 选择图层坐标系\n" +
                               "6. 点击'开始建库'执行创建";

            PresentationServices.Dialogs.Show(helpContent, "属性表建库工具使用说明");
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
