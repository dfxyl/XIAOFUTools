using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;

namespace XIAOFUTools.Features.DataManagement.AttributeTransferFields
{
    internal class AttributeTransferFieldsButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开窗口
                AttributeTransferFieldsView.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
