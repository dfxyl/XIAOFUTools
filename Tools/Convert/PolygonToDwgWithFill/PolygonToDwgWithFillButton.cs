using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.PolygonToDwgWithFill
{
    /// <summary>
    /// 面要素图层转DWG[带色块填充] 按钮
    /// </summary>
    internal class PolygonToDwgWithFillButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 授权检查
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("面要素图层转DWG[带色块填充]工具"))
                {
                    return;
                }

                // 打开停靠窗格
                PolygonToDwgWithFillDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
