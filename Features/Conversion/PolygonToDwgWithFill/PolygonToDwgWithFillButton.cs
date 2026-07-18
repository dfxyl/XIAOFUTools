using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Conversion.PolygonToDwgWithFill
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
