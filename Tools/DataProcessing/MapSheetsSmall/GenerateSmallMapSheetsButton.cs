using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.MapSheetsSmall
{
    /// <summary>
    /// 生成小比例尺图幅 按钮
    /// </summary>
    internal class GenerateSmallMapSheetsButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开停靠窗格
                GenerateSmallMapSheetsDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
