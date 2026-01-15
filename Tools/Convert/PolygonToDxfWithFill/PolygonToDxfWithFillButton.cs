using System;
using ArcGIS.Desktop.Framework.Contracts;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.PolygonToDxfWithFill
{
    /// <summary>
    /// 面要素图层转DXF[带填充] 按钮
    /// </summary>
    internal class PolygonToDxfWithFillButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 授权检查
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("面要素图层转DXF[带填充]工具"))
                {
                    return;
                }

                // 打开停靠窗格
                PolygonToDxfWithFillDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
