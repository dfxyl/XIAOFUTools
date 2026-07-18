using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Conversion.DocumentBatchReplace
{
    internal class DocumentBatchReplaceButton : Button
    {
        protected override void OnClick()
        {
            try
            {

                DocumentBatchReplaceDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
