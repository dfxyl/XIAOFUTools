using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.ProtocolLineExtract
{
    /// <summary>
    /// 提取协议线按钮
    /// </summary>
    internal class ProtocolLineExtractButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开停靠窗格
                ProtocolLineExtractDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
