using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Collections.Generic;
using System.Linq;

namespace XIAOFUTools.Tools.Analysis.ExportExcel
{
    /// <summary>
    /// 导出Excel按钮
    /// </summary>
    internal class ExportExcelButton : Button
    {
        protected override async void OnClick()
        {
            try
            {
                // 获取当前活动视图
                var mapView = MapView.Active;
                if (mapView == null)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请先打开一个地图视图", "提示");
                    return;
                }

                // 获取当前活动视图中的所有选中图层
                var selectedLayers = mapView.GetSelectedLayers().Cast<MapMember>().ToList();

                // 获取当前活动视图中的所有选中独立表
                var selectedStandaloneTables = mapView.GetSelectedStandaloneTables().Cast<MapMember>().ToList();

                // 如果没有选中内容，尝试获取所有独立表（需要在MCT线程上访问）
                if (!selectedLayers.Any() && !selectedStandaloneTables.Any())
                {
                    selectedStandaloneTables = await QueuedTask.Run(() =>
                        mapView.Map.StandaloneTables.Cast<MapMember>().ToList()
                    );
                }

                // 使用 LINQ 收集所有有效的图层或表格 URI
                var sourceURIs = await QueuedTask.Run(() =>
                    selectedLayers.Concat(selectedStandaloneTables) // 合并选中的图层和独立表
                        .Select(member => member switch
                        {
                            FeatureLayer featureLayer => featureLayer.URI.ToString(), // 处理 FeatureLayer
                            StandaloneTable table => table.URI.ToString(), // 处理 StandaloneTable
                            _ => null // 过滤其他类型
                        })
                        .Where(uri => !string.IsNullOrEmpty(uri)) // 过滤空 URI
                        .ToList() // 转换为列表
                );

                // 如果没有有效的图层或表格 URI，则返回
                if (!sourceURIs.Any())
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请先选择要导出的要素图层或独立表", "提示");
                    return;
                }

                // 设置导出到 Excel 的参数
                var parameters = Geoprocessing.MakeValueArray(
                    sourceURIs,  // 输入选中的图层或表格 URI 列表
                    "",
                    "true"
                );

                // 打开 TableToExcel 工具对话框，并传递参数
                await Geoprocessing.OpenToolDialogAsync("TableToExcel_conversion", parameters);
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出Excel时发生错误: {ex.Message}", "错误");
            }
        }
    }
}
