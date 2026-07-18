using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.Windows.Input;
using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Dialogs;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Cartography.ExportLayout
{
    public partial class ExportLayoutViewModel
    {

        /// <summary>
        /// 刷新布局列表
        /// </summary>
        public void RefreshLayouts()
        {
            Layouts.Clear();
            LoadLayouts();
        }

        /// <summary>
        /// 浏览文件夹
        /// </summary>
        private void BrowseFolder()
        {
            var selectedFolder = PresentationServices.Files.SelectFolder("选择输出文件夹", OutputFolder);
            if (!string.IsNullOrWhiteSpace(selectedFolder))
            {
                OutputFolder = selectedFolder;
            }
        }

        /// <summary>
        /// 全选
        /// </summary>
        private void SelectAll()
        {
            foreach (var layout in Layouts)
            {
                layout.IsSelected = true;
            }
        }

        /// <summary>
        /// 反选
        /// </summary>
        private void InvertSelection()
        {
            foreach (var layout in Layouts)
            {
                layout.IsSelected = !layout.IsSelected;
            }
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "导出布局工具帮助\n\n" +
                "功能描述：\n" +
                "批量导出项目中的布局为指定格式的文件。\n\n" +
                "参数说明：\n" +
                "- 输出文件夹：导出文件的保存位置\n" +
                "- 分辨率：导出图像的分辨率，单位为DPI，默认300\n" +
                "- 导出格式：支持PDF、TIF、JPG、PNG、GeoTIFF等格式\n" +
                "- 布局列表：显示项目中所有可用的布局，可选择需要导出的布局\n\n" +
                "操作步骤：\n" +
                "1. 选择输出文件夹\n" +
                "2. 设置分辨率（建议300DPI）\n" +
                "3. 选择导出格式\n" +
                "4. 在布局列表中选择要导出的布局\n" +
                "5. 点击\"开始\"按钮执行导出\n\n" +
                "注意事项：\n" +
                "- 确保输出文件夹有写入权限\n" +
                "- 导出过程中请勿关闭ArcGIS Pro\n" +
                "- 大量布局导出可能需要较长时间\n" +
                "- GeoTIFF格式会包含完整的地理参考信息和坐标系统";

            PresentationServices.Dialogs.Show(helpContent, "导出布局工具帮助");
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
