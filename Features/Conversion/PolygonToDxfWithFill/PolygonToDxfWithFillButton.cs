using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Conversion.PolygonToDxfWithFill
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
