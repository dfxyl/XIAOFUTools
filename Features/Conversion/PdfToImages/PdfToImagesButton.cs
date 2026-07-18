using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Conversion.PdfToImages
{
    /// <summary>
    /// PDF批量转图片按钮
    /// </summary>
    internal class PdfToImagesButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开PDF批量转图片停靠窗格
                PdfToImagesDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
