using System;
using System.Linq;
using ArcGIS.Desktop.Catalog;
using ArcGIS.Desktop.Core;

namespace XIAOFUTools.Shared
{
    /// <summary>
    /// 使用 ArcGIS Pro 内置对话框的通用路径选择工具。
    /// </summary>
    public static class PathDialogUtils
    {
        /// <summary>
        /// 选择文件夹（使用 OpenItemDialog + ItemFilters.Folders）。
        /// 成功返回选中文件夹的本地路径，取消返回 null。
        /// </summary>
        public static string PickFolder(string title = "选择文件夹", string initialLocation = null)
        {
            try
            {
                var dlg = new OpenItemDialog
                {
                    Title = string.IsNullOrWhiteSpace(title) ? "选择文件夹" : title,
                    MultiSelect = false,
                    Filter = ItemFilters.Folders
                };
                if (!string.IsNullOrWhiteSpace(initialLocation))
                    dlg.InitialLocation = initialLocation;

                var ok = dlg.ShowDialog();
                if (ok == true && dlg.Items != null && dlg.Items.Any())
                {
                    // Item.Path 为本地路径或门户路径，本场景下筛的是本地文件夹
                    return dlg.Items.First().Path;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 保存要素类/要素图层（GDB 内要素类或 Shapefile），使用 SaveItemDialog + ItemFilters.FeatureClasses_All。
        /// 返回用户选择/输入的完整路径（例如 C:\a.gdb\fc 或 C:\folder\name.shp），取消返回 null。
        /// </summary>
        public static string PickSaveFeatureClassPath(string title = "选择输出要素类位置", string initialLocation = null)
        {
            try
            {
                var dlg = new SaveItemDialog
                {
                    Title = string.IsNullOrWhiteSpace(title) ? "选择输出要素类位置" : title,
                    OverwritePrompt = true,
                    Filter = ItemFilters.FeatureClasses_All
                };
                // 初始位置可为 GDB 或文件夹
                if (!string.IsNullOrWhiteSpace(initialLocation))
                    dlg.InitialLocation = initialLocation;

                var ok = dlg.ShowDialog();
                if (ok == true)
                {
                    return dlg.FilePath; // GDB 中不带 .shp，文件夹中可能带 .shp
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 返回当前项目默认地理数据库路径（若不可用返回用户文档目录）。
        /// </summary>
        public static string GetProjectDefaultGdb()
        {
            try
            {
                return Project.Current?.DefaultGeodatabasePath ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            catch
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }
    }
}
