using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using Microsoft.Win32;
using XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable;
using XIAOFUTools.Shared.Presentation;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport
{
    public partial class MapSeriesExportViewModel
    {

        private void RefreshLayouts() => LoadLayouts();

        private void RefreshMapSeries()
        {
            LoadMapSeriesPages();
        }

        private void SelectAll()
        {
            foreach (var page in GetVisiblePages())
            {
                page.IsSelected = true;
            }
        }

        private void InvertSelection()
        {
            foreach (var page in GetVisiblePages())
            {
                page.IsSelected = !page.IsSelected;
            }
        }

        private void BrowseFolder()
        {
            var selectedFolder = PresentationServices.Files.SelectFolder("选择输出文件夹", OutputFolder);
            if (!string.IsNullOrWhiteSpace(selectedFolder))
            {
                OutputFolder = selectedFolder;
            }
        }

        private void ShowHelp()
        {
            string helpContent = 
                "【地图系列批量导出工具】\n\n" +
                "━━━━━━ 基本功能 ━━━━━━\n" +
                "基于ArcGIS Pro地图系列批量导出布局页面，支持自动生成坐标表。\n\n" +
                "━━━━━━ 主界面说明 ━━━━━━\n" +
                "• 布局选择：选择包含地图系列的布局\n" +
                "• 页面列表：显示所有页面，勾选要导出的页面\n" +
                "  - 双击行可切换到该页面预览\n" +
                "  - 全选/反选按钮快速选择\n" +
                "  - 搜索支持按页码或名称筛选\n" +
                "• 输出文件夹：导出文件保存位置\n" +
                "• 导出方式：\n" +
                "  - 全部导出：导出所有页面\n" +
                "  - 选中导出：导出勾选的页面\n" +
                "  - 指定页面：输入页码如 1,3,5-8\n" +
                "• 格式：PDF、JPG、PNG、TIF\n" +
                "• 分辨率：输出DPI，默认300\n\n" +
                "━━━━━━ 坐标表设置(⚙) ━━━━━━\n" +
                "• 启用坐标表：勾选后导出时自动生成坐标表\n" +
                "• 定位设置：\n" +
                "  - 地图框定位：相对于地图框边角定位\n" +
                "  - 锚点定位：相对于指定锚点元素定位\n" +
                "• 表格尺寸：点号宽、坐标宽、边长宽、行高\n" +
                "• 每列行数：超过此行数自动分列\n" +
                "• 压缩总行数：点数过多时自动省略中间行（如1-5...25-30）\n" +
                "• 面积显示：不显示/仅面积/面积+亩数\n" +
                "• 自定义文本：支持字段占位符如 [面积]\n" +
                "• 生成模板：创建XF_TX和XF_WB模板元素\n" +
                "  用于自定义表格样式（边框颜色、文字样式等）\n\n" +
                "━━━━━━ 界址点标注 ━━━━━━\n" +
                "• 生成界址点：在地图上创建点标注\n" +
                "• 点大小：默认8点，可生成XF_JZD模板设置样式\n" +
                "• 生成点号：在界址点外侧生成编号文本\n" +
                "  - 前缀、距离、大小可设置\n" +
                "  - 可生成XF_DH模板设置字体颜色\n" +
                "• 压盖处理：\n" +
                "  - 压盖隐藏：重叠时隐藏后面的\n" +
                "  - 压盖避让：自动调整位置避开重叠\n\n" +
                "━━━━━━ 交集表格 ━━━━━━\n" +
                "• 显示交集表格：按当前驱动红线与指定面图层相交计算\n" +
                "• 分类字段：作为交集结果的表格行类别\n" +
                "• 未覆盖部分自动归为“其他”，面积按当前红线总面积调平\n" +
                "• 交集表使用地图框/锚点定位方式，可设置角点、偏移、列宽和行高\n\n" +
                "━━━━━━ 注意事项 ━━━━━━\n" +
                "• 布局必须已启用空间地图系列\n" +
                "• 坐标表定位需要正确设置地图框或锚点\n" +
                "• 模板元素可自定义后重复使用：\n" +
                "  - XF_TX/XF_WB：表格样式\n" +
                "  - XF_JZD：界址点样式\n" +
                "  - XF_DH：点号文本样式\n" +
                "• 界址点标注创建在XF_界址点标注图形图层";
            PresentationServices.Dialogs.Show(helpContent, "地图系列批量导出工具 - 帮助");
        }

        /// <summary>
        /// 打开设置窗口
        /// </summary>
        private void OpenSettings()
        {
            _settingsWindowService.Show(
                _coordinateTableSettings,
                settings => CoordinateTableSettings = settings);
        }
        
        /// <summary>
        /// 判断点是否在多边形内部（射线法）
        /// </summary>
        private bool IsPointInPolygon(double x, double y, List<MapPoint> polygon)
        {
            int count = polygon.Count;
            bool inside = false;
            
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                double xi = polygon[i].X, yi = polygon[i].Y;
                double xj = polygon[j].X, yj = polygon[j].Y;
                
                if (((yi > y) != (yj > y)) &&
                    (x < (xj - xi) * (y - yi) / (yj - yi) + xi))
                {
                    inside = !inside;
                }
            }
            
            return inside;
        }
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
