using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.SpecialCoordinateTransform
{
    /// <summary>
    /// 特殊坐标转换按钮
    /// </summary>
    internal class SpecialCoordinateTransformButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开特殊坐标转换停靠窗格
                SpecialCoordinateTransformDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
