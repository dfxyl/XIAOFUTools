using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Geometry;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using XIAOFUTools.Shared;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Collections.Generic;

namespace XIAOFUTools.Features.DataManagement.BatchProjectionDefinition
{
    internal partial class BatchProjectionDefinitionViewModel
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
        }

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
                    LogMessage($"已选择坐标系: {SelectedSpatialReference.Name}");
                }
            }
            catch (Exception ex)
            {
                LogError($"选择坐标系时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 为单个图层定义投影
        /// </summary>
        private async Task DefineProjectionForLayer(Layer layer)
        {
            await QueuedTask.Run(async () =>
            {
                try
                {
                    if (layer is FeatureLayer featureLayer)
                    {
                        // 对于要素图层，使用地理处理工具定义投影
                        var parameters = Geoprocessing.MakeValueArray(
                            featureLayer,
                            SelectedSpatialReference
                        );

                        var result = await Geoprocessing.ExecuteToolAsync(
                            "DefineProjection_management",
                            parameters,
                            null,
                            null,
                            null,
                            GPExecuteToolFlags.GPThread);
                        if (result.IsFailed)
                        {
                            throw new Exception($"定义投影失败: {string.Join(", ", result.Messages)}");
                        }
                    }
                    else if (layer is RasterLayer rasterLayer)
                    {
                        // 对于栅格图层，使用地理处理工具定义投影
                        var parameters = Geoprocessing.MakeValueArray(
                            rasterLayer,
                            SelectedSpatialReference
                        );

                        var result = await Geoprocessing.ExecuteToolAsync(
                            "DefineProjection_management",
                            parameters,
                            null,
                            null,
                            null,
                            GPExecuteToolFlags.GPThread);
                        if (result.IsFailed)
                        {
                            throw new Exception($"定义投影失败: {string.Join(", ", result.Messages)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"定义投影失败: {ex.Message}");
                }
            });
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
        /// 记录消息
        /// </summary>
        private void LogMessage(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] {message}\n";
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        private void LogError(string error)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogContent += $"[{timestamp}] 错误: {error}\n";
        }

        /// <summary>
        /// 显示帮助
        /// </summary>
        private void ShowHelp()
        {
            string helpContent = "批量定义投影工具使用说明\n\n" +
                               "功能描述：\n" +
                               "该工具用于为多个图层批量定义投影坐标系。\n\n" +
                               "参数说明：\n" +
                               "1. 图层列表：显示当前地图中的所有图层（除网络图层）\n" +
                               "2. 选择：勾选需要定义投影的图层\n" +
                               "3. 图层名称：图层的名称\n" +
                               "4. 类型：图层的类型（要素图层、栅格图层等）\n" +
                               "5. 当前坐标系：图层当前的坐标系\n\n" +
                               "操作步骤：\n" +
                               "1. 选择需要定义投影的图层（可使用全选/反选）\n" +
                               "2. 点击\"选择坐标系\"按钮选择目标坐标系\n" +
                               "3. 点击\"开始\"按钮执行批量定义投影\n\n" +
                               "注意事项：\n" +
                               "- 定义投影不会改变数据的实际坐标，只是告诉系统数据使用的坐标系\n" +
                               "- 请确保选择的坐标系与数据的实际坐标系一致\n" +
                               "- 操作过程和结果将显示在日志窗口中\n" +
                               "- 处理过程中可以点击\"停止\"按钮取消操作";

            PresentationServices.Dialogs.Show(helpContent, "批量定义投影工具使用说明");
        }
    }
}
