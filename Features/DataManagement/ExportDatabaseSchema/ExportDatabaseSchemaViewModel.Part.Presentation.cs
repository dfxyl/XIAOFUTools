using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;

namespace XIAOFUTools.Features.DataManagement.ExportDatabaseSchema
{
    public partial class ExportDatabaseSchemaViewModel
    {

        private void BrowseInputGdb()
        {
            var selectedPath = PresentationServices.Files.SelectGeodatabase("选择文件地理数据库");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                InputGdbPath = selectedPath;
                LogInfo($"已选择数据库: {InputGdbPath}");

                // 自动设置输出路径
                if (string.IsNullOrEmpty(OutputExcelPath))
                {
                    var gdbName = Path.GetFileNameWithoutExtension(InputGdbPath);
                    var parentDir = Path.GetDirectoryName(InputGdbPath);
                    OutputExcelPath = Path.Combine(parentDir, $"{gdbName}_属性结构表.xlsx");
                }
            }
        }

        private void BrowseOutputExcel()
        {
            string defaultFileName = null;
            string initialDirectory = null;
            if (!string.IsNullOrEmpty(InputGdbPath))
            {
                var gdbName = Path.GetFileNameWithoutExtension(InputGdbPath);
                defaultFileName = $"{gdbName}_属性结构表.xlsx";
                initialDirectory = Path.GetDirectoryName(InputGdbPath);
            }
            var selectedPath = PresentationServices.Files.SaveFile(
                "Excel文件 (*.xlsx)|*.xlsx|Excel 97-2003文件 (*.xls)|*.xls",
                defaultFileName,
                initialDirectory,
                "选择Excel文件保存位置",
                ".xlsx");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                OutputExcelPath = selectedPath;
                LogInfo($"已选择输出路径: {OutputExcelPath}");
            }
        }

        private void ShowHelp()
        {
            var helpText = "【输出数据库属性结构表】\n\n" +
                "功能说明：\n" +
                "将文件地理数据库(GDB)的属性结构导出为Excel文件，包含图层信息和字段定义。\n\n" +
                "参数说明：\n" +
                "- 输入数据库: 选择要导出结构的文件地理数据库(.gdb)\n" +
                "- 输出Excel: 选择导出的Excel文件路径(支持.xlsx和.xls格式)\n\n" +
                "输出内容：\n" +
                "- 图层汇总表: 包含所有要素类和表的基本信息\n" +
                "- 字段详情表: 每个要素类/表单独一个工作表，包含字段详细定义\n\n" +
                "使用步骤：\n" +
                "1. 点击\"浏览\"选择输入的GDB数据库\n" +
                "2. 点击\"浏览\"选择输出Excel文件路径\n" +
                "3. 点击\"开始导出\"执行导出操作\n\n" +
                "注意事项：\n" +
                "- 系统字段(OBJECTID、Shape等)不会被导出\n" +
                "- 工作表名称最长31个字符，超长会被截断\n" +
                "- 导出的Excel格式与建库模板兼容";

            PresentationServices.Dialogs.Show(helpText, "帮助 - 输出数据库属性结构表", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}\n";
            
            PresentationServices.UiThread.InvokeOrRun(() => LogText += logMessage);
        }

        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 错误: {message}\n";
            
            PresentationServices.UiThread.InvokeOrRun(() => LogText += logMessage);
        }

        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logMessage = $"[{timestamp}] 警告: {message}\n";
            
            PresentationServices.UiThread.InvokeOrRun(() => LogText += logMessage);
        }
    }
}
