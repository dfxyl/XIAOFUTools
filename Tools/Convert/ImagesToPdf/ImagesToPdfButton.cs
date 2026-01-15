using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.ImagesToPdf
{
    /// <summary>
    /// 图片批量转PDF按钮
    /// </summary>
    internal class ImagesToPdfButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("图片批量转PDF工具"))
                {
                    return;
                }

                // 打开图片批量转PDF停靠窗格
                ImagesToPdfDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
