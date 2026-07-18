using System;
using System.Windows;
using XIAOFUTools.Features.Editing.Boundary.LayoutCoordinateTable;

namespace XIAOFUTools.Features.Cartography.MapSeriesExport.Presentation
{
    /// <summary>
    /// 隔离地图系列设置窗口的创建、宿主窗口关联和保存回调。
    /// </summary>
    internal interface IMapSeriesSettingsWindowService
    {
        void Show(CoordinateTableSettings settings, Action<CoordinateTableSettings> onSettingsSaved);
    }

    internal sealed class MapSeriesSettingsWindowService : IMapSeriesSettingsWindowService
    {
        public void Show(CoordinateTableSettings settings, Action<CoordinateTableSettings> onSettingsSaved)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(onSettingsSaved);

            var window = new MapSeriesSettingsWindow(settings)
            {
                Owner = Application.Current?.MainWindow
            };
            window.SettingsSaved += onSettingsSaved;
            window.Show();
        }
    }
}
