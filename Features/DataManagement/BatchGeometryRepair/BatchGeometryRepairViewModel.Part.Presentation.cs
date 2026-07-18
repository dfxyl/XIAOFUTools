using ArcGIS.Core.CIM;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using ArcGIS.Desktop.Mapping;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;

namespace XIAOFUTools.Features.DataManagement.BatchGeometryRepair
{
    internal partial class BatchGeometryRepairViewModel
    {

        /// <summary>
        /// 刷新图层列表
        /// </summary>
        public void RefreshLayers()
        {
            LoadLayers();
        }

        /// <summary>
        /// 全选
        /// </summary>
        private void SelectAll()
        {
            foreach (var layer in LayerList)
            {
                layer.IsSelected = true;
            }
            NotifyPropertyChanged(() => CanProcess);
            StatusMessage = $"已选择 {LayerList.Count} 个图层";
        }

        /// <summary>
        /// 反选
        /// </summary>
        private void SelectNone()
        {
            foreach (var layer in LayerList)
            {
                layer.IsSelected = false;
            }
            NotifyPropertyChanged(() => CanProcess);
            StatusMessage = "已取消选择所有图层";
        }

        /// <summary>
        /// 取消操作
        /// </summary>
        private void Cancel()
        {
            CancelRequested = true;
            StatusMessage = "正在取消...";
        }

        /// <summary>
        /// 记录信息消息
        /// </summary>
        private void LogInfo(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] {message}\n";
        }

        /// <summary>
        /// 记录警告消息
        /// </summary>
        private void LogWarning(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] 警告: {message}\n";
        }

        /// <summary>
        /// 记录错误消息
        /// </summary>
        private void LogError(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] 错误: {message}\n";
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "批量修复几何工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于批量修复要素图层中的几何错误。\n\n" +
                               "参数说明：\n" +
                               "1. 图层列表：显示当前地图中的所有要素图层\n" +
                               "2. 选择：勾选需要修复几何的图层\n" +
                               "3. 图层名称：图层的名称\n" +
                               "4. 类型：图层的几何类型（点、线、面等）\n" +
                               "5. 坐标系：图层的坐标系\n\n" +
                               "操作步骤：\n" +
                               "1. 选择需要修复几何的图层（可使用全选/反选）\n" +
                               "2. 点击\"开始\"按钮执行批量修复几何\n" +
                               "3. 查看日志窗口了解处理进度和结果\n\n" +
                               "注意事项：\n" +
                               "- 修复几何会删除空几何和修复无效几何\n" +
                               "- 建议在修复前备份重要数据\n" +
                               "- 操作过程和结果将显示在日志窗口中\n" +
                               "- 处理过程中可以点击\"停止\"按钮取消操作";

            PresentationServices.Dialogs.Show(helpContent, "批量修复几何工具使用说明");
        }

        /// <summary>
        /// 处理图层列表集合变化事件
        /// </summary>
        private void LayerList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 为新添加的项目订阅属性变化事件
            if (e.NewItems != null)
            {
                foreach (LayerGeometryInfo item in e.NewItems)
                {
                    item.PropertyChanged += LayerInfo_PropertyChanged;
                }
            }

            // 为移除的项目取消订阅属性变化事件
            if (e.OldItems != null)
            {
                foreach (LayerGeometryInfo item in e.OldItems)
                {
                    item.PropertyChanged -= LayerInfo_PropertyChanged;
                }
            }
        }
    }
}
