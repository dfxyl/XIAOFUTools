using ArcGIS.Desktop.Framework.Contracts;
using System;

namespace XIAOFUTools.Features.General.DownloadOnlineImagery
{
    /// <summary>
    /// 下载在线影像工具按钮
    /// </summary>
    internal class DownloadOnlineImageryButton : Button
    {
        /// <summary>
        /// 按钮点击事件
        /// </summary>
        protected override void OnClick()
        {
            try
            {
                // 打开下载在线影像停靠窗格
                DownloadOnlineImageryDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
