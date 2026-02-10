using System;
using System.Linq;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.AreaCalculator
{
    /// <summary>
    /// 计算面积按钮
    /// </summary>
    internal class AreaCalculatorButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("计算面积工具"))
                {
                    return;
                }

                // 优先传递当前选中图层名称（用于图层右键打开时自动定位）
                var selectedLayerName = MapView.Active?.GetSelectedLayers()?.FirstOrDefault()?.Name;

                // 打开计算面积停靠窗格
                AreaCalculatorDockPane.Show(selectedLayerName);
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
