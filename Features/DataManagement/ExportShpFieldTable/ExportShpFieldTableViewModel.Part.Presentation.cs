using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.DataManagement.ExportShpFieldTable
{
    public partial class ExportShpFieldTableViewModel
    {

        private void BrowseInputFolder()
        {
            var selectedPath = PresentationServices.Files.SelectFolder("选择输入SHP目录");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                InputFolderPath = selectedPath;
                LogInfo($"已选择输入目录: {InputFolderPath}");

                if (string.IsNullOrWhiteSpace(OutputExcelPath))
                {
                    OutputExcelPath = Path.Combine(InputFolderPath, "SHP字段表.xlsx");
                }
            }
        }

        private void BrowseOutputExcel()
        {
            var selectedPath = PresentationServices.Files.SaveFile(
                "Excel文件 (*.xlsx)|*.xlsx|Excel 97-2003文件 (*.xls)|*.xls",
                string.IsNullOrWhiteSpace(OutputExcelPath) ? "SHP字段表.xlsx" : Path.GetFileName(OutputExcelPath),
                _fileStore.DirectoryExists(InputFolderPath) ? InputFolderPath : null,
                "选择输出字段表",
                ".xlsx");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                OutputExcelPath = selectedPath;
                LogInfo($"已选择输出表: {OutputExcelPath}");
            }
        }

        private void ShowHelp()
        {
            var helpText = "【SHP输字段表】\n\n"
                         + "功能说明：\n"
                         + "将输入目录中的SHP字段结构导出为Excel表，可直接用于属性表建SHP模板编辑。\n\n"
                         + "参数说明：\n"
                         + "- 输入SHP目录：包含SHP文件的文件夹\n"
                         + "- 输出表：导出的Excel文件路径（.xlsx/.xls）\n\n"
                         + "输出结构：\n"
                         + "- 图层工作表：记录图层名、几何类型、属性表名\n"
                         + "- 字段工作表：每个SHP一个同名字段定义工作表\n\n"
                         + "注意事项：\n"
                         + "- 仅处理目录下一级SHP文件（不递归子目录）\n"
                         + "- 系统字段（FID/Shape等）不会导出\n"
                         + "- 工作表名称超过31字符会自动截断并去重";

            PresentationServices.Dialogs.Show(helpText, "帮助 - SHP输字段表", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        private void LogInfo(string message)
        {
            AppendLog(message);
        }

        private void LogWarning(string message)
        {
            AppendLog("警告: " + message);
        }

        private void LogError(string message)
        {
            AppendLog("错误: " + message);
        }

        private void AppendLog(string message)
        {
            var line = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
            PresentationServices.UiThread.InvokeOrRun(() => LogText += line);
        }
    }
}
