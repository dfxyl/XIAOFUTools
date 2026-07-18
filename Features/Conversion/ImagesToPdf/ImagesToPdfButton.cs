using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Conversion.ImagesToPdf
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
