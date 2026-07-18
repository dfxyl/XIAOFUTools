using System.Collections.ObjectModel;
using System.Windows;

namespace XIAOFUTools.Features.Analysis.LandClassTable
{
    /// <summary>
    /// 封装分类报表配置窗口，隔离 ViewModel 对 WPF 窗口及 Owner 的依赖。
    /// </summary>
    internal interface ILandClassReportProfileDialogService
    {
        bool Show(
            ObservableCollection<LandClassReportProfile> profiles,
            LandClassReportProfile selectedProfile,
            out LandClassReportProfile updatedProfile);
    }

    internal sealed class LandClassReportProfileDialogService : ILandClassReportProfileDialogService
    {
        public bool Show(
            ObservableCollection<LandClassReportProfile> profiles,
            LandClassReportProfile selectedProfile,
            out LandClassReportProfile updatedProfile)
        {
            var window = new LandClassReportProfileWindow(profiles, selectedProfile)
            {
                Owner = Application.Current?.MainWindow
            };
            if (window.ShowDialog() == true)
            {
                updatedProfile = window.SelectedProfile;
                return true;
            }

            updatedProfile = null;
            return false;
        }
    }
}
