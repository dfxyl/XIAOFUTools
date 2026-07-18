using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace XIAOFUTools.Features.Editing.PasteSymbology
{
    /// <summary>
    /// 粘贴符号按钮
    /// </summary>
    internal class PasteSymbologyButton : Button
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

                // 获取选中的图层
                var selectedLayers = mapView.GetSelectedLayers();
                
                // 验证选中图层数量
                if (selectedLayers.Count < 2)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("请至少选择两个图层，第一个图层将作为符号源", "提示");
                    return;
                }

                // 获取第一个图层作为符号参考图层
                var symbologyLayer = selectedLayers[0];
                if (symbologyLayer == null)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("符号源图层无效", "错误");
                    return;
                }

                // 筛选有效的目标图层（要素图层和栅格图层）
                var targetLayers = new List<Layer>();
                for (int i = 1; i < selectedLayers.Count; i++)
                {
                    var layer = selectedLayers[i];
                    if (layer != null && (layer is FeatureLayer || layer is RasterLayer))
                    {
                        targetLayers.Add(layer);
                    }
                }

                if (targetLayers.Count == 0)
                {
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show("没有找到有效的目标图层（要素图层或栅格图层）", "提示");
                    return;
                }

                // 显示进度提示
                var progressDialog = new ArcGIS.Desktop.Framework.Threading.Tasks.ProgressDialog("正在应用符号系统...");
                progressDialog.Show();

                try
                {
                    // 顺序处理所有目标图层（GP工具在MCT上执行，并行无实际加速）
                    foreach (var targetLayer in targetLayers)
                    {
                        try
                        {
                            // 设置 ApplySymbologyFromLayer 工具的参数
                            var parameters = Geoprocessing.MakeValueArray(
                                targetLayer.URI,      // 目标图层的 URI
                                symbologyLayer.URI    // 符号参考图层的 URI
                            );

                            // 执行符号应用工具
                            var result = await Geoprocessing.ExecuteToolAsync("ApplySymbologyFromLayer_management", parameters);
                            
                            if (result.IsFailed)
                            {
                                // 记录失败信息但不中断其他任务
                                var errorMsg = string.Join(", ", result.Messages.Select(m => m.Text));
                                System.Diagnostics.Debug.WriteLine($"应用符号到图层 {targetLayer.Name} 失败: {errorMsg}");
                            }
                        }
                        catch (Exception ex)
                        {
                            // 记录异常信息但不中断其他任务
                            System.Diagnostics.Debug.WriteLine($"处理图层 {targetLayer.Name} 时发生异常: {ex.Message}");
                        }
                    }
                    
                    progressDialog.Hide();
                    
                    // 显示完成提示
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"成功将 {symbologyLayer.Name} 的符号应用到 {targetLayers.Count} 个图层", "完成");
                }
                catch (Exception ex)
                {
                    progressDialog.Hide();
                    ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"应用符号时发生错误: {ex.Message}", "错误");
                }
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"执行粘贴符号功能时发生错误: {ex.Message}", "错误");
            }
        }
    }
}
