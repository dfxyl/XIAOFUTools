using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.DDL;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;

namespace XIAOFUTools.Features.DataManagement.MirrorDatabase
{
    public partial class MirrorDatabaseViewModel
    {

        private void BrowseSourceDatabase()
        {
            var selectedPath = PresentationServices.Files.SelectGeodatabase("选择源文件地理数据库");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                SourceDatabasePath = selectedPath;
                LogInfo("已选择源数据库: " + SourceDatabasePath);
            }
        }

        private void BrowseOutputFolder()
        {
            var selectedPath = PresentationServices.Files.SelectFolder("选择输出位置");
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                OutputFolderPath = selectedPath;
                LogInfo("已选择输出位置: " + OutputFolderPath);
            }
        }

        private void ShowHelp()
        {
            string helpMessage = "【镜像数据库工具使用说明】\n\n" +
                "功能说明：\n" +
                "将源文件地理数据库的结构（要素集、要素类、表）镜像到新的数据库中，只复制结构不复制数据。\n\n" +
                "使用步骤：\n" +
                "1. 选择源数据库：点击浏览选择要镜像的.gdb文件\n" +
                "2. 选择输出路径：点击浏览选择新数据库的保存位置\n" +
                "3. 输入数据库名称：设置新数据库的名称（不含.gdb后缀）\n" +
                "4. 点击开始镜像执行操作\n\n" +
                "注意事项：\n" +
                "- 只镜像数据库结构，不复制数据\n" +
                "- 会保留字段名称、类型、别名等属性\n" +
                "- 会保留要素集结构\n" +
                "- 如果目标数据库已存在，会在其中创建不存在的结构";

            PresentationServices.Dialogs.Show(helpMessage, "镜像数据库 - 帮助", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        private void LogInfo(string message)
        {
            AppendLog("[信息] " + message);
        }

        private void LogWarning(string message)
        {
            AppendLog("[警告] " + message);
        }

        private void LogError(string message)
        {
            AppendLog("[错误] " + message);
        }

        private void AppendLog(string message)
        {
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogText += DateTime.Now.ToString("HH:mm:ss") + " " + message + "\r\n";
            });
        }
    }
}
