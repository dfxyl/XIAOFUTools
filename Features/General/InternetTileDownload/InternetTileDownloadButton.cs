using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.General.InternetTileDownload
{
    internal class InternetTileDownloadButton : Button
    {
        protected override void OnClick()
        {

            if (MapView.Active == null)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
                    "请先打开一个地图视图。",
                    "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            InternetTileDownloadDockPane.Show();
        }
    }
}
