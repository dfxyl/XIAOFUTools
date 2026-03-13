using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.MapSheetsSmallAssign
{
    internal class SmallMapSheetAssignButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("小比例图幅赋值"))
                {
                    return;
                }

                SmallMapSheetAssignDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开工具时出错: {ex.Message}", "错误");
            }
        }
    }
}
