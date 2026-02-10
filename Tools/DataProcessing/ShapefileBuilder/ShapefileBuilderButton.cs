using ArcGIS.Desktop.Framework.Contracts;
using System;
using XIAOFUTools.Tools.Authorization;

namespace XIAOFUTools.Tools.DataProcessing.ShapefileBuilder
{
    /// <summary>
    /// 属性表建SHP工具按钮
    /// </summary>
    internal class ShapefileBuilderButton : Button
    {
        /// <summary>
        /// 按钮点击事件
        /// </summary>
        protected override void OnClick()
        {
            try
            {
                if (!AuthorizationChecker.CheckAuthorizationWithPrompt("属性表建SHP"))
                {
                    return;
                }

                ShapefileBuilderDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
