using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.MapSheetsLarge
{
    /// <summary>
    /// 生成大比例尺图幅 按钮
    /// </summary>
    internal class GenerateLargeMapSheetsButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 授权检查
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("生成大比例尺图幅"))
                    return;

                // 打开停靠窗格
                GenerateLargeMapSheetsDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
