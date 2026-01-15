using System;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Dialogs;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.AttributeTransferFields
{
    internal class AttributeTransferFieldsButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 授权检查
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("属性传递[字段]"))
                    return;

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
