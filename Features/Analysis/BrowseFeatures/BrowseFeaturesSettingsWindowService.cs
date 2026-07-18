using ArcGIS.Desktop.Mapping;

namespace XIAOFUTools.Features.Analysis.BrowseFeatures
{
    /// <summary>
    /// 封装浏览要素设置窗口的展示，避免 ViewModel 直接依赖 WPF 窗口。
    /// </summary>
    internal interface IBrowseFeaturesSettingsWindowService
    {
        bool Show(FeatureLayer selectedLayer);
    }

    internal sealed class BrowseFeaturesSettingsWindowService : IBrowseFeaturesSettingsWindowService
    {
        public bool Show(FeatureLayer selectedLayer)
        {
            var window = new BrowseFeaturesSettingsWindow(selectedLayer);
            return window.ShowDialog() == true;
        }
    }
}
