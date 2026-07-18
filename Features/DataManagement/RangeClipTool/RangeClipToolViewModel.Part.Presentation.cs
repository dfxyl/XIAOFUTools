using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XIAOFUTools.Shared;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.DataManagement.RangeClipTool
{
    internal partial class RangeClipToolViewModel
    {

        /// <summary>
        /// 刷新图层列表（供DockPane调用）
        /// </summary>
        public void RefreshLayers()
        {
            LoadLayers();
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        private void LogInfo(string message)
        {
            string logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent += logMessage + Environment.NewLine;
            });
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        private void LogError(string message)
        {
            string logMessage = $"[{DateTime.Now:HH:mm:ss}] 错误: {message}";
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent += logMessage + Environment.NewLine;
            });
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        private void LogWarning(string message)
        {
            string logMessage = $"[{DateTime.Now:HH:mm:ss}] 警告: {message}";
            PresentationServices.UiThread.InvokeOrRun(() =>
            {
                LogContent += logMessage + Environment.NewLine;
            });
        }

        /// <summary>
        /// 浏览文件夹
        /// </summary>
        private void BrowseFolder()
        {
            try
            {
                // 使用 ArcGIS Pro 自带的 OpenItemDialog 选择文件夹（需在UI线程上调用）
                var initialLocation = Project.Current?.HomeFolderPath ?? OutputFolder;
                if (!_outputFolderStore.DirectoryExists(initialLocation))
                {
                    initialLocation = GetProjectFolderPath();
                }

                var selectedPath = PresentationServices.Files.SelectFolder("选择输出文件夹", initialLocation);
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    OutputFolder = selectedPath;
                    LogInfo($"选择输出文件夹: {OutputFolder}");
                }
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show($"选择文件夹出错: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "根据范围批量裁剪要素图层工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于根据范围图层的字段值对要素图层进行批量裁剪。\n\n" +
                               "参数说明：\n" +
                               "1. 范围图层：选择用作裁剪范围的要素图层\n" +
                               "2. 范围字段：选择范围图层中用于分组的字段\n" +
                               "3. 需裁剪要素图层：选择需要被裁剪的要素图层（可多选）\n" +
                               "4. 输出文件夹：选择裁剪结果的保存位置\n" +
                               "5. 单独创建文件夹：是否为每个范围字段值创建单独的文件夹\n\n" +
                               "操作步骤：\n" +
                               "1. 选择范围图层\n" +
                               "2. 选择范围字段\n" +
                               "3. 勾选需要裁剪的要素图层\n" +
                               "4. 选择输出文件夹\n" +
                               "5. 选择是否创建子文件夹\n" +
                               "6. 点击开始按钮执行裁剪操作\n\n" +
                               "注意事项：\n" +
                               "- 工具会根据范围字段的不同值将范围图层分组\n" +
                               "- 每个分组的几何体将用于裁剪选中的要素图层\n" +
                               "- 输出文件保存在指定的输出文件夹中\n" +
                               "- 文件名保持原始图层名称\n" +
                               "- 处理过程中可以点击停止按钮取消操作";

            PresentationServices.Dialogs.Show(helpContent, "根据范围批量裁剪要素图层工具使用说明");
        }
    }
}
