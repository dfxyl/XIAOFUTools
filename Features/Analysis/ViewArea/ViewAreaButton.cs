using System;
using System.Linq;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Features.User.Settings;

namespace XIAOFUTools.Features.Analysis.ViewArea
{
    /// <summary>
    /// 查看面积按钮
    /// </summary>
    internal class ViewAreaButton : Button
    {
        protected override async void OnClick()
        {
            try
            {
                // 检查设置是否要求选择要素才能打开
                if (SettingsManager.Settings.ViewArea.RequireSelectionToOpen)
                {
                    // 在QueuedTask中检查是否有选择的要素
                    bool hasSelection = await QueuedTask.Run(() => HasSelectedFeatures());

                    if (!hasSelection)
                    {
                        ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                            "请先在地图中选择面要素或线要素，然后再打开查看面积工具。",
                            "提示",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                        return;
                    }
                }

                // 打开查看面积停靠窗格
                ViewAreaDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }

        /// <summary>
        /// 检查是否有选择的要素
        /// </summary>
        private bool HasSelectedFeatures()
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
    }
}
