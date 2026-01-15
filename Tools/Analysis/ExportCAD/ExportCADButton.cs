using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Collections.Generic;
using System.Linq;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.Analysis.ExportCAD
{
    /// <summary>
    /// 导出CAD按钮
    /// </summary>
    internal class ExportCADButton : Button
    {
        protected override async void OnClick()
        {
            try
            {
                // 检查授权状态
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("导出CAD功能"))
                {
                    return; // 授权无效时，退出操作
                }

                // 获取当前活动视图
                var mapView = MapView.Active;
                if (mapView == null)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请先打开一个地图视图", "提示");
                    return;
                }

                // 获取当前活动视图中的所有选中图层
                var selectedLayers = mapView.GetSelectedLayers();

                // 使用 LINQ 收集所有有效的图层 URI
                var layerSourceURIs = await QueuedTask.Run(() =>
                    selectedLayers
                        .OfType<FeatureLayer>()  // 仅选择 FeatureLayer
                        .Select(layer => layer.URI.ToString()) // 获取 URI
                        .Where(uri => !string.IsNullOrEmpty(uri)) // 过滤空 URI
                        .ToList() // 转换为列表
                );

                // 如果没有有效的图层 URI，则返回
                if (!layerSourceURIs.Any())
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请先选择要导出的要素图层", "提示");
                    return;
                }

                // 设置 CAD 导出参数
                var parameters = Geoprocessing.MakeValueArray(
                    layerSourceURIs,            // 输入要素图层 URI 列表
                    "DWG_R2007"                 // 输出类型
                );

                // 打开 ExportCAD 工具对话框，并传递参数
                await Geoprocessing.OpenToolDialogAsync("ExportCAD_conversion", parameters);
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"导出CAD时发生错误: {ex.Message}", "错误");
            }
        }
    }
}
