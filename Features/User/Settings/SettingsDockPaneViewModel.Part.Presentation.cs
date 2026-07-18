using System;
using System.ComponentModel;
using System.Windows.Input;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using XIAOFUTools.Features.User.PluginUpdate;
using System.Threading.Tasks;

namespace XIAOFUTools.Features.User.Settings
{
    public partial class SettingsDockPaneViewModel
    {

        /// <summary>
        /// 打开预设图层文件夹
        /// </summary>
        private void OpenLayersFolder()
        {
            try
            {
                string folderPath = LayersPath;

                _presetLayerFileStore.EnsureDirectory(folderPath);

                // 打开文件夹
                PresentationServices.ExternalProcesses.Open(folderPath);
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show(
                    $"打开文件夹时出错: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 检查更新
        /// </summary>
        private async void CheckForUpdates()
        {
            try
            {
                using var progressDialog = PresentationServices.ProgressDialogs.CreateCancelable("正在检查更新", "取消", 100, true);
                var cps = progressDialog.ProgressorSource;

                var updateInfo = await QueuedTask.Run(async () =>
                {
                    return await UpdateChecker.CheckForUpdatesWithProgressAsync(cps);
                }, cps.Progressor);

                // 更新最后检查时间
                SettingsManager.Settings.PluginUpdate.LastCheckTime = DateTime.Now;
                SettingsManager.SaveSettings();
                NotifyPropertyChanged(nameof(LastCheckTimeText));

                if (!string.IsNullOrEmpty(updateInfo.ErrorMessage))
                {
                    PresentationServices.Dialogs.Show(
                        updateInfo.ErrorMessage,
                        "检查更新失败",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }

                if (updateInfo.HasUpdate)
                {
                    var message = $"发现新版本！\n\n当前版本: {updateInfo.CurrentVersion}\n最新版本: {updateInfo.LatestVersion}";

                    if (!string.IsNullOrEmpty(updateInfo.ReleaseDate))
                    {
                        message += $"\n发布日期: {updateInfo.ReleaseDate}";
                    }

                    message += $"\n\n更新内容:\n{updateInfo.UpdateNotes}";

                    if (!string.IsNullOrEmpty(updateInfo.Notice))
                    {
                        message += $"\n\n{updateInfo.Notice}";
                    }

                    if (!updateInfo.IsDesktopVersionCompatible)
                    {
                        var currentDv = string.IsNullOrEmpty(updateInfo.CurrentDesktopVersion) ? "未知" : updateInfo.CurrentDesktopVersion;
                        var minDv = string.IsNullOrEmpty(updateInfo.MinDesktopVersion) ? "未知" : updateInfo.MinDesktopVersion;

                        message += $"\n\n当前 ArcGIS Pro: {currentDv}\n最低支持版本: {minDv}\n版本不匹配，仅显示更新信息（不提供下载）。";

                        PresentationServices.Dialogs.Show(
                            message,
                            "发现更新",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Information);
                        return;
                    }

                    message += "\n\n是否立即下载并安装？";

                    var result = PresentationServices.Dialogs.Show(
                        message,
                        "发现更新",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        // 直接下载并安装，不再弹窗
                        await UpdateChecker.DownloadAndInstallUpdateAsync(updateInfo);
                    }
                }
                else
                {
                    PresentationServices.Dialogs.Show(
                        $"当前已是最新版本 ({updateInfo.CurrentVersion})",
                        "检查更新",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                PresentationServices.Dialogs.Show(
                    $"检查更新时出错: {ex.Message}",
                    "错误",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
