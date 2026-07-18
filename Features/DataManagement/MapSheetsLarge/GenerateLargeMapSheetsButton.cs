using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.DataManagement.MapSheetsLarge
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
