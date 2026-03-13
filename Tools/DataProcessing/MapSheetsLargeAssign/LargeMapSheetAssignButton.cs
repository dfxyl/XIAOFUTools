using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.MapSheetsLargeAssign
{
    internal class LargeMapSheetAssignButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("大比例图幅赋值"))
                {
                    return;
                }

                LargeMapSheetAssignDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
