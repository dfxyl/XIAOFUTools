using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Features.Conversion.TxtToFeature
{
    /// <summary>
    /// TXT转SHP按钮
    /// </summary>
    internal class TxtToFeatureButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开TXT转SHP停靠窗格
                TxtToFeatureDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
