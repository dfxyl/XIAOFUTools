using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.ExportToKml
{
    /// <summary>
    /// 要素图层分组导出KML/KMZ按钮
    /// </summary>
    internal class ExportToKmlButton : Button
    {
        protected override void OnClick()
        {
            try
            {
                // 打开停靠窗格
                ExportToKmlDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开停靠窗格时出错: {ex.Message}", "错误");
            }
        }
    }
}
