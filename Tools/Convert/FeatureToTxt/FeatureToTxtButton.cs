using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.FeatureToTxt
{
    /// <summary>
    /// 要素类转TXT按钮
    /// </summary>
    internal class FeatureToTxtButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("要素类转TXT工具"))
                {
                    return;
                }

                // 打开要素类转TXT停靠窗格
                FeatureToTxtDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
