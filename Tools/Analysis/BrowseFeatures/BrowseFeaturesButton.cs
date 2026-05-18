using System;
using ArcGIS.Desktop.Framework.Contracts;

namespace XIAOFUTools.Tools.BrowseFeatures
{
    internal class BrowseFeaturesButton : Button
    {
        protected override void OnClick()
        {
            try
            {

                BrowseFeaturesDockPane.Show();
            }
            catch (Exception ex)
            {
                ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show($"打开浏览要素停靠窗格失败: {ex.Message}", "错误");
            }
        }
    }
}
