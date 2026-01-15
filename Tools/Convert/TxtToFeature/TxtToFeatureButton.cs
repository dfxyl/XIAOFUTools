using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.TxtToFeature
{
    /// <summary>
    /// TXT转SHP按钮
    /// </summary>
    internal class TxtToFeatureButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 检查授权
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("TXT转SHP工具"))
                {
                    return;
                }

                // 打开TXT转SHP停靠窗格
                TxtToFeatureDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
