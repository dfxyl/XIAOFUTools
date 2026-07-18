using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ArcGIS.Core.Data;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Desktop.Mapping.Events;
using ArcGIS.Desktop.Framework.Events;
using XIAOFUTools.Features.User.Settings;

namespace XIAOFUTools.Features.Analysis.ViewArea
{
    internal partial class ViewAreaDockPaneViewModel
    {

        /// <summary>
        /// 地图选择变化事件处理
        /// </summary>
        private async void OnMapSelectionChanged(MapSelectionChangedEventArgs args)
        {
            try
            {
                // 检查是否启用了自动关闭功能
                if (SettingsManager.Settings.ViewArea.AutoCloseOnClearSelection)
                {
                    // 在MCT线程上检查是否有选择的要素
                    bool hasSelection = await QueuedTask.Run(() => HasAnySelectedFeatures());
                    if (!hasSelection)
                    {
                        // 如果窗口可见，则关闭它
                        if (ViewAreaDockPane.IsVisible())
                        {
                            ViewAreaDockPane.Close();
                            return; // 不需要刷新计算，因为窗口已关闭
                        }
                    }
                }

                RefreshCalculation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理选择变化事件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否有任何选择的要素（必须在MCT线程上调用）
        /// </summary>
        private bool HasAnySelectedFeatures()
        {
            try
            {
                var mapView = MapView.Active;
                if (mapView?.Map == null) return false;

                var selection = mapView.Map.GetSelection();
                if (selection == null) return false;

                // 检查是否有选择的要素图层
                foreach (var kvp in selection.ToDictionary())
                {
                    var layer = kvp.Key as FeatureLayer;
                    if (layer != null && kvp.Value != null && kvp.Value.Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查选择要素时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 刷新计算
        /// </summary>
        private async void RefreshCalculation()
        {
            IsCalculating = true;
            try
            {
                await QueuedTask.Run(() =>
                {
                    try
                    {
                        CalculateSelectedFeatures();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"计算选中要素时出错: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"错误: {ex.Message}");
            }
            finally
            {
                IsCalculating = false;
            }
        }

        /// <summary>
        /// 显示复制失败对话框，提供备用方案
        /// </summary>
        private void ShowCopyFailureDialog(string text)
        {
            var message = "剪贴板当前被其他应用程序占用，无法复制。\n\n" +
                         "您可以：\n" +
                         "1. 稍后再试点击\"复制结果\"按钮\n" +
                         "2. 双击表格中的数值直接复制单个数据\n" +
                         "3. 手动记录需要的数值\n\n" +
                         "建议关闭其他可能占用剪贴板的程序（如远程桌面、截图工具等）后重试。";

            PresentationServices.Dialogs.Show(message, "复制失败",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }

        /// <summary>
        /// 更新选择信息
        /// </summary>
        private void UpdateSelectionInfo(string info)
        {
            PresentationServices.UiThread.InvokeOrRun(() => SelectionInfo = info);
        }
    }
}
