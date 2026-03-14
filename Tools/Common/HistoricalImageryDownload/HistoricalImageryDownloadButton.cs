using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.HistoricalImageryDownload
{
    internal class HistoricalImageryDownloadButton : Button
    {
        protected override void OnClick()
        {
            if (!AuthorizationChecker.CheckAuthorizationWithPrompt("历史影像下载工具"))
            {
                return;
            }

            if (MapView.Active == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "请先打开一个地图视图。",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            HistoricalImageryDownloadDockPane.Show();
        }
    }
}
