using ArcGIS.Desktop.Framework.Contracts;
using System;

namespace XIAOFUTools.Features.DataManagement.MirrorDatabase
{
    /// <summary>
    /// 镜像数据库工具按钮
    /// </summary>
    internal class MirrorDatabaseButton : Button
    {
        /// <summary>
        /// 按钮点击事件
        /// </summary>
        protected override void OnClick()
        {
            try
            {
                // 打开镜像数据库停靠窗格
                MirrorDatabaseDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
