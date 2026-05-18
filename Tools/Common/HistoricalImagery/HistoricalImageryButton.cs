using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Tools.HistoricalImagery
{
    /// <summary>
    /// 历史影像按钮
    /// </summary>
    internal class HistoricalImageryButton : Button
    {
        protected override void OnClick()
        {
            // 检查是否有活动地图
            if (MapView.Active == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "请先打开一个地图视图", 
                    "提示", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            // 显示停靠窗格
            HistoricalImageryDockPane.Show();
        }
    }
}
